namespace CompatibilityValidation;

/// <summary>Lists the shard geometries and loss patterns that every codec version must handle.</summary>
internal static class ProbeCases
{
    internal const int LargestExhaustiveShardCount = 12;
    private const int LargestSmallGeometryShardCount = 6;

    internal static IEnumerable<ShardGeometry> Geometries()
    {
        for (var dataShardCount = 1; dataShardCount <= LargestSmallGeometryShardCount; dataShardCount++)
        {
            for (var parityShardCount = 1; parityShardCount <= LargestSmallGeometryShardCount; parityShardCount++)
            {
                yield return new ShardGeometry(dataShardCount, parityShardCount);
            }
        }

        yield return new ShardGeometry(10, 2);
        yield return new ShardGeometry(10, 4);
        yield return new ShardGeometry(17, 3);
        yield return new ShardGeometry(128, 128);
        yield return new ShardGeometry(200, 56);
        yield return new ShardGeometry(255, 1);
        yield return new ShardGeometry(1, 255);
    }

    /// <summary>Creates a seed so each geometry encodes different payload bytes.</summary>
    internal static uint CreateSeed(ShardGeometry geometry, int payloadLength)
    {
        const int dataShardSeedFactor = 1000;

        return (uint)(geometry.DataShardCount * dataShardSeedFactor + geometry.ParityShardCount + payloadLength);
    }

    /// <summary>Returns every loss subset for small geometries and representative subsets for large ones.</summary>
    internal static IEnumerable<int[]> LossPatterns(ShardGeometry geometry)
    {
        return geometry.TotalShardCount <= LargestExhaustiveShardCount
            ? AllLossPatterns(geometry)
            : RepresentativeLossPatterns(geometry);
    }

    private static IEnumerable<int[]> AllLossPatterns(ShardGeometry geometry)
    {
        var patternCount = 1 << geometry.TotalShardCount;

        for (var pattern = 1; pattern < patternCount; pattern++)
        {
            var lostShards = Enumerable.Range(0, geometry.TotalShardCount)
                .Where(shardIndex => (pattern & (1 << shardIndex)) != 0)
                .ToArray();

            if (lostShards.Length <= geometry.ParityShardCount)
            {
                yield return lostShards;
            }
        }
    }

    private static IEnumerable<int[]> RepresentativeLossPatterns(ShardGeometry geometry)
    {
        var lossCount = geometry.ParityShardCount;
        var spacing = geometry.TotalShardCount / lossCount;

        yield return Enumerable.Range(0, lossCount).ToArray();
        yield return Enumerable.Range(geometry.TotalShardCount - lossCount, lossCount).ToArray();
        yield return Enumerable.Range(0, lossCount)
            .Select(lossIndex => lossIndex * spacing)
            .ToArray();
    }
}
