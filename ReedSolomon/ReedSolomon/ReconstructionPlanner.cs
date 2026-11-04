namespace Witteborn.ReedSolomon;

/// <summary>Prepares recovery coefficients from the surviving data and parity shards.</summary>
internal static class ReconstructionPlanner
{
    internal static ReconstructionPlan Create(
        ReadOnlySpan<bool> present,
        int dataShardCount,
        byte[][] parityCoefficientRows)
    {
        var (missingDataIndices, availableDataIndices, availableParityIndices) = FindAvailableShards(present, dataShardCount);

        var missingShardCount = missingDataIndices.Count;

        var presentShardCount = availableDataIndices.Count + availableParityIndices.Count;

        if (presentShardCount < dataShardCount)
        {
            throw new ArgumentException(
                $"Reconstruction needs at least {dataShardCount} shards, but only {presentShardCount} are present.");
        }

        if (missingShardCount == 0)
        {
            return new ReconstructionPlan([], [], []);
        }

        var recoveryInverse = CreateRecoveryInverse(missingDataIndices, availableParityIndices, parityCoefficientRows);
        var inputShardIndices = new int[dataShardCount];

        availableDataIndices.CopyTo(inputShardIndices);

        for (var parityIndex = 0; parityIndex < missingShardCount; parityIndex++)
        {
            inputShardIndices[availableDataIndices.Count + parityIndex] = dataShardCount + availableParityIndices[parityIndex];
        }

        var coefficientRows = CreateCoefficientRows(
            recoveryInverse, availableDataIndices, availableParityIndices, parityCoefficientRows, dataShardCount, missingShardCount);

        var missingShardIndices = missingDataIndices.ToArray();

        return new ReconstructionPlan(missingShardIndices, inputShardIndices, coefficientRows);
    }

    internal static Matrix CreateRecoveryInverse(
        List<int> missingDataIndices,
        List<int> availableParityIndices,
        byte[][] parityCoefficientRows)
    {
        var missingShardCount = missingDataIndices.Count;
        var recoveryMatrix = new Matrix(missingShardCount, missingShardCount);

        for (var missingDataIndex = 0; missingDataIndex < missingShardCount; missingDataIndex++)
        {
            for (var missingColumnIndex = 0; missingColumnIndex < missingShardCount; missingColumnIndex++)
            {
                var parityShardIndex = availableParityIndices[missingDataIndex];
                var dataShardIndex = missingDataIndices[missingColumnIndex];
                var coefficient = parityCoefficientRows[parityShardIndex][dataShardIndex];

                recoveryMatrix.Set(missingDataIndex, missingColumnIndex, unchecked((sbyte)coefficient));
            }
        }

        return recoveryMatrix.Invert();
    }

    private static (List<int> Missing, List<int> Data, List<int> Parity) FindAvailableShards(
        ReadOnlySpan<bool> present,
        int dataShardCount)
    {
        var missingDataIndices = new List<int>();
        var availableParityIndices = new List<int>();
        var availableDataIndices = new List<int>();

        for (var shardIndex = 0; shardIndex < present.Length; shardIndex++)
        {
            if (shardIndex < dataShardCount)
            {
                var dataIndices = present[shardIndex] ? availableDataIndices : missingDataIndices;

                dataIndices.Add(shardIndex);
            }
            else if (present[shardIndex])
            {
                availableParityIndices.Add(shardIndex - dataShardCount);
            }
        }

        return (missingDataIndices, availableDataIndices, availableParityIndices);
    }

    private static byte[][] CreateCoefficientRows(
        Matrix recoveryInverse,
        List<int> availableDataIndices,
        List<int> availableParityIndices,
        byte[][] parityCoefficientRows,
        int dataShardCount,
        int missingShardCount)
    {
        var coefficientRows = new byte[missingShardCount][];

        for (var missingDataIndex = 0; missingDataIndex < missingShardCount; missingDataIndex++)
        {
            var recoveryRow = recoveryInverse.GetRow(missingDataIndex);
            var inverseRow = Array.ConvertAll(
                recoveryRow, coefficientValue => unchecked((byte)coefficientValue));

            coefficientRows[missingDataIndex] = new byte[dataShardCount];

            WriteAvailableDataCoefficients(
                coefficientRows[missingDataIndex], inverseRow, availableDataIndices, availableParityIndices, parityCoefficientRows);

            for (var parityIndex = 0; parityIndex < missingShardCount; parityIndex++)
            {
                coefficientRows[missingDataIndex][availableDataIndices.Count + parityIndex] = inverseRow[parityIndex];
            }
        }

        return coefficientRows;
    }

    private static void WriteAvailableDataCoefficients(
        byte[] coefficientRow,
        byte[] inverseRow,
        List<int> availableDataIndices,
        List<int> availableParityIndices,
        byte[][] parityCoefficientRows)
    {
        for (var availableDataIndex = 0; availableDataIndex < availableDataIndices.Count; availableDataIndex++)
        {
            for (var parityIndex = 0; parityIndex < inverseRow.Length; parityIndex++)
            {
                var parityShardIndex = availableParityIndices[parityIndex];
                var dataShardIndex = availableDataIndices[availableDataIndex];
                var parityCoefficient = parityCoefficientRows[parityShardIndex][dataShardIndex];
                var multiplicationTable = ReedSolomon.MultiplicationTables[inverseRow[parityIndex]];

                coefficientRow[availableDataIndex] ^= multiplicationTable[parityCoefficient];
            }
        }
    }
}
