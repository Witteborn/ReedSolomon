using Codec = Witteborn.ReedSolomon.ReedSolomon;

namespace ReedSolomonTests;

/// <summary>The replacement code printed in UPGRADING.md, kept here so that it stays correct.</summary>
internal static class UpgradeGuideSnippets
{
    internal static int GetPaddingSize(int dataLength, int dataShardCount)
    {
        var bytesInLastPartialRow = dataLength % dataShardCount;

        return bytesInLastPartialRow == 0 ? 0 : dataShardCount - bytesInLastPartialRow;
    }

    internal static bool IsParityCorrect(Codec codec, byte[][] shards, int dataShardCount)
    {
        var recalculatedShards = shards
            .Select((shard, shardIndex) => shardIndex < dataShardCount ? shard : new byte[shard.Length])
            .ToArray();

        codec.EncodeParity(recalculatedShards);

        return Enumerable.Range(dataShardCount, shards.Length - dataShardCount)
            .All(shardIndex => recalculatedShards[shardIndex].SequenceEqual(shards[shardIndex]));
    }
}
