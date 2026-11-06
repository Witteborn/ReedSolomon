using System.Runtime.CompilerServices;

namespace Witteborn.ReedSolomon;

/// <summary>Shards that each occupy a whole array. A null array appears as an empty shard.</summary>
internal readonly struct ArrayShardBuffers : IShardBuffers
{
    private readonly byte[]?[] shards;

    internal ArrayShardBuffers(byte[]?[] shards)
    {
        this.shards = shards;
    }

    public int Count
    {
        get
        {
            return shards.Length;
        }
    }

    public int GetShardLength(int shardIndex)
    {
        return shards[shardIndex]?.Length ?? 0;
    }

    public Span<byte> GetShard(int shardIndex)
    {
        return shards[shardIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindSharedShard(int shardIndex, int candidateCount)
    {
        const int noSharedShard = -1;

        var shard = shards[shardIndex];

        // Whole arrays of one validated length share memory exactly when they are the same array.
        for (var candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
        {
            var sharesMemory = candidateIndex != shardIndex && ReferenceEquals(shard, shards[candidateIndex]);

            if (sharesMemory)
            {
                return candidateIndex;
            }
        }

        return noSharedShard;
    }
}
