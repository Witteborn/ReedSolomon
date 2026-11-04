using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace ReferenceImplementation;

public sealed class ReedSolomon
{
    private const int DefaultBlockSizeBytes = 8192;
    private const int SingleVectorUnrollFactor = 1;
    private const int TwoVectorUnrollFactor = 2;
    private const int FourVectorUnrollFactor = 4;
    private const int BitsPerNibble = 4;
    private const byte LowNibbleMask = 0x0F;
    private const int NeonVectorSizeBytes = 16;
    private const int Avx2VectorSizeBytes = 32;

    private readonly int dataShardCount;
    private readonly int parityShardCount;
    private readonly byte[][] parityCoefficientRows;
    private readonly bool simdEnabled;
    private readonly int blockSizeBytes;
    private readonly int unrollFactor;
    private static readonly ConcurrentDictionary<(int Data, int Parity), byte[][]> ParityMatrixCache =
        new();
    private static readonly byte[][] MultiplicationTables = BuildMultiplicationTables();
    private static readonly Vector256<byte>[] LowNibbleTables = BuildNibbleTables(useHighNibble: false);
    private static readonly Vector256<byte>[] HighNibbleTables = BuildNibbleTables(useHighNibble: true);

    public ReedSolomon(
        int dataShards,
        int parityShards,
        bool simd = true,
        int blockSize = DefaultBlockSizeBytes,
        int unroll = SingleVectorUnrollFactor)
    {
        const int simdBlockAlignmentBytes = 64;

        var hasInvalidShardCounts = dataShards <= 0 || parityShards <= 0 || dataShards > Galois.FIELD_SIZE - parityShards;

        if (hasInvalidShardCounts)
        {
            throw new ArgumentException("invalid shard counts");
        }

        dataShardCount = dataShards;
        parityShardCount = parityShards;

        var hasInvalidSimdConfiguration = blockSize <= 0
            || blockSize % simdBlockAlignmentBytes != 0
            || (unroll != SingleVectorUnrollFactor
            && unroll != TwoVectorUnrollFactor
            && unroll != FourVectorUnrollFactor);

        if (hasInvalidSimdConfiguration)
        {
            throw new ArgumentException("invalid SIMD block/unroll configuration");
        }

        this.blockSizeBytes = blockSize;
        this.unrollFactor = unroll;
        simdEnabled = simd && (Avx2.IsSupported || AdvSimd.Arm64.IsSupported);
        parityCoefficientRows = ParityMatrixCache.GetOrAdd(
            (dataShards, parityShards),
            shardGeometry => BuildParityRows(shardGeometry.Data, shardGeometry.Parity));
    }

    private static byte[][] BuildParityRows(int dataShardCount, int parityShardCount)
    {
        // Identity rows preserve the original data shards in the systematic Vandermonde matrix.
        var matrix = new Matrix(dataShardCount + parityShardCount, dataShardCount);

        for (var rowIndex = 0; rowIndex < dataShardCount + parityShardCount; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < dataShardCount; columnIndex++)
            {
                matrix.Set(rowIndex, columnIndex, Galois.Exp(unchecked((sbyte)rowIndex), columnIndex));
            }
        }

        matrix = matrix.Times(matrix.Submatrix(0, 0, dataShardCount, dataShardCount).Invert());

        var parityRows = new byte[parityShardCount][];

        for (var rowIndex = 0; rowIndex < parityShardCount; rowIndex++)
        {
            parityRows[rowIndex] = Array.ConvertAll(
                matrix.GetRow(dataShardCount + rowIndex),
                coefficientValue => unchecked((byte)coefficientValue));
        }

        return parityRows;
    }

    public byte[][] ManagedEncode(byte[] data)
    {
        if (data.Length == 0)
        {
            throw new ArgumentException("empty data");
        }

        var shardLength = checked((data.Length + dataShardCount - 1) / dataShardCount);
        var shards = new byte[dataShardCount + parityShardCount][];

        for (var shardIndex = 0; shardIndex < shards.Length; shardIndex++)
        {
            // Parity is fully assigned by the first coefficient pass. Data
            // is copied in full except explicit zero padding in the final shards.
            shards[shardIndex] = GC.AllocateUninitializedArray<byte>(shardLength);

            if (shardIndex < dataShardCount)
            {
                var blockStart = shardIndex * shardLength;
                var bytesToCopy = Math.Min(shardLength, Math.Max(0, data.Length - blockStart));

                data.AsSpan(Math.Min(blockStart, data.Length), bytesToCopy).CopyTo(shards[shardIndex]);
                shards[shardIndex].AsSpan(bytesToCopy).Clear();
            }
        }

        EncodeParity(shards);

        return shards;
    }

    public void EncodeParity(byte[][] shards)
    {
        ValidateShards(shards);
        CombineRows(
            parityCoefficientRows,
            shards.AsSpan(0, dataShardCount),
            shards.AsSpan(dataShardCount, parityShardCount));
    }

    public byte[] ManagedDecode(byte[][] shards, int paddingSize = 0)
    {
        if (shards.Length != dataShardCount + parityShardCount)
        {
            throw new ArgumentException("wrong shard count");
        }

        var shardLength = 0;
        var presentShardCount = 0;
        var missingDataIndices = new List<int>();
        var availableParityIndices = new List<int>();

        for (var shardIndex = 0; shardIndex < shards.Length; shardIndex++)
        {
            if (shards[shardIndex] is byte[] shard)
            {
                var hasInvalidShardLength = shard.Length == 0 || (shardLength != 0 && shard.Length != shardLength);

                if (hasInvalidShardLength)
                {
                    throw new ArgumentException("unequal or empty shards");
                }

                shardLength = shard.Length;
                presentShardCount++;

                if (shardIndex >= dataShardCount)
                {
                    availableParityIndices.Add(shardIndex - dataShardCount);
                }
            }
            else if (shardIndex < dataShardCount)
            {
                missingDataIndices.Add(shardIndex);
            }
        }

        if (presentShardCount < dataShardCount)
        {
            throw new ArgumentException("not enough shards");
        }

        var hasInvalidPadding = paddingSize < 0 || paddingSize >= checked(dataShardCount * shardLength);

        if (hasInvalidPadding)
        {
            throw new ArgumentException("invalid padding");
        }

        // Transform the missing-shard inverse into coefficients for the actual
        // surviving inputs; no payload-sized residual shards are required.
        if (missingDataIndices.Count != 0)
        {
            var missingShardCount = missingDataIndices.Count;
            var recoveryMatrix = new Matrix(missingShardCount, missingShardCount);

            for (var missingDataIndex = 0; missingDataIndex < missingShardCount; missingDataIndex++)
            {
                for (var missingColumnIndex = 0; missingColumnIndex < missingShardCount; missingColumnIndex++)
                {
                    recoveryMatrix.Set(
                        missingDataIndex,
                        missingColumnIndex,
                        unchecked((sbyte)parityCoefficientRows[availableParityIndices[missingDataIndex]][missingDataIndices[missingColumnIndex]]));
                }
            }

            var inverse = recoveryMatrix.Invert();
            var inputShards = new byte[dataShardCount][];
            var availableDataIndices = new int[dataShardCount - missingShardCount];
            var nextInputIndex = 0;

            for (var availableDataIndex = 0; availableDataIndex < dataShardCount; availableDataIndex++)
            {
                if ((shards[availableDataIndex] is byte[] data) == false)
                {
                    continue;
                }

                availableDataIndices[nextInputIndex] = availableDataIndex;
                inputShards[nextInputIndex++] = data;
            }

            for (var parityIndex = 0; parityIndex < missingShardCount; parityIndex++)
            {
                inputShards[nextInputIndex + parityIndex] = shards[dataShardCount + availableParityIndices[parityIndex]];
            }

            var recoveryCoefficientRows = new byte[missingShardCount][];
            var restoredDataShards = new byte[missingShardCount][];

            for (var missingDataIndex = 0; missingDataIndex < missingShardCount; missingDataIndex++)
            {
                var inverseRow = Array.ConvertAll(
                    inverse.GetRow(missingDataIndex),
                    coefficientValue => unchecked((byte)coefficientValue));

                recoveryCoefficientRows[missingDataIndex] = new byte[dataShardCount];
                restoredDataShards[missingDataIndex] = GC.AllocateUninitializedArray<byte>(shardLength);

                for (var availableDataIndex = 0; availableDataIndex < availableDataIndices.Length; availableDataIndex++)
                {
                    var coefficient = (byte)0;

                    for (var parityIndex = 0; parityIndex < missingShardCount; parityIndex++)
                    {
                        coefficient ^= MultiplicationTables[inverseRow[parityIndex]][parityCoefficientRows[availableParityIndices[parityIndex]][availableDataIndices[availableDataIndex]]];
                    }

                    recoveryCoefficientRows[missingDataIndex][availableDataIndex] = coefficient;
                }

                for (var parityIndex = 0; parityIndex < missingShardCount; parityIndex++)
                {
                    recoveryCoefficientRows[missingDataIndex][nextInputIndex + parityIndex] = inverseRow[parityIndex];
                }
            }

            CombineRows(recoveryCoefficientRows, inputShards, restoredDataShards);

            for (var missingDataIndex = 0; missingDataIndex < missingShardCount; missingDataIndex++)
            {
                shards[missingDataIndices[missingDataIndex]] = restoredDataShards[missingDataIndex];
            }
        }

        // Every returned byte is copied below, including the final partial shard.
        var output = GC.AllocateUninitializedArray<byte>(checked(dataShardCount * shardLength - paddingSize));

        for (var shardIndex = 0; shardIndex < dataShardCount; shardIndex++)
        {
            var blockStart = shardIndex * shardLength;
            var bytesToCopy = Math.Min(shardLength, Math.Max(0, output.Length - blockStart));

            if (bytesToCopy != 0)
            {
                shards[shardIndex].AsSpan(0, bytesToCopy).CopyTo(output.AsSpan(blockStart, bytesToCopy));
            }
        }

        return output;
    }

    public void ReconstructDataInto(byte[][] shards, ReadOnlySpan<bool> present, DecodeWorkspace workspace)
    {
        const int shardsPerGroup = 64;
        const int maximumCachedReconstructionPlans = 128;

        ValidateShards(shards);

        var hasIncompatibleWorkspace = present.Length != shards.Length
            || workspace.Inputs.Length != dataShardCount
            || workspace.Outputs.Length != parityShardCount;

        if (hasIncompatibleWorkspace)
        {
            throw new ArgumentException("wrong mask or workspace geometry");
        }

        for (var shardIndex = 0; shardIndex < dataShardCount; shardIndex++)
        {
            if (present[shardIndex] == false)
            {
                for (var otherShardIndex = 0; otherShardIndex < shards.Length; otherShardIndex++)
                {
                    var targetAliasesAnotherShard = shardIndex != otherShardIndex && ReferenceEquals(
                        shards[shardIndex],
                        shards[otherShardIndex]);

                    if (targetAliasesAnotherShard)
                    {
                        throw new ArgumentException("missing data targets must not alias another shard");
                    }
                }
            }
        }

        var firstGroup = 0UL;
        var secondGroup = 0UL;
        var thirdGroup = 0UL;
        var fourthGroup = 0UL;

        for (var shardIndex = 0; shardIndex < present.Length; shardIndex++)
        {
            if (present[shardIndex])
            {
                var presenceBit = 1UL << (shardIndex % shardsPerGroup);

                switch (shardIndex / shardsPerGroup)
                {
                    case 0:
                        firstGroup |= presenceBit;
                        break;
                    case 1:
                        secondGroup |= presenceBit;
                        break;
                    case 2:
                        thirdGroup |= presenceBit;
                        break;
                    default:
                        fourthGroup |= presenceBit;
                        break;
                }
            }
        }

        var presenceMask = new ShardPresenceMask(firstGroup, secondGroup, thirdGroup, fourthGroup);

        if (workspace.Plans.TryGetValue(presenceMask, out var reconstructionPlan) == false)
        {
            reconstructionPlan = BuildReconstructionPlan(present);

            // A workspace belongs to one geometry/worker. Bound metadata growth.
            if (workspace.Plans.Count == maximumCachedReconstructionPlans)
            {
                workspace.Plans.Clear();
            }

            workspace.Plans.Add(presenceMask, reconstructionPlan);
        }

        if (reconstructionPlan.MissingDataIndices.Length == 0)
        {
            return;
        }

        for (var shardIndex = 0; shardIndex < reconstructionPlan.InputShardIndices.Length; shardIndex++)
        {
            workspace.Inputs[shardIndex] = shards[reconstructionPlan.InputShardIndices[shardIndex]];
        }

        for (var shardIndex = 0; shardIndex < reconstructionPlan.MissingDataIndices.Length; shardIndex++)
        {
            workspace.Outputs[shardIndex] = shards[reconstructionPlan.MissingDataIndices[shardIndex]];
        }

        CombineRows(
            reconstructionPlan.CoefficientRows,
            workspace.Inputs,
            workspace.Outputs.AsSpan(0, reconstructionPlan.MissingDataIndices.Length));
        // Do not retain per-call owned payloads through the persistent workspace.
        Array.Clear(workspace.Inputs);
        Array.Clear(workspace.Outputs);
    }

    private ReconstructionPlan BuildReconstructionPlan(ReadOnlySpan<bool> present)
    {
        var missingDataIndices = new List<int>();
        var availableParityIndices = new List<int>();
        var availableDataIndices = new List<int>();

        for (var shardIndex = 0; shardIndex < present.Length; shardIndex++)
        {
            if (shardIndex < dataShardCount)
            {
                if (present[shardIndex])
                {
                    availableDataIndices.Add(shardIndex);
                }
                else
                {
                    missingDataIndices.Add(shardIndex);
                }
            }
            else if (present[shardIndex])
            {
                availableParityIndices.Add(shardIndex - dataShardCount);
            }
        }

        var missingShardCount = missingDataIndices.Count;

        if (availableDataIndices.Count + availableParityIndices.Count < dataShardCount)
        {
            throw new ArgumentException("not enough shards");
        }

        if (missingShardCount == 0)
        {
            return new ReconstructionPlan(Array.Empty<int>(), Array.Empty<int>(), Array.Empty<byte[]>());
        }

        var recoveryMatrix = new Matrix(missingShardCount, missingShardCount);

        for (var missingDataIndex = 0; missingDataIndex < missingShardCount; missingDataIndex++)
        {
            for (var missingColumnIndex = 0; missingColumnIndex < missingShardCount; missingColumnIndex++)
            {
                recoveryMatrix.Set(
                    missingDataIndex,
                    missingColumnIndex,
                    unchecked((sbyte)parityCoefficientRows[availableParityIndices[missingDataIndex]][missingDataIndices[missingColumnIndex]]));
            }
        }

        var inverse = recoveryMatrix.Invert();
        var inputShardIndices = availableDataIndices.Concat(availableParityIndices.Take(missingShardCount).Select(shardIndex => dataShardCount + shardIndex)).ToArray();
        var recoveryCoefficientRows = new byte[missingShardCount][];

        for (var missingDataIndex = 0; missingDataIndex < missingShardCount; missingDataIndex++)
        {
            var inverseRow = Array.ConvertAll(
                inverse.GetRow(missingDataIndex),
                coefficientValue => unchecked((byte)coefficientValue));

            recoveryCoefficientRows[missingDataIndex] = new byte[dataShardCount];

            for (var availableDataIndex = 0; availableDataIndex < availableDataIndices.Count; availableDataIndex++)
            {
                for (var parityIndex = 0; parityIndex < missingShardCount; parityIndex++)
                {
                    recoveryCoefficientRows[missingDataIndex][availableDataIndex] ^= MultiplicationTables[inverseRow[parityIndex]][parityCoefficientRows[availableParityIndices[parityIndex]][availableDataIndices[availableDataIndex]]];
                }
            }

            for (var parityIndex = 0; parityIndex < missingShardCount; parityIndex++)
            {
                recoveryCoefficientRows[missingDataIndex][availableDataIndices.Count + parityIndex] = inverseRow[parityIndex];
            }
        }

        return new ReconstructionPlan(missingDataIndices.ToArray(), inputShardIndices, recoveryCoefficientRows);
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

    private void CombineTwoNeon(
        byte[] firstCoefficientRow,
        byte[] secondCoefficientRow,
        ReadOnlySpan<byte[]> inputShards,
        byte[] firstOutput,
        byte[] secondOutput)
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

    private void ValidateShards(byte[][] shards)
    {
        var hasInvalidShards = shards.Length != dataShardCount + parityShardCount || shards[0] is null || shards[0].Length == 0;

        if (hasInvalidShards)
        {
            throw new ArgumentException("wrong or empty shards");
        }

        var shardLength = shards[0].Length;

        foreach (var shard in shards)
        {
            var hasInvalidShardLength = shard is null || shard.Length != shardLength;

            if (hasInvalidShardLength)
            {
                throw new ArgumentException("unequal shard length");
            }
        }

        // Read-only data shards may alias, but a writable parity buffer may not.
        for (var parityShardIndex = dataShardCount; parityShardIndex < shards.Length; parityShardIndex++)
        {
            for (var otherShardIndex = 0; otherShardIndex < parityShardIndex; otherShardIndex++)
            {
                if (ReferenceEquals(shards[parityShardIndex], shards[otherShardIndex]))
                {
                    throw new ArgumentException("parity buffers must not alias data or other parity buffers");
                }
            }
        }
    }

    private void CombineRows(
        byte[][] coefficients,
        ReadOnlySpan<byte[]> inputShards,
        ReadOnlySpan<byte[]> outputShards)
    {
        const int pairedOutputCount = 2;

        var canUseAvx2Pair = simdEnabled && Avx2.IsSupported && outputShards.Length == pairedOutputCount;

        if (canUseAvx2Pair)
        {
            CombineTwoAvx2(coefficients[0], coefficients[1], inputShards, outputShards[0], outputShards[1]);

            return;
        }

        var canUseArm64Pair = simdEnabled && AdvSimd.Arm64.IsSupported && outputShards.Length == pairedOutputCount;

        if (canUseArm64Pair)
        {
            CombineTwoNeon(coefficients[0], coefficients[1], inputShards, outputShards[0], outputShards[1]);

            return;
        }

        for (var outputIndex = 0; outputIndex < outputShards.Length; outputIndex++)
        {
            CombineRow(coefficients[outputIndex], inputShards, outputShards[outputIndex]);
        }
    }

    private void CombineTwoAvx2(
        byte[] firstCoefficientRow,
        byte[] secondCoefficientRow,
        ReadOnlySpan<byte[]> inputShards,
        byte[] firstOutput,
        byte[] secondOutput)
    {
        const int quadAvx2VectorSizeBytes = 4 * Avx2VectorSizeBytes;
        const int doubleAvx2VectorSizeBytes = 2 * Avx2VectorSizeBytes;

        const int tripleAvx2VectorSizeBytes = 3 * Avx2VectorSizeBytes;

        var nibbleMask = Vector256.Create((byte)LowNibbleMask);
        ref var firstDestination = ref MemoryMarshal.GetArrayDataReference(firstOutput);
        ref var secondDestination = ref MemoryMarshal.GetArrayDataReference(secondOutput);

        for (var blockStart = 0; blockStart < firstOutput.Length; blockStart += blockSizeBytes)
        {
            var blockEnd = Math.Min(firstOutput.Length, blockStart + blockSizeBytes);

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

                if (unrollFactor >= FourVectorUnrollFactor)
                {
                    for (; byteOffset <= blockEnd - quadAvx2VectorSizeBytes; byteOffset += quadAvx2VectorSizeBytes)
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
                            byteOffset + doubleAvx2VectorSizeBytes,
                            firstLowNibbleTable, firstHighNibbleTable,
                            secondLowNibbleTable, secondHighNibbleTable,
                            nibbleMask,
                            inputIndex != 0);
                        MultiplyAddPairVector(
                            ref source, ref firstDestination, ref secondDestination,
                            byteOffset + tripleAvx2VectorSizeBytes,
                            firstLowNibbleTable, firstHighNibbleTable,
                            secondLowNibbleTable, secondHighNibbleTable,
                            nibbleMask,
                            inputIndex != 0);
                    }
                }

                if (unrollFactor >= TwoVectorUnrollFactor)
                {
                    for (; byteOffset <= blockEnd - doubleAvx2VectorSizeBytes; byteOffset += doubleAvx2VectorSizeBytes)
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
                }

                // One input load and nibble split produce both output products.
                // Shard validation establishes all bounds; outputs are distinct.
                for (; byteOffset <= blockEnd - Avx2VectorSizeBytes; byteOffset += Avx2VectorSizeBytes)
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

                    if (inputIndex != 0)
                    {
                        firstProduct = Avx2.Xor(
                            firstProduct,
                            Vector256.LoadUnsafe(ref firstDestination, (nuint)byteOffset));
                        secondProduct = Avx2.Xor(
                            secondProduct,
                            Vector256.LoadUnsafe(ref secondDestination, (nuint)byteOffset));
                    }

                    firstProduct.StoreUnsafe(ref firstDestination, (nuint)byteOffset);
                    secondProduct.StoreUnsafe(ref secondDestination, (nuint)byteOffset);
                }

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
        }
    }

    private void CombineRow(byte[] coefficients, ReadOnlySpan<byte[]> inputShards, byte[] output)
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
                    inputIndex != 0);
            }
        }
    }

    private void MultiplyAdd(
        byte coefficient,
        byte[] input,
        byte[] output,
        int blockStart,
        int blockEnd,
        bool accumulate)
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
        }

        var canUseArm64 = simdEnabled && AdvSimd.Arm64.IsSupported;

        if (canUseArm64)
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
                nibbleProducts[nibbleValue] = MultiplicationTables[coefficient][useHighNibble ? nibbleValue << BitsPerNibble : nibbleValue];
            }

            var lookupLane = Vector128.LoadUnsafe(ref MemoryMarshal.GetArrayDataReference(nibbleProducts));

            tables[coefficient] = Vector256.Create(lookupLane, lookupLane);
        }

        return tables;
    }
}
