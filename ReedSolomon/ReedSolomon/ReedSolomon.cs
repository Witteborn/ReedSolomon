using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Witteborn.ReedSolomon;

public sealed class ReedSolomon
{
    private const int DefaultBlockSizeBytes = 8192;
    private const int SingleVectorUnrollFactor = 1;

    private readonly int dataShardCount;
    private readonly int parityShardCount;
    private readonly byte[][] parityCoefficientRows;
    private readonly bool simdEnabled;
    private readonly int blockSizeBytes;
    private readonly int unrollFactor;
    private static readonly ConcurrentDictionary<(int DataShardCount, int ParityShardCount), byte[][]> ParityMatrixCache =
        new();

    // The indices 0 to 255 in order. Slices of it select the data or parity shards of any codec without allocating.
    private static readonly int[] ShardIndexSequence = Enumerable.Range(0, Galois.FIELD_SIZE).ToArray();

    internal static readonly byte[][] MultiplicationTables = ShardCoding.MultiplicationTables;

    internal bool SimdEnabled
    {
        get
        {
            return simdEnabled;
        }
    }

    internal int BlockSizeBytes
    {
        get
        {
            return blockSizeBytes;
        }
    }

    internal int UnrollFactor
    {
        get
        {
            return unrollFactor;
        }
    }

    /// <summary>Creates a codec for positive data/parity counts totaling at most 256.</summary>
    /// <param name="dataShards">Number of data shards.</param>
    /// <param name="parityShards">Number of parity shards.</param>
    /// <param name="simd">Use AVX2 or ARM64 NEON when available; otherwise use scalar code.</param>
    /// <param name="blockSize">Cache tile size in bytes, a positive multiple of 64.</param>
    /// <param name="unroll">AVX2 two-output unroll factor: 1, 2 or 4.</param>
    public ReedSolomon(
        int dataShards,
        int parityShards,
        bool simd = true,
        int blockSize = DefaultBlockSizeBytes,
        int unroll = SingleVectorUnrollFactor)
    {
        const int simdBlockAlignmentBytes = 64;
        const int twoVectorUnrollFactor = 2;
        const int fourVectorUnrollFactor = 4;

        var hasInvalidShardCounts = dataShards <= 0 || parityShards <= 0 || dataShards > Galois.FIELD_SIZE - parityShards;

        if (hasInvalidShardCounts)
        {
            throw new ArgumentException(
                $"Shard counts must be positive and total at most {Galois.FIELD_SIZE}, but got {dataShards} data and {parityShards} parity shards.");
        }

        dataShardCount = dataShards;
        parityShardCount = parityShards;

        var hasInvalidSimdConfiguration = blockSize <= 0
            || blockSize % simdBlockAlignmentBytes != 0
            || (unroll != SingleVectorUnrollFactor
            && unroll != twoVectorUnrollFactor
            && unroll != fourVectorUnrollFactor);

        if (hasInvalidSimdConfiguration)
        {
            throw new ArgumentException(
                $"Block size must be a positive multiple of {simdBlockAlignmentBytes} bytes and unroll must be {SingleVectorUnrollFactor}, {twoVectorUnrollFactor} or {fourVectorUnrollFactor}, but got block size {blockSize} and unroll {unroll}.");
        }

        simdEnabled = simd && (Avx2.IsSupported || AdvSimd.Arm64.IsSupported);
        blockSizeBytes = blockSize;
        unrollFactor = unroll;
        parityCoefficientRows = ParityMatrixCache.GetOrAdd(
            (dataShards, parityShards),
            static shardGeometry => BuildParityRows(shardGeometry.DataShardCount, shardGeometry.ParityShardCount));
    }

    private static byte[][] BuildParityRows(int dataShardCount, int parityShardCount)
    {
        // Identity rows preserve the original data shards in the systematic Vandermonde matrix.
        var totalShardCount = dataShardCount + parityShardCount;
        var matrix = new Matrix(totalShardCount, dataShardCount);

        for (var rowIndex = 0; rowIndex < totalShardCount; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < dataShardCount; columnIndex++)
            {
                var coefficient = Galois.Exp(unchecked((sbyte)rowIndex), columnIndex);

                matrix.Set(rowIndex, columnIndex, coefficient);
            }
        }

        var dataRows = matrix.Submatrix(0, 0, dataShardCount, dataShardCount);
        var dataRowsInverse = dataRows.Invert();

        matrix = matrix.Times(dataRowsInverse);

        var parityRows = new byte[parityShardCount][];

        for (var rowIndex = 0; rowIndex < parityShardCount; rowIndex++)
        {
            var matrixRow = matrix.GetRow(dataShardCount + rowIndex);

            parityRows[rowIndex] = Array.ConvertAll(
                matrixRow,
                coefficientValue => unchecked((byte)coefficientValue));
        }

        return parityRows;
    }

    /// <summary>Copies nonempty data into newly allocated shards, zero-pads and encodes parity.</summary>
    public byte[][] ManagedEncode(byte[] data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (data.Length == 0)
        {
            throw new ArgumentException("Data must not be empty because every shard needs at least one byte.", nameof(data));
        }

        var lastDataByteIndex = data.Length - 1;
        var lastDataShardIndex = lastDataByteIndex / dataShardCount;
        var shardLength = checked(lastDataShardIndex + 1);
        var shards = new byte[dataShardCount + parityShardCount][];

        for (var shardIndex = 0; shardIndex < shards.Length; shardIndex++)
        {
            // Parity is fully assigned by the first coefficient pass. Data
            // is copied in full except explicit zero padding in the final shards.
            shards[shardIndex] = GC.AllocateUninitializedArray<byte>(shardLength);

            if (shardIndex < dataShardCount)
            {
                var blockStart = shardIndex * shardLength;
                var remainingDataBytes = data.Length - blockStart;
                var availableDataBytes = Math.Max(0, remainingDataBytes);
                var bytesToCopy = Math.Min(shardLength, availableDataBytes);
                var sourceOffset = Math.Min(blockStart, data.Length);
                var sourceBytes = data.AsSpan(sourceOffset, bytesToCopy);

                sourceBytes.CopyTo(shards[shardIndex]);
                shards[shardIndex].AsSpan(bytesToCopy).Clear();
            }
        }

        EncodeParity(shards);

        return shards;
    }

    /// <summary>Overwrites all parity bytes using equal-sized, nonempty caller-owned shards.</summary>
    public void EncodeParity(byte[][] shards)
    {
        if (shards is null)
        {
            throw new ArgumentNullException(nameof(shards));
        }

        EncodeParityCore(new ArrayShardBuffers(shards));
    }

    /// <summary>Overwrites all parity ranges using equal-length, nonempty caller memory ranges.</summary>
    /// <remarks>
    /// Pass one range per shard, the data shards first and then the parity shards. A range may be a slice of a
    /// larger buffer at any offset, so one set of buffers sized for the longest shard serves every shorter one.
    /// A parity range must not overlap any other range. Bytes outside the ranges stay unchanged. The parity
    /// bytes are identical to those of the array overload. Once warm, this method does not allocate.
    /// </remarks>
    public void EncodeParity(Memory<byte>[] shards)
    {
        if (shards is null)
        {
            throw new ArgumentNullException(nameof(shards));
        }

        EncodeParityCore(new MemoryShardBuffers(shards));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EncodeParityCore<TShards>(TShards shards)
        where TShards : struct, IShardBuffers
    {
        var shardLength = ValidateShards(shards);

        ShardCoding.CombineRows(
            this,
            parityCoefficientRows,
            shards,
            ShardIndexSequence.AsSpan(0, dataShardCount),
            ShardIndexSequence.AsSpan(dataShardCount, parityShardCount),
            shardLength);
    }

    /// <summary>Restores null data shards and joins the data, removing the specified padding.</summary>
    /// <remarks>Zero-filled shards are valid data. Missing parity shards remain null.</remarks>
    public byte[] ManagedDecode(byte[]?[] shards, int paddingSize = 0)
    {
        var information = ManagedShardRecovery.Inspect(shards, paddingSize, dataShardCount, parityShardCount);

        if (information.MissingDataIndices.Count != 0)
        {
            ManagedShardRecovery.Restore(shards, information, dataShardCount, parityCoefficientRows, this);
        }

        return ManagedShardRecovery.JoinData(shards, information.ShardLength, paddingSize, dataShardCount);
    }

    /// <summary>Restores missing data into preallocated buffers using explicit presence flags.</summary>
    /// <remarks>Does not restore parity or modify the flags. Targets must not alias any other shard.</remarks>
    public void ReconstructDataInto(byte[][] shards, ReadOnlySpan<bool> present, DecodeWorkspace workspace)
    {
        if (workspace is null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (shards is null)
        {
            throw new ArgumentNullException(nameof(shards));
        }

        ReconstructDataCore(new ArrayShardBuffers(shards), present, workspace);
    }

    /// <summary>Restores missing data into preallocated memory ranges using explicit presence flags.</summary>
    /// <remarks>
    /// The ranges follow the rules of <see cref="EncodeParity(Memory{byte}[])"/>. Only the ranges of missing data
    /// shards are written, and their previous contents do not matter. The flags and the parity ranges stay
    /// unchanged. A missing data range must not overlap any other range. Once warm, this method does not allocate.
    /// </remarks>
    public void ReconstructDataInto(Memory<byte>[] shards, ReadOnlySpan<bool> present, DecodeWorkspace workspace)
    {
        if (workspace is null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (shards is null)
        {
            throw new ArgumentNullException(nameof(shards));
        }

        ReconstructDataCore(new MemoryShardBuffers(shards), present, workspace);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReconstructDataCore<TShards>(TShards shards, ReadOnlySpan<bool> present, DecodeWorkspace workspace)
        where TShards : struct, IShardBuffers
    {
        var shardLength = ValidateShards(shards);

        ValidateWorkspace(shards.Count, present, workspace);
        ValidateMissingDataBuffers(shards, present);

        var reconstructionPlan = GetReconstructionPlan(present, workspace);

        ShardCoding.CombineRows(
            this,
            reconstructionPlan.CoefficientRows,
            shards,
            reconstructionPlan.InputShardIndices,
            reconstructionPlan.MissingDataIndices,
            shardLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateWorkspace(int shardCount, ReadOnlySpan<bool> present, DecodeWorkspace workspace)
    {
        var hasIncompatibleWorkspace = present.Length != shardCount
            || workspace.DataShardCount != dataShardCount
            || workspace.ParityShardCount != parityShardCount;

        if (hasIncompatibleWorkspace)
        {
            throw CreateWorkspaceMismatchException(shardCount, present, workspace);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateMissingDataBuffers<TShards>(TShards shards, ReadOnlySpan<bool> present)
        where TShards : struct, IShardBuffers
    {
        for (var shardIndex = 0; shardIndex < dataShardCount; shardIndex++)
        {
            if (present[shardIndex])
            {
                continue;
            }

            var sharedShardIndex = shards.FindSharedShard(shardIndex, shards.Count);

            if (sharedShardIndex >= 0)
            {
                throw CreateBufferSharingException(shardIndex, sharedShardIndex);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReconstructionPlan GetReconstructionPlan(ReadOnlySpan<bool> present, DecodeWorkspace workspace)
    {
        const int maximumCachedReconstructionPlans = 128;

        var presenceKey = ShardPresenceKey.Create(present);

        if (workspace.Plans.TryGetValue(presenceKey, out var reconstructionPlan) == false)
        {
            reconstructionPlan = ReconstructionPlanner.Create(present, dataShardCount, parityCoefficientRows);

            if (workspace.Plans.Count == maximumCachedReconstructionPlans)
            {
                workspace.Plans.Clear();
            }

            workspace.Plans.Add(presenceKey, reconstructionPlan);
        }

        return reconstructionPlan;
    }

    /// <returns>The common length of all shards.</returns>
    private int ValidateShards<TShards>(TShards shards)
        where TShards : struct, IShardBuffers
    {
        var expectedShardCount = dataShardCount + parityShardCount;

        if (shards.Count != expectedShardCount)
        {
            throw new ArgumentException(
                $"Expected {expectedShardCount} shards ({dataShardCount} data and {parityShardCount} parity), but got {shards.Count}.");
        }

        var shardLength = shards.GetShardLength(0);

        if (shardLength == 0)
        {
            throw new ArgumentException("Shard 0 must exist and contain at least one byte, but it is missing or empty.");
        }

        for (var shardIndex = 1; shardIndex < shards.Count; shardIndex++)
        {
            var actualShardLength = shards.GetShardLength(shardIndex);

            if (actualShardLength != shardLength)
            {
                throw new ArgumentException(
                    $"Shard {shardIndex} must exist and have {shardLength} bytes like shard 0, but has {actualShardLength} bytes.");
            }
        }

        // Read-only data shards may share memory, but a writable parity shard may not.
        // Comparing with lower indices only checks every pair of shards once.
        for (var parityShardIndex = dataShardCount; parityShardIndex < shards.Count; parityShardIndex++)
        {
            var sharedShardIndex = shards.FindSharedShard(parityShardIndex, parityShardIndex);

            if (sharedShardIndex >= 0)
            {
                throw CreateBufferSharingException(parityShardIndex, sharedShardIndex);
            }
        }

        return shardLength;
    }

    // These messages stay outside the inlined validation so successful calls do not carry their formatting code.
    private ArgumentException CreateWorkspaceMismatchException(int shardCount, ReadOnlySpan<bool> present, DecodeWorkspace workspace)
    {
        return new ArgumentException(
            $"Expected {shardCount} presence flags and a workspace for {dataShardCount} data and {parityShardCount} parity shards, "
            + $"but got {present.Length} flags and a workspace for {workspace.DataShardCount} data and {workspace.ParityShardCount} parity shards.");
    }

    private static ArgumentException CreateBufferSharingException(int shardIndex, int otherShardIndex)
    {
        return new ArgumentException(
            $"Writable shard {shardIndex} must not share its buffer with shard {otherShardIndex}. "
            + "Every writable shard needs its own memory that overlaps no other shard.");
    }
}
