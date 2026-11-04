namespace ReedSolomonTests;

public static class ShardLossCases
{
    private const int MaximumExhaustiveShardCountPerKind = 6;
    private const int DefaultShardLength = 65;
    private const int RandomSeed = 16102026;

    public static IEnumerable<TestDataRow<ShardLossCase>> ExhaustiveLossCases()
    {
        for (var dataShardCount = 1; dataShardCount <= MaximumExhaustiveShardCountPerKind; dataShardCount++)
        {
            for (var parityShardCount = 1; parityShardCount <= MaximumExhaustiveShardCountPerKind; parityShardCount++)
            {
                var totalShardCount = dataShardCount + parityShardCount;
                var lossPatternCount = 1 << totalShardCount;

                // Every subset is tested, including no loss and losses beyond the recovery limit.
                for (var lossMask = 0; lossMask < lossPatternCount; lossMask++)
                {
                    var missingShardIndices = Enumerable.Range(0, totalShardCount)
                        .Where(shardIndex => (lossMask & (1 << shardIndex)) != 0)
                        .ToArray();

                    foreach (var row in CreateRows(
                        dataShardCount, parityShardCount, missingShardIndices, DefaultShardLength, RandomSeed + lossMask))
                    {
                        yield return row;
                    }
                }
            }
        }
    }

    public static IEnumerable<TestDataRow<ShardLossCase>> LargerLossCases()
    {
        const int maximumShardCount = 256;
        const int maximumFullySampledShardCount = 32;
        const int geometrySeedStride = 1009;

        var geometries = new (int DataShardCount, int ParityShardCount)[]
        {
            (2, 13), (13, 2), (3, 7), (7, 3), (7, 7), (10, 2), (16, 4),
            (11, 17), (17, 11), (31, 33), (33, 31), (64, 64),
            (1, 255), (2, 254), (63, 193), (64, 192), (65, 191),
            (127, 129), (128, 128), (129, 127),
            (191, 65), (192, 64), (193, 63), (200, 56), (254, 2), (255, 1)
        };

        for (var geometryIndex = 0; geometryIndex < geometries.Length; geometryIndex++)
        {
            var (dataShardCount, parityShardCount) = geometries[geometryIndex];
            var totalShardCount = dataShardCount + parityShardCount;
            var seed = RandomSeed + geometryIndex * geometrySeedStride;
            var random = new Random(seed);
            var sampledLossCounts = totalShardCount <= maximumFullySampledShardCount
                ? Enumerable.Range(0, parityShardCount + 2)
                : new[] { 0, 1, 2, 3, parityShardCount / 2, parityShardCount - 1, parityShardCount, parityShardCount + 1 };

            var lossCounts = sampledLossCounts.Append(totalShardCount).Distinct();

            foreach (var missingShardCount in lossCounts)
            {
                var shardIndices = Enumerable.Range(0, totalShardCount).ToArray();

                random.Shuffle(shardIndices);

                var missingShardIndices = shardIndices.Take(missingShardCount).Order().ToArray();

                foreach (var row in CreateRows(dataShardCount, parityShardCount, missingShardIndices, DefaultShardLength, seed))
                {
                    yield return row;
                }
            }

            // Check both extremes explicitly: only data loss and only parity loss.
            foreach (var missingShardIndices in new[]
            {
                Enumerable.Range(0, dataShardCount).ToArray(),
                Enumerable.Range(dataShardCount, parityShardCount).ToArray(),
                Enumerable.Range(0, parityShardCount).ToArray(),
                Enumerable.Range(totalShardCount - parityShardCount, parityShardCount).ToArray()
            })
            {
                foreach (var row in CreateRows(dataShardCount, parityShardCount, missingShardIndices, DefaultShardLength, seed))
                {
                    yield return row;
                }
            }

            if (totalShardCount == maximumShardCount)
            {
                // Exercise the last shard and each boundary between 64-bit presence groups.
                var missingShardIndices = new[] { 0, 63, 64, 127, 128, 191, 192, 255 };

                foreach (var row in CreateRows(dataShardCount, parityShardCount, missingShardIndices, DefaultShardLength, seed))
                {
                    yield return row;
                }
            }
        }
    }

    public static IEnumerable<TestDataRow<ShardLossCase>> RandomLossCases()
    {
        const int sampleCount = 256;
        const int maximumDataShardCount = 48;
        const int maximumParityShardCount = 48;

        var random = new Random(RandomSeed);
        var shardLengths = new[] { 1, 31, 32, 33, 63, 64, 65, 127, 128, 129, 8191, 8192, 8193 };

        for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            var dataShardCount = random.Next(1, maximumDataShardCount + 1);
            var parityShardCount = random.Next(1, maximumParityShardCount + 1);
            var totalShardCount = dataShardCount + parityShardCount;
            var missingShardCount = random.Next(0, totalShardCount + 1);
            var shardIndices = Enumerable.Range(0, totalShardCount).ToArray();

            random.Shuffle(shardIndices);

            var missingShardIndices = shardIndices.Take(missingShardCount).Order().ToArray();
            var shardLength = shardLengths[sampleIndex % shardLengths.Length];

            foreach (var row in CreateRows(dataShardCount, parityShardCount, missingShardIndices, shardLength, RandomSeed + sampleIndex))
            {
                yield return row;
            }
        }
    }

    private static IEnumerable<TestDataRow<ShardLossCase>> CreateRows(
        int dataShardCount,
        int parityShardCount,
        int[] missingShardIndices,
        int shardLength,
        int seed)
    {
        foreach (var useSimd in new[] { false, true })
        {
            var testCase = new ShardLossCase(dataShardCount, parityShardCount, missingShardIndices, useSimd, shardLength, seed);

            yield return new TestDataRow<ShardLossCase>(testCase)
            {
                ArgumentsDisplayName = testCase.ToString()
            };
        }
    }
}
