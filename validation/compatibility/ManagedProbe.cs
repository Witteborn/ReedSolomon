using System.Security.Cryptography;

namespace CompatibilityValidation;

/// <summary>Measures the allocating API: shard layout from version 1.1.0 on, unsigned recovery from version 2.0.0 on.</summary>
internal static class ManagedProbe
{
#if MANAGED_BYTE_API || SIGNED_MANAGED_ENCODE
    private static readonly int[] PayloadLengths = [1, 19, 640, 4097];
#endif

    internal static ManagedResult[] Run()
    {
#if MANAGED_BYTE_API || SIGNED_MANAGED_ENCODE
        var results = new List<ManagedResult>();

        foreach (var geometry in ProbeCases.Geometries())
        {
            foreach (var payloadLength in PayloadLengths)
            {
                results.Add(RunCase(geometry, payloadLength));
            }
        }

        return results.ToArray();
#else
        return [];
#endif
    }

    internal static string[] CreateSampleShards()
    {
#if MANAGED_BYTE_API
        const int payloadLength = 17;

        var geometry = new ShardGeometry(DataShardCount: 4, ParityShardCount: 2);
        var codec = new CodecUnderTest(geometry.DataShardCount, geometry.ParityShardCount);
        var payload = Enumerable.Range(0, payloadLength).Select(value => (byte)value).ToArray();

        return Array.ConvertAll(codec.ManagedEncode(payload), Convert.ToBase64String);
#else
        return [];
#endif
    }

#if MANAGED_BYTE_API || SIGNED_MANAGED_ENCODE
    private static ManagedResult RunCase(ShardGeometry geometry, int payloadLength)
    {
        var codec = new CodecUnderTest(geometry.DataShardCount, geometry.ParityShardCount);
        var payload = Payload.Create(payloadLength, ProbeCases.CreateSeed(geometry, payloadLength));
        var shards = codec.ManagedEncode(payload);
        var shardBytes = shards.SelectMany(shard => shard).ToArray();
        var shardsHash = Convert.ToHexString(SHA256.HashData(shardBytes));
        var lossPatternCount = 0;
        var restoredPatternCount = 0;
        var rejectedPatternCount = 0;

#if MANAGED_BYTE_API
        var paddingSize = shards[0].Length * geometry.DataShardCount - payloadLength;

        foreach (var lostShards in ProbeCases.LossPatterns(geometry))
        {
            lossPatternCount++;

            try
            {
                if (Restores(codec, shards, lostShards, paddingSize, payload))
                {
                    restoredPatternCount++;
                }
            }
            catch (ArgumentException)
            {
                rejectedPatternCount++;
            }
        }
#endif

        var caseName = $"{geometry.DataShardCount}+{geometry.ParityShardCount}, {payloadLength} byte payload";

        return new ManagedResult(caseName, shardsHash, lossPatternCount, restoredPatternCount, rejectedPatternCount);
    }
#endif

#if MANAGED_BYTE_API
    private static bool Restores(
        CodecUnderTest codec,
        byte[][] originalShards,
        int[] lostShards,
        int paddingSize,
        byte[] payload)
    {
        var shards = originalShards.Select(shard => shard.ToArray()).ToArray();

        foreach (var lostShard in lostShards)
        {
            shards[lostShard] = null!;
        }

        var decoded = codec.ManagedDecode(shards, paddingSize);

        return decoded.SequenceEqual(payload);
    }
#endif
}
