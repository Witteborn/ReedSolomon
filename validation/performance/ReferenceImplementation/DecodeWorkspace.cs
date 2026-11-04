using System;
using System.Collections.Generic;

namespace ReferenceImplementation;

public sealed class DecodeWorkspace
{
    internal readonly byte[][] Inputs;
    internal readonly byte[][] Outputs;
    internal readonly Dictionary<ShardPresenceMask, ReconstructionPlan> Plans = new();

    public DecodeWorkspace(int dataShards, int parityShards)
    {
        var hasInvalidShardCounts = dataShards <= 0 || parityShards <= 0;

        if (hasInvalidShardCounts)
        {
            throw new ArgumentException("invalid workspace");
        }

        Inputs = new byte[dataShards][];
        Outputs = new byte[parityShards][];
    }
}
