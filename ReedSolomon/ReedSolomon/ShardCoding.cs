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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CombineRows(
        ReedSolomon codec,
        byte[][] coefficients,
        ReadOnlySpan<byte[]> inputShards,
        ReadOnlySpan<byte[]> outputShards)
    {
        const int pairedOutputCount = 2;

        var canUseAvx2Pair = codec.SimdEnabled && Avx2.IsSupported && outputShards.Length == pairedOutputCount;

        if (canUseAvx2Pair)
        {
            CombineTwoAvx2(codec, coefficients[0], coefficients[1], inputShards, outputShards[0], outputShards[1]);

            return;
        }

        var canUseArm64Pair = codec.SimdEnabled && AdvSimd.Arm64.IsSupported && outputShards.Length == pairedOutputCount;

        if (canUseArm64Pair)
        {
            CombineTwoNeon(coefficients[0], coefficients[1], inputShards, outputShards[0], outputShards[1], codec.BlockSizeBytes);

            return;
        }

        for (var outputIndex = 0; outputIndex < outputShards.Length; outputIndex++)
        {
            CombineRow(coefficients[outputIndex], inputShards, outputShards[outputIndex], codec.BlockSizeBytes, codec.SimdEnabled);
        }
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

    private static void CombineTwoNeon(
        byte[] firstCoefficientRow,
        byte[] secondCoefficientRow,
        ReadOnlySpan<byte[]> inputShards,
        byte[] firstOutput,
        byte[] secondOutput,
        int blockSizeBytes)
    {
        var nibbleMask = Vector128.Create((byte)LowNibbleMask);

        for (var blockStart = 0; blockStart < firstOutput.Length; blockStart += blockSizeBytes)
        {
            var blockEnd = Math.Min(firstOutput.Length, blockStart + blockSizeBytes);

            for (var inputIndex = 0; inputIndex < inputShards.Length; inputIndex++)
            {
                var firstLowNibbleTable = LowNibbleTables[firstCoefficientRow[inputIndex]].GetLower();
                var firstHighNibbleTable = HighNibbleTables[firstCoefficientRow[inputIndex]].GetLower();
                var secondLowNibbleTable = LowNibbleTables[secondCoefficientRow[inputIndex]].GetLower();
                var secondHighNibbleTable = HighNibbleTables[secondCoefficientRow[inputIndex]].GetLower();
                ref var source = ref MemoryMarshal.GetArrayDataReference(inputShards[inputIndex]);
                ref var firstDestination = ref MemoryMarshal.GetArrayDataReference(firstOutput);
                ref var secondDestination = ref MemoryMarshal.GetArrayDataReference(secondOutput);
                var byteOffset = blockStart;

                byteOffset = MultiplyArm64Vectors(
                    ref source, ref firstDestination, ref secondDestination,
                    byteOffset, blockEnd,
                    firstLowNibbleTable, firstHighNibbleTable,
                    secondLowNibbleTable, secondHighNibbleTable,
                    nibbleMask, inputIndex);

                for (; byteOffset < blockEnd; byteOffset++)
                {
                    var firstProduct = MultiplicationTables[firstCoefficientRow[inputIndex]][inputShards[inputIndex][byteOffset]];
                    var secondProduct = MultiplicationTables[secondCoefficientRow[inputIndex]][inputShards[inputIndex][byteOffset]];

                    if (inputIndex == 0)
                    {
                        firstOutput[byteOffset] = firstProduct;
                        secondOutput[byteOffset] = secondProduct;
                    }
                    else
                    {
                        firstOutput[byteOffset] ^= firstProduct;
                        secondOutput[byteOffset] ^= secondProduct;
                    }
                }
            }
        }
    }

    private static void CombineTwoAvx2(
        ReedSolomon codec,
        byte[] firstCoefficientRow,
        byte[] secondCoefficientRow,
        ReadOnlySpan<byte[]> inputShards,
        byte[] firstOutput,
        byte[] secondOutput)
    {
        var nibbleMask = Vector256.Create((byte)LowNibbleMask);
        ref var firstDestination = ref MemoryMarshal.GetArrayDataReference(firstOutput);
        ref var secondDestination = ref MemoryMarshal.GetArrayDataReference(secondOutput);

        for (var blockStart = 0; blockStart < firstOutput.Length; blockStart += codec.BlockSizeBytes)
        {
            var blockEnd = Math.Min(firstOutput.Length, blockStart + codec.BlockSizeBytes);

            for (var inputIndex = 0; inputIndex < inputShards.Length; inputIndex++)
            {
                var firstCoefficient = firstCoefficientRow[inputIndex];
                var secondCoefficient = secondCoefficientRow[inputIndex];
                var firstLowNibbleTable = LowNibbleTables[firstCoefficient];
                var firstHighNibbleTable = HighNibbleTables[firstCoefficient];
                var secondLowNibbleTable = LowNibbleTables[secondCoefficient];
                var secondHighNibbleTable = HighNibbleTables[secondCoefficient];
                ref var source = ref MemoryMarshal.GetArrayDataReference(inputShards[inputIndex]);
                var byteOffset = blockStart;

                byteOffset = MultiplyAvx2Vectors(
                    ref source, ref firstDestination, ref secondDestination,
                    byteOffset, blockEnd,
                    firstLowNibbleTable, firstHighNibbleTable,
                    secondLowNibbleTable, secondHighNibbleTable,
                    nibbleMask, inputIndex, codec.UnrollFactor);

                MultiplyScalarRemainder(
                    firstCoefficient, secondCoefficient,
                    inputShards, inputIndex, firstOutput, secondOutput,
                    byteOffset, blockEnd);
            }
        }
    }

    private static void CombineRow(byte[] coefficients, ReadOnlySpan<byte[]> inputShards, byte[] output,
        int blockSizeBytes, bool simdEnabled)
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
                    inputShards[inputIndex],
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
        byte[] input,
        byte[] output,
        int blockStart,
        int blockEnd,
        bool accumulate,
        bool simdEnabled)
    {
        if (coefficient == 0)
        {
            if (accumulate == false)
            {
                output.AsSpan(blockStart, blockEnd - blockStart).Clear();
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MultiplyScalarRemainder(
        byte firstCoefficient, byte secondCoefficient,
        ReadOnlySpan<byte[]> inputShards, int inputIndex,
        byte[] firstOutput, byte[] secondOutput,
        int byteOffset, int blockEnd)
    {
        var firstMultiplicationTable = MultiplicationTables[firstCoefficient];
        var secondMultiplicationTable = MultiplicationTables[secondCoefficient];

        for (; byteOffset < blockEnd; byteOffset++)
        {
            var firstProduct = firstMultiplicationTable[inputShards[inputIndex][byteOffset]];
            var secondProduct = secondMultiplicationTable[inputShards[inputIndex][byteOffset]];

            if (inputIndex == 0)
            {
                firstOutput[byteOffset] = firstProduct;
                secondOutput[byteOffset] = secondProduct;
            }
            else
            {
                firstOutput[byteOffset] ^= firstProduct;
                secondOutput[byteOffset] ^= secondProduct;
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
        byte coefficient, byte[] input, byte[] output,
        int byteOffset, int blockEnd, bool accumulate)
    {
        var lowNibbleTable = LowNibbleTables[coefficient];
        var highNibbleTable = HighNibbleTables[coefficient];
        var nibbleMask = Vector256.Create((byte)LowNibbleMask);
        ref var source = ref MemoryMarshal.GetArrayDataReference(input);
        ref var destination = ref MemoryMarshal.GetArrayDataReference(output);

        // Shard validation keeps every vector load and store inside its buffer.
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
        byte coefficient, byte[] input, byte[] output,
        int byteOffset, int blockEnd, bool accumulate)
    {
        var lowNibbleTable = LowNibbleTables[coefficient].GetLower();
        var highNibbleTable = HighNibbleTables[coefficient].GetLower();
        var nibbleMask = Vector128.Create((byte)LowNibbleMask);
        ref var source = ref MemoryMarshal.GetArrayDataReference(input);
        ref var destination = ref MemoryMarshal.GetArrayDataReference(output);

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
