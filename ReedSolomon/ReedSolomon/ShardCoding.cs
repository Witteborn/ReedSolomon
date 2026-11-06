using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Witteborn.ReedSolomon;

/// <summary>Combines shard bytes using scalar code or the available processor's vector instructions.</summary>
/// <remarks>
/// A nibble is one four-bit half of a byte. The lookup tables multiply each half separately.
/// ARM64 NEON (exposed as AdvSimd) and x86 AVX2 process several bytes in parallel.
/// </remarks>
internal static class ShardCoding
{
    private const int BitsPerNibble = 4;
    private const byte LowNibbleMask = 0x0F;
    private const int NeonVectorSizeBytes = 16;
    private const int Avx2VectorSizeBytes = 32;

    internal static readonly byte[][] MultiplicationTables = BuildMultiplicationTables();
    private static readonly Vector256<byte>[] LowNibbleTables = BuildNibbleTables(useHighNibble: false);
    private static readonly Vector256<byte>[] HighNibbleTables = BuildNibbleTables(useHighNibble: true);

    /// <summary>Writes one combination of the selected input shards into each selected output shard.</summary>
    /// <remarks>
    /// Callers validate that all shards have <paramref name="shardLength"/> bytes and that no output shares memory
    /// with another shard. The vector loops rely on that length without bounds checks.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CombineRows<TShards>(
        ReedSolomon codec,
        byte[][] coefficients,
        TShards shards,
        ReadOnlySpan<int> inputShardIndices,
        ReadOnlySpan<int> outputShardIndices,
        int shardLength)
        where TShards : struct, IShardBuffers
    {
        const int pairedOutputCount = 2;

        var canUseAvx2Pair = codec.SimdEnabled && Avx2.IsSupported && outputShardIndices.Length == pairedOutputCount;

        if (canUseAvx2Pair)
        {
            CombineTwoAvx2(
                codec, coefficients[0], coefficients[1],
                shards, inputShardIndices,
                GetValidatedShard(shards, outputShardIndices[0], shardLength),
                GetValidatedShard(shards, outputShardIndices[1], shardLength));

            return;
        }

        var canUseArm64Pair = codec.SimdEnabled && AdvSimd.Arm64.IsSupported && outputShardIndices.Length == pairedOutputCount;

        if (canUseArm64Pair)
        {
            CombineTwoNeon(
                coefficients[0], coefficients[1],
                shards, inputShardIndices,
                GetValidatedShard(shards, outputShardIndices[0], shardLength),
                GetValidatedShard(shards, outputShardIndices[1], shardLength),
                codec.BlockSizeBytes);

            return;
        }

        for (var outputIndex = 0; outputIndex < outputShardIndices.Length; outputIndex++)
        {
            CombineRow(
                coefficients[outputIndex],
                shards,
                inputShardIndices,
                GetValidatedShard(shards, outputShardIndices[outputIndex], shardLength),
                codec.BlockSizeBytes,
                codec.SimdEnabled);
        }
    }

    // Validation ran before, but the caller may still replace a shard in its array from another thread.
    // Checking each shard when it is fetched keeps every unchecked vector access inside that shard.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span<byte> GetValidatedShard<TShards>(TShards shards, int shardIndex, int shardLength)
        where TShards : struct, IShardBuffers
    {
        var shard = shards.GetShard(shardIndex);

        if (shard.Length != shardLength)
        {
            ThrowShardChanged(shardIndex, shardLength, shard.Length);
        }

        return shard;
    }

    // Deliberately not marked NoInlining. The JIT then sees that this method never returns and keeps it
    // out of the register allocation of the vector loops. With NoInlining, arrays became 27 % slower.
    [DoesNotReturn]
    private static void ThrowShardChanged(int shardIndex, int shardLength, int actualShardLength)
    {
        throw new InvalidOperationException(
            $"Shard {shardIndex} changed during the call. It was validated with {shardLength} bytes but now has "
            + $"{actualShardLength} bytes. Shards must not be replaced while the codec uses them.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MultiplyAddPairVector(
        ref byte source,
        ref byte firstDestination,
        ref byte secondDestination,
        int byteOffset,
        Vector256<byte> firstLowNibbleTable,
        Vector256<byte> firstHighNibbleTable,
        Vector256<byte> secondLowNibbleTable,
        Vector256<byte> secondHighNibbleTable,
        Vector256<byte> nibbleMask,
        bool accumulate)
    {
        var value = Vector256.LoadUnsafe(ref source, (nuint)byteOffset);
        var lowNibbles = Avx2.And(value, nibbleMask);
        var shiftedHighBits = Avx2.ShiftRightLogical(value.AsUInt16(), BitsPerNibble);
        var highNibbles = Avx2.And(shiftedHighBits.AsByte(), nibbleMask);
        var firstProduct = Avx2.Xor(
            Avx2.Shuffle(firstLowNibbleTable, lowNibbles),
            Avx2.Shuffle(firstHighNibbleTable, highNibbles));
        var secondProduct = Avx2.Xor(
            Avx2.Shuffle(secondLowNibbleTable, lowNibbles),
            Avx2.Shuffle(secondHighNibbleTable, highNibbles));

        if (accumulate)
        {
            firstProduct = Avx2.Xor(firstProduct, Vector256.LoadUnsafe(ref firstDestination, (nuint)byteOffset));
            secondProduct = Avx2.Xor(secondProduct, Vector256.LoadUnsafe(ref secondDestination, (nuint)byteOffset));
        }

        firstProduct.StoreUnsafe(ref firstDestination, (nuint)byteOffset);
        secondProduct.StoreUnsafe(ref secondDestination, (nuint)byteOffset);
    }

    private static void CombineTwoNeon<TShards>(
        byte[] firstCoefficientRow,
        byte[] secondCoefficientRow,
        TShards shards,
        ReadOnlySpan<int> inputShardIndices,
        Span<byte> firstOutput,
        Span<byte> secondOutput,
        int blockSizeBytes)
        where TShards : struct, IShardBuffers
    {
        var keepsVectorLoopOutOfLine = typeof(TShards) == typeof(MemoryShardBuffers);
        var shardLength = firstOutput.Length;
        ref var firstDestination = ref MemoryMarshal.GetReference(firstOutput);
        ref var secondDestination = ref MemoryMarshal.GetReference(secondOutput);

        for (var blockStart = 0; blockStart < shardLength; blockStart += blockSizeBytes)
        {
            var blockEnd = Math.Min(shardLength, blockStart + blockSizeBytes);

            for (var inputIndex = 0; inputIndex < inputShardIndices.Length; inputIndex++)
            {
                var firstCoefficient = firstCoefficientRow[inputIndex];
                var secondCoefficient = secondCoefficientRow[inputIndex];
                ref var source = ref MemoryMarshal.GetReference(GetValidatedShard(shards, inputShardIndices[inputIndex], shardLength));

                if (keepsVectorLoopOutOfLine)
                {
                    MultiplyAddPairBlockNeonOutOfLine(
                        firstCoefficient, secondCoefficient, ref source, inputIndex,
                        ref firstDestination, ref secondDestination, blockStart, blockEnd);
                }
                else
                {
                    MultiplyAddPairBlockNeon(
                        firstCoefficient, secondCoefficient, ref source, inputIndex,
                        ref firstDestination, ref secondDestination, blockStart, blockEnd);
                }
            }
        }
    }

    private static void CombineTwoAvx2<TShards>(
        ReedSolomon codec,
        byte[] firstCoefficientRow,
        byte[] secondCoefficientRow,
        TShards shards,
        ReadOnlySpan<int> inputShardIndices,
        Span<byte> firstOutput,
        Span<byte> secondOutput)
        where TShards : struct, IShardBuffers
    {
        var keepsVectorLoopOutOfLine = typeof(TShards) == typeof(MemoryShardBuffers);
        var shardLength = firstOutput.Length;
        ref var firstDestination = ref MemoryMarshal.GetReference(firstOutput);
        ref var secondDestination = ref MemoryMarshal.GetReference(secondOutput);

        for (var blockStart = 0; blockStart < shardLength; blockStart += codec.BlockSizeBytes)
        {
            var blockEnd = Math.Min(shardLength, blockStart + codec.BlockSizeBytes);

            for (var inputIndex = 0; inputIndex < inputShardIndices.Length; inputIndex++)
            {
                var firstCoefficient = firstCoefficientRow[inputIndex];
                var secondCoefficient = secondCoefficientRow[inputIndex];
                ref var source = ref MemoryMarshal.GetReference(GetValidatedShard(shards, inputShardIndices[inputIndex], shardLength));

                if (keepsVectorLoopOutOfLine)
                {
                    MultiplyAddPairBlockAvx2OutOfLine(
                        firstCoefficient, secondCoefficient, ref source, inputIndex,
                        ref firstDestination, ref secondDestination, blockStart, blockEnd, codec.UnrollFactor);
                }
                else
                {
                    MultiplyAddPairBlockAvx2(
                        firstCoefficient, secondCoefficient, ref source, inputIndex,
                        ref firstDestination, ref secondDestination, blockStart, blockEnd, codec.UnrollFactor);
                }
            }
        }
    }

    // Memory<T>.Span inlines enough code into the generic callers that the JIT moved the destination
    // pointers of the vector loop to the stack, which made 6 KiB shards about 30 % slower. Memory ranges
    // therefore call the vector loop out of line. Arrays keep it inline because a call per input slows
    // down small shards. The typeof check is a constant for each struct instantiation.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MultiplyAddPairBlockNeonOutOfLine(
        byte firstCoefficient, byte secondCoefficient,
        ref byte source, int inputIndex,
        ref byte firstDestination, ref byte secondDestination,
        int blockStart, int blockEnd)
    {
        MultiplyAddPairBlockNeon(
            firstCoefficient, secondCoefficient, ref source, inputIndex,
            ref firstDestination, ref secondDestination, blockStart, blockEnd);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MultiplyAddPairBlockAvx2OutOfLine(
        byte firstCoefficient, byte secondCoefficient,
        ref byte source, int inputIndex,
        ref byte firstDestination, ref byte secondDestination,
        int blockStart, int blockEnd, int unrollFactor)
    {
        MultiplyAddPairBlockAvx2(
            firstCoefficient, secondCoefficient, ref source, inputIndex,
            ref firstDestination, ref secondDestination, blockStart, blockEnd, unrollFactor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MultiplyAddPairBlockNeon(
        byte firstCoefficient, byte secondCoefficient,
        ref byte source, int inputIndex,
        ref byte firstDestination, ref byte secondDestination,
        int blockStart, int blockEnd)
    {
        var nibbleMask = Vector128.Create((byte)LowNibbleMask);

        var byteOffset = MultiplyArm64Vectors(
            ref source, ref firstDestination, ref secondDestination,
            blockStart, blockEnd,
            LowNibbleTables[firstCoefficient].GetLower(), HighNibbleTables[firstCoefficient].GetLower(),
            LowNibbleTables[secondCoefficient].GetLower(), HighNibbleTables[secondCoefficient].GetLower(),
            nibbleMask, inputIndex);

        MultiplyScalarRemainder(
            firstCoefficient, secondCoefficient,
            ref source, inputIndex, ref firstDestination, ref secondDestination,
            byteOffset, blockEnd);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MultiplyAddPairBlockAvx2(
        byte firstCoefficient, byte secondCoefficient,
        ref byte source, int inputIndex,
        ref byte firstDestination, ref byte secondDestination,
        int blockStart, int blockEnd, int unrollFactor)
    {
        var nibbleMask = Vector256.Create((byte)LowNibbleMask);

        var byteOffset = MultiplyAvx2Vectors(
            ref source, ref firstDestination, ref secondDestination,
            blockStart, blockEnd,
            LowNibbleTables[firstCoefficient], HighNibbleTables[firstCoefficient],
            LowNibbleTables[secondCoefficient], HighNibbleTables[secondCoefficient],
            nibbleMask, inputIndex, unrollFactor);

        MultiplyScalarRemainder(
            firstCoefficient, secondCoefficient,
            ref source, inputIndex, ref firstDestination, ref secondDestination,
            byteOffset, blockEnd);
    }

    private static void CombineRow<TShards>(
        byte[] coefficients,
        TShards shards,
        ReadOnlySpan<int> inputShardIndices,
        Span<byte> output,
        int blockSizeBytes,
        bool simdEnabled)
        where TShards : struct, IShardBuffers
    {
        // Cache-sized spans retain output in cache across coefficient passes.
        // First term assigns; later terms XOR, avoiding a separate output clear.
        for (var blockStart = 0; blockStart < output.Length; blockStart += blockSizeBytes)
        {
            var blockEnd = Math.Min(output.Length, blockStart + blockSizeBytes);

            for (var inputIndex = 0; inputIndex < coefficients.Length; inputIndex++)
            {
                MultiplyAdd(
                    coefficients[inputIndex],
                    GetValidatedShard(shards, inputShardIndices[inputIndex], output.Length),
                    output,
                    blockStart,
                    blockEnd,
                    inputIndex != 0,
                    simdEnabled);
            }
        }
    }

    private static void MultiplyAdd(
        byte coefficient,
        ReadOnlySpan<byte> input,
        Span<byte> output,
        int blockStart,
        int blockEnd,
        bool accumulate,
        bool simdEnabled)
    {
        if (coefficient == 0)
        {
            if (accumulate == false)
            {
                output.Slice(blockStart, blockEnd - blockStart).Clear();
            }

            return;
        }

        var byteOffset = blockStart;

        var canUseAvx2 = simdEnabled && Avx2.IsSupported;

        if (canUseAvx2)
        {
            byteOffset = MultiplySingleOutputAvx2(coefficient, input, output, byteOffset, blockEnd, accumulate);
        }

        var canUseArm64 = simdEnabled && AdvSimd.Arm64.IsSupported;

        if (canUseArm64)
        {
            byteOffset = MultiplySingleOutputArm64(coefficient, input, output, byteOffset, blockEnd, accumulate);
        }

        var multiplicationTable = MultiplicationTables[coefficient];

        if (accumulate)
        {
            for (; byteOffset < blockEnd; byteOffset++)
            {
                output[byteOffset] ^= multiplicationTable[input[byteOffset]];
            }
        }
        else
        {
            for (; byteOffset < blockEnd; byteOffset++)
            {
                output[byteOffset] = multiplicationTable[input[byteOffset]];
            }
        }
    }

    private static byte[][] BuildMultiplicationTables()
    {
        var tables = new byte[Galois.FIELD_SIZE][];

        for (var coefficient = 0; coefficient < Galois.FIELD_SIZE; coefficient++)
        {
            tables[coefficient] = new byte[Galois.FIELD_SIZE];

            for (var fieldElement = 0; fieldElement < Galois.FIELD_SIZE; fieldElement++)
            {
                tables[coefficient][fieldElement] = unchecked((byte)Galois.Multiply(
                    unchecked((sbyte)coefficient),
                    unchecked((sbyte)fieldElement)));
            }
        }

        return tables;
    }

    private static Vector256<byte>[] BuildNibbleTables(bool useHighNibble)
    {
        const int nibbleValueCount = 16;

        var tables = new Vector256<byte>[Galois.FIELD_SIZE];

        for (var coefficient = 0; coefficient < Galois.FIELD_SIZE; coefficient++)
        {
            var nibbleProducts = new byte[nibbleValueCount];

            for (var nibbleValue = 0; nibbleValue < nibbleValueCount; nibbleValue++)
            {
                var byteValue = useHighNibble ? nibbleValue << BitsPerNibble : nibbleValue;

                nibbleProducts[nibbleValue] = MultiplicationTables[coefficient][byteValue];
            }

            var lookupLane = Vector128.LoadUnsafe(ref MemoryMarshal.GetArrayDataReference(nibbleProducts));

            tables[coefficient] = Vector256.Create(lookupLane, lookupLane);
        }

        return tables;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MultiplyFourVectorBatches(
        ref byte source, ref byte firstDestination, ref byte secondDestination,
        int byteOffset, int blockEnd,
        Vector256<byte> firstLowNibbleTable, Vector256<byte> firstHighNibbleTable,
        Vector256<byte> secondLowNibbleTable, Vector256<byte> secondHighNibbleTable,
        Vector256<byte> nibbleMask, int inputIndex)
    {
        const int fourVectorBatchSizeBytes = 4 * Avx2VectorSizeBytes;
        const int twoVectorBatchSizeBytes = 2 * Avx2VectorSizeBytes;
        const int thirdVectorOffsetBytes = 3 * Avx2VectorSizeBytes;

        for (; byteOffset <= blockEnd - fourVectorBatchSizeBytes; byteOffset += fourVectorBatchSizeBytes)
        {
            MultiplyAddPairVector(
                ref source, ref firstDestination, ref secondDestination,
                byteOffset,
                firstLowNibbleTable, firstHighNibbleTable,
                secondLowNibbleTable, secondHighNibbleTable,
                nibbleMask,
                inputIndex != 0);
            MultiplyAddPairVector(
                ref source, ref firstDestination, ref secondDestination,
                byteOffset + Avx2VectorSizeBytes,
                firstLowNibbleTable, firstHighNibbleTable,
                secondLowNibbleTable, secondHighNibbleTable,
                nibbleMask,
                inputIndex != 0);
            MultiplyAddPairVector(
                ref source, ref firstDestination, ref secondDestination,
                byteOffset + twoVectorBatchSizeBytes,
                firstLowNibbleTable, firstHighNibbleTable,
                secondLowNibbleTable, secondHighNibbleTable,
                nibbleMask,
                inputIndex != 0);
            MultiplyAddPairVector(
                ref source, ref firstDestination, ref secondDestination,
                byteOffset + thirdVectorOffsetBytes,
                firstLowNibbleTable, firstHighNibbleTable,
                secondLowNibbleTable, secondHighNibbleTable,
                nibbleMask,
                inputIndex != 0);
        }

        return byteOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MultiplyTwoVectorBatches(
        ref byte source, ref byte firstDestination, ref byte secondDestination,
        int byteOffset, int blockEnd,
        Vector256<byte> firstLowNibbleTable, Vector256<byte> firstHighNibbleTable,
        Vector256<byte> secondLowNibbleTable, Vector256<byte> secondHighNibbleTable,
        Vector256<byte> nibbleMask, int inputIndex)
    {
        const int twoVectorBatchSizeBytes = 2 * Avx2VectorSizeBytes;

        for (; byteOffset <= blockEnd - twoVectorBatchSizeBytes; byteOffset += twoVectorBatchSizeBytes)
        {
            MultiplyAddPairVector(
                ref source, ref firstDestination, ref secondDestination,
                byteOffset,
                firstLowNibbleTable, firstHighNibbleTable,
                secondLowNibbleTable, secondHighNibbleTable,
                nibbleMask,
                inputIndex != 0);
            MultiplyAddPairVector(
                ref source, ref firstDestination, ref secondDestination,
                byteOffset + Avx2VectorSizeBytes,
                firstLowNibbleTable, firstHighNibbleTable,
                secondLowNibbleTable, secondHighNibbleTable,
                nibbleMask,
                inputIndex != 0);
        }

        return byteOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MultiplyAvx2Vectors(
        ref byte source, ref byte firstDestination, ref byte secondDestination,
        int byteOffset, int blockEnd,
        Vector256<byte> firstLowNibbleTable, Vector256<byte> firstHighNibbleTable,
        Vector256<byte> secondLowNibbleTable, Vector256<byte> secondHighNibbleTable,
        Vector256<byte> nibbleMask, int inputIndex,
        int unrollFactor)
    {
        const int twoVectorUnrollFactor = 2;
        const int fourVectorUnrollFactor = 4;

        if (unrollFactor >= fourVectorUnrollFactor)
        {
            byteOffset = MultiplyFourVectorBatches(
                ref source, ref firstDestination, ref secondDestination,
                byteOffset, blockEnd,
                firstLowNibbleTable, firstHighNibbleTable,
                secondLowNibbleTable, secondHighNibbleTable,
                nibbleMask, inputIndex);
        }

        if (unrollFactor >= twoVectorUnrollFactor)
        {
            byteOffset = MultiplyTwoVectorBatches(
                ref source, ref firstDestination, ref secondDestination,
                byteOffset, blockEnd,
                firstLowNibbleTable, firstHighNibbleTable,
                secondLowNibbleTable, secondHighNibbleTable,
                nibbleMask, inputIndex);
        }

        // One input load and nibble split produce both output products.
        // Shard validation establishes all bounds; outputs are distinct.
        for (; byteOffset <= blockEnd - Avx2VectorSizeBytes; byteOffset += Avx2VectorSizeBytes)
        {
            MultiplyAddPairVector(
                ref source, ref firstDestination, ref secondDestination,
                byteOffset,
                firstLowNibbleTable, firstHighNibbleTable,
                secondLowNibbleTable, secondHighNibbleTable,
                nibbleMask,
                accumulate: inputIndex != 0);
        }

        return byteOffset;
    }

    // Shard validation keeps every offset below blockEnd inside the shards, like the vector loads before it.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MultiplyScalarRemainder(
        byte firstCoefficient, byte secondCoefficient,
        ref byte source, int inputIndex,
        ref byte firstDestination, ref byte secondDestination,
        int byteOffset, int blockEnd)
    {
        var firstMultiplicationTable = MultiplicationTables[firstCoefficient];
        var secondMultiplicationTable = MultiplicationTables[secondCoefficient];

        for (; byteOffset < blockEnd; byteOffset++)
        {
            var inputValue = Unsafe.Add(ref source, byteOffset);
            var firstProduct = firstMultiplicationTable[inputValue];
            var secondProduct = secondMultiplicationTable[inputValue];
            ref var firstOutputByte = ref Unsafe.Add(ref firstDestination, byteOffset);
            ref var secondOutputByte = ref Unsafe.Add(ref secondDestination, byteOffset);

            if (inputIndex == 0)
            {
                firstOutputByte = firstProduct;
                secondOutputByte = secondProduct;
            }
            else
            {
                firstOutputByte ^= firstProduct;
                secondOutputByte ^= secondProduct;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MultiplyArm64Vectors(
        ref byte source, ref byte firstDestination, ref byte secondDestination,
        int byteOffset, int blockEnd,
        Vector128<byte> firstLowNibbleTable, Vector128<byte> firstHighNibbleTable,
        Vector128<byte> secondLowNibbleTable, Vector128<byte> secondHighNibbleTable,
        Vector128<byte> nibbleMask, int inputIndex)
    {
        for (; byteOffset <= blockEnd - NeonVectorSizeBytes; byteOffset += NeonVectorSizeBytes)
        {
            var value = Vector128.LoadUnsafe(ref source, (nuint)byteOffset);
            var lowNibbles = AdvSimd.And(value, nibbleMask);
            var highNibbles = AdvSimd.ShiftRightLogical(value, BitsPerNibble);
            var firstProduct = AdvSimd.Xor(
                AdvSimd.Arm64.VectorTableLookup(firstLowNibbleTable, lowNibbles),
                AdvSimd.Arm64.VectorTableLookup(firstHighNibbleTable, highNibbles));
            var secondProduct = AdvSimd.Xor(
                AdvSimd.Arm64.VectorTableLookup(secondLowNibbleTable, lowNibbles),
                AdvSimd.Arm64.VectorTableLookup(secondHighNibbleTable, highNibbles));

            if (inputIndex != 0)
            {
                firstProduct = AdvSimd.Xor(
                    firstProduct,
                    Vector128.LoadUnsafe(ref firstDestination, (nuint)byteOffset));
                secondProduct = AdvSimd.Xor(
                    secondProduct,
                    Vector128.LoadUnsafe(ref secondDestination, (nuint)byteOffset));
            }

            firstProduct.StoreUnsafe(ref firstDestination, (nuint)byteOffset);
            secondProduct.StoreUnsafe(ref secondDestination, (nuint)byteOffset);
        }

        return byteOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MultiplySingleOutputAvx2(
        byte coefficient, ReadOnlySpan<byte> input, Span<byte> output,
        int byteOffset, int blockEnd, bool accumulate)
    {
        var lowNibbleTable = LowNibbleTables[coefficient];
        var highNibbleTable = HighNibbleTables[coefficient];
        var nibbleMask = Vector256.Create((byte)LowNibbleMask);
        ref var source = ref MemoryMarshal.GetReference(input);
        ref var destination = ref MemoryMarshal.GetReference(output);

        // Shard validation keeps every vector load and store inside its shard range.
        for (; byteOffset <= blockEnd - Avx2VectorSizeBytes; byteOffset += Avx2VectorSizeBytes)
        {
            var value = Vector256.LoadUnsafe(ref source, (nuint)byteOffset);
            var lowNibbles = Avx2.And(value, nibbleMask);
            var shiftedHighBits = Avx2.ShiftRightLogical(value.AsUInt16(), BitsPerNibble);
            var highNibbles = Avx2.And(shiftedHighBits.AsByte(), nibbleMask);
            var product = Avx2.Xor(
                Avx2.Shuffle(lowNibbleTable, lowNibbles),
                Avx2.Shuffle(highNibbleTable, highNibbles));

            if (accumulate)
            {
                product = Avx2.Xor(product, Vector256.LoadUnsafe(ref destination, (nuint)byteOffset));
            }

            product.StoreUnsafe(ref destination, (nuint)byteOffset);
        }

        return byteOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MultiplySingleOutputArm64(
        byte coefficient, ReadOnlySpan<byte> input, Span<byte> output,
        int byteOffset, int blockEnd, bool accumulate)
    {
        var lowNibbleTable = LowNibbleTables[coefficient].GetLower();
        var highNibbleTable = HighNibbleTables[coefficient].GetLower();
        var nibbleMask = Vector128.Create((byte)LowNibbleMask);
        ref var source = ref MemoryMarshal.GetReference(input);
        ref var destination = ref MemoryMarshal.GetReference(output);

        for (; byteOffset <= blockEnd - NeonVectorSizeBytes; byteOffset += NeonVectorSizeBytes)
        {
            var value = Vector128.LoadUnsafe(ref source, (nuint)byteOffset);
            var lowNibbles = AdvSimd.And(value, nibbleMask);
            var highNibbles = AdvSimd.ShiftRightLogical(value, BitsPerNibble);
            var product = AdvSimd.Xor(
                AdvSimd.Arm64.VectorTableLookup(lowNibbleTable, lowNibbles),
                AdvSimd.Arm64.VectorTableLookup(highNibbleTable, highNibbles));

            if (accumulate)
            {
                product = AdvSimd.Xor(product, Vector128.LoadUnsafe(ref destination, (nuint)byteOffset));
            }

            product.StoreUnsafe(ref destination, (nuint)byteOffset);
        }

        return byteOffset;
    }
}
