using System.Runtime.CompilerServices;

namespace Witteborn.ReedSolomon;

/// <summary>Shards that are ranges of caller memory, possibly slices of larger buffers.</summary>
internal readonly struct MemoryShardBuffers : IShardBuffers
{
    private readonly Memory<byte>[] shards;

    internal MemoryShardBuffers(Memory<byte>[] shards)
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
        return shards[shardIndex].Length;
    }

    public Span<byte> GetShard(int shardIndex)
    {
        return shards[shardIndex].Span;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindSharedShard(int shardIndex, int candidateCount)
    {
        const int noSharedShard = -1;

        var shard = shards[shardIndex].Span;

        for (var candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
        {
            var sharesMemory = candidateIndex != shardIndex && shard.Overlaps(shards[candidateIndex].Span);

            if (sharesMemory)
            {
                return candidateIndex;
            }
        }

        return noSharedShard;
    }
}
