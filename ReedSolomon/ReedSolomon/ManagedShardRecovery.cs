using System.Runtime.InteropServices;

namespace Witteborn.ReedSolomon;

/// <summary>Validates, restores and joins shards for the allocating API.</summary>
internal static class ManagedShardRecovery
{
    internal static ManagedShardInfo Inspect(byte[]?[] shards, int paddingSize, int dataShardCount, int parityShardCount)
    {
        if (shards is null)
        {
            throw new ArgumentNullException(nameof(shards));
        }

        if (shards.Length != dataShardCount + parityShardCount)
        {
            throw new ArgumentException(
                $"Expected {dataShardCount + parityShardCount} shards ({dataShardCount} data and {parityShardCount} parity), but got {shards.Length}.");
        }

        var shardLength = 0;
        var presentShardCount = 0;
        var missingDataIndices = new List<int>();
        var availableParityIndices = new List<int>();

        for (var shardIndex = 0; shardIndex < shards.Length; shardIndex++)
        {
            var shard = shards[shardIndex];

            if (shard is null)
            {
                if (shardIndex < dataShardCount)
                {
                    missingDataIndices.Add(shardIndex);
                }

                continue;
            }

            var hasInvalidShardLength = shard.Length == 0 || (shardLength != 0 && shard.Length != shardLength);

            if (hasInvalidShardLength)
            {
                throw new ArgumentException(
                    $"All shards must be nonempty and equally long, but shard {shardIndex} has {shard.Length} bytes after shards of {shardLength} bytes.");
            }

            shardLength = shard.Length;
            presentShardCount++;

            if (shardIndex >= dataShardCount)
            {
                availableParityIndices.Add(shardIndex - dataShardCount);
            }
        }

        if (presentShardCount < dataShardCount)
        {
            throw new ArgumentException(
                $"Reconstruction needs at least {dataShardCount} shards, but only {presentShardCount} are present.");
        }

        var hasInvalidPadding = paddingSize < 0 || paddingSize >= checked(dataShardCount * shardLength);

        if (hasInvalidPadding)
        {
            throw new ArgumentException(
                $"Padding must be at least 0 and below the {dataShardCount * shardLength} bytes held by the data shards, but got {paddingSize}.");
        }

        return new ManagedShardInfo(shardLength, missingDataIndices, availableParityIndices);
    }

    internal static void Restore(
        byte[]?[] shards,
        ManagedShardInfo information,
        int dataShardCount,
        byte[][] parityCoefficientRows,
        ReedSolomon codec)
    {
        var inverse = ReconstructionPlanner.CreateRecoveryInverse(
            information.MissingDataIndices, information.AvailableParityIndices, parityCoefficientRows);

        var (inputShardIndices, dataIndices, parityStart) = GatherInputs(shards, information, dataShardCount);
        var coefficientRows = CreateRecoveryCoefficients(
            inverse, information, dataIndices, parityStart, dataShardCount, parityCoefficientRows);
        var missingDataIndices = CollectionsMarshal.AsSpan(information.MissingDataIndices);

        // Every restored byte is written by the first coefficient pass.
        foreach (var missingShardIndex in missingDataIndices)
        {
            shards[missingShardIndex] = GC.AllocateUninitializedArray<byte>(information.ShardLength);
        }

        ShardCoding.CombineRows(
            codec,
            coefficientRows,
            new ArrayShardBuffers(shards),
            inputShardIndices,
            missingDataIndices,
            information.ShardLength);
    }

    private static (int[] InputShardIndices, int[] DataIndices, int ParityStart) GatherInputs(
        byte[]?[] shards,
        ManagedShardInfo information,
        int dataShardCount)
    {
        var missingShardCount = information.MissingDataIndices.Count;
        var inputShardIndices = new int[dataShardCount];
        var dataIndices = new int[dataShardCount - missingShardCount];
        var nextInputIndex = 0;

        for (var dataShardIndex = 0; dataShardIndex < dataShardCount; dataShardIndex++)
        {
            if (shards[dataShardIndex] is null)
            {
                continue;
            }

            dataIndices[nextInputIndex] = dataShardIndex;
            inputShardIndices[nextInputIndex++] = dataShardIndex;
        }

        for (var parityIndex = 0; parityIndex < missingShardCount; parityIndex++)
        {
            inputShardIndices[nextInputIndex + parityIndex] = dataShardCount + information.AvailableParityIndices[parityIndex];
        }

        return (inputShardIndices, dataIndices, nextInputIndex);
    }

    private static byte[][] CreateRecoveryCoefficients(
        Matrix inverse,
        ManagedShardInfo information,
        int[] dataIndices,
        int parityStart,
        int dataShardCount,
        byte[][] parityCoefficientRows)
    {
        var missingShardCount = information.MissingDataIndices.Count;
        var coefficientRows = new byte[missingShardCount][];

        for (var missingIndex = 0; missingIndex < missingShardCount; missingIndex++)
        {
            var recoveryRow = inverse.GetRow(missingIndex);
            var inverseRow = Array.ConvertAll(
                recoveryRow, coefficientValue => unchecked((byte)coefficientValue));

            coefficientRows[missingIndex] = new byte[dataShardCount];

            WriteRecoveryCoefficients(
                coefficientRows[missingIndex], inverseRow, dataIndices,
                information.AvailableParityIndices, parityCoefficientRows, parityStart);
        }

        return coefficientRows;
    }

    private static void WriteRecoveryCoefficients(
        byte[] coefficientRow,
        byte[] inverseRow,
        int[] dataIndices,
        List<int> availableParityIndices,
        byte[][] parityCoefficientRows,
        int parityStart)
    {
        for (var dataIndex = 0; dataIndex < dataIndices.Length; dataIndex++)
        {
            var coefficient = (byte)0;

            for (var parityIndex = 0; parityIndex < inverseRow.Length; parityIndex++)
            {
                var parityShardIndex = availableParityIndices[parityIndex];
                var dataShardIndex = dataIndices[dataIndex];
                var parityCoefficient = parityCoefficientRows[parityShardIndex][dataShardIndex];
                var multiplicationTable = ReedSolomon.MultiplicationTables[inverseRow[parityIndex]];

                coefficient ^= multiplicationTable[parityCoefficient];
            }

            coefficientRow[dataIndex] = coefficient;
        }

        for (var parityIndex = 0; parityIndex < inverseRow.Length; parityIndex++)
        {
            coefficientRow[parityStart + parityIndex] = inverseRow[parityIndex];
        }
    }

    internal static byte[] JoinData(byte[]?[] shards, int shardLength, int paddingSize, int dataShardCount)
    {
        // Every returned byte is copied below, including the final partial shard.
        var paddedDataLength = checked(dataShardCount * shardLength);
        var outputLength = checked(paddedDataLength - paddingSize);
        var output = GC.AllocateUninitializedArray<byte>(outputLength);

        for (var shardIndex = 0; shardIndex < dataShardCount; shardIndex++)
        {
            var blockStart = shardIndex * shardLength;
            var remainingOutputBytes = output.Length - blockStart;
            var availableOutputBytes = Math.Max(0, remainingOutputBytes);
            var bytesToCopy = Math.Min(shardLength, availableOutputBytes);

            if (bytesToCopy != 0)
            {
                shards[shardIndex]!.AsSpan(0, bytesToCopy).CopyTo(output.AsSpan(blockStart, bytesToCopy));
            }
        }

        return output;
    }
}
