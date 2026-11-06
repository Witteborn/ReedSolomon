using System;
using System.Collections.Generic;

namespace Witteborn.ReedSolomon;

/// <summary>Reusable decode metadata and at most 128 cached reconstruction plans.</summary>
/// <remarks>Use one workspace per concurrent worker. The workspace never holds references to payload buffers.</remarks>
public sealed class DecodeWorkspace
{
    internal readonly int DataShardCount;
    internal readonly int ParityShardCount;
    internal readonly Dictionary<ShardPresenceKey, ReconstructionPlan> Plans = new();

    public DecodeWorkspace(int dataShards, int parityShards)
    {
        var hasInvalidShardCounts = dataShards <= 0 || parityShards <= 0 || dataShards > Galois.FIELD_SIZE - parityShards;

        if (hasInvalidShardCounts)
        {
            throw new ArgumentException(
                $"Shard counts must be positive and total at most {Galois.FIELD_SIZE}, but got {dataShards} data and {parityShards} parity shards.");
        }

        DataShardCount = dataShards;
        ParityShardCount = parityShards;
    }
}
