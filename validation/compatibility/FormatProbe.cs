using System.Security.Cryptography;

namespace CompatibilityValidation;

/// <summary>Measures parity bytes and buffer-level recovery, which define the stored shard format.</summary>
internal static class FormatProbe
{
    private static readonly int[] SmallGeometryShardLengths = [1, 31, 32, 33, 64, 4096];
    private static readonly int[] LargeGeometryShardLengths = [33, 4096];

    internal static FormatResult[] Run()
    {
        var results = new List<FormatResult>();

        foreach (var geometry in ProbeCases.Geometries())
        {
            var hasManyShards = geometry.TotalShardCount > ProbeCases.LargestExhaustiveShardCount;
            var shardLengths = hasManyShards ? LargeGeometryShardLengths : SmallGeometryShardLengths;

            foreach (var shardLength in shardLengths)
            {
                results.Add(RunCase(geometry, shardLength));
            }
        }

        return results.ToArray();
    }

    private static FormatResult RunCase(ShardGeometry geometry, int shardLength)
    {
        var codec = new CodecUnderTest(geometry.DataShardCount, geometry.ParityShardCount);
        var shards = CreateEncodedShards(codec, geometry, shardLength);
        var parityBytes = shards.Skip(geometry.DataShardCount).SelectMany(shard => shard).ToArray();
        var parityHash = Convert.ToHexString(SHA256.HashData(parityBytes));
        var lossPatternCount = 0;
        var restoredPatternCount = 0;

        foreach (var lostShards in ProbeCases.LossPatterns(geometry))
        {
            lossPatternCount++;

            if (RestoresData(codec, geometry, shards, lostShards))
            {
                restoredPatternCount++;
            }
        }

        var caseName = $"{geometry.DataShardCount}+{geometry.ParityShardCount}, {shardLength} bytes per shard";

        return new FormatResult(caseName, parityHash, lossPatternCount, restoredPatternCount);
    }

    private static byte[][] CreateEncodedShards(CodecUnderTest codec, ShardGeometry geometry, int shardLength)
    {
        var payloadLength = shardLength * geometry.DataShardCount;
        var payload = Payload.Create(payloadLength, ProbeCases.CreateSeed(geometry, payloadLength));
        var shards = new byte[geometry.TotalShardCount][];

        for (var shardIndex = 0; shardIndex < shards.Length; shardIndex++)
        {
            shards[shardIndex] = new byte[shardLength];
        }

        for (var dataShardIndex = 0; dataShardIndex < geometry.DataShardCount; dataShardIndex++)
        {
            payload.AsSpan(dataShardIndex * shardLength, shardLength).CopyTo(shards[dataShardIndex]);
        }

        codec.EncodeParity(shards);

        return shards;
    }

    private static bool RestoresData(
        CodecUnderTest codec,
        ShardGeometry geometry,
        byte[][] originalShards,
        int[] lostShards)
    {
        const byte dirtyBufferValue = 0xA5;

        var shards = originalShards.Select(shard => shard.ToArray()).ToArray();
        var present = Enumerable.Repeat(element: true, geometry.TotalShardCount).ToArray();

        foreach (var lostShard in lostShards)
        {
            present[lostShard] = false;
            shards[lostShard].AsSpan().Fill(dirtyBufferValue);
        }

        codec.RestoreData(shards, present);

        return Enumerable.Range(0, geometry.DataShardCount)
            .All(shardIndex => shards[shardIndex].SequenceEqual(originalShards[shardIndex]));
    }
}
