namespace Witteborn.ReedSolomon;

/// <summary>Gives validation and coding uniform access to caller shards stored as arrays or memory ranges.</summary>
/// <remarks>
/// Implementations are structs passed as generic arguments, so the JIT compiles a separate copy of the
/// calling code for each storage kind without interface calls in the hot path.
/// </remarks>
internal interface IShardBuffers
{
    int Count
    {
        get;
    }

    /// <summary>Returns the shard length, or 0 for a missing shard.</summary>
    int GetShardLength(int shardIndex);

    Span<byte> GetShard(int shardIndex);

    /// <summary>Returns the first other shard below <paramref name="candidateCount"/> that shares memory, or -1.</summary>
    /// <remarks>Callers validate first that all shards are present and equally long.</remarks>
    int FindSharedShard(int shardIndex, int candidateCount);
}
