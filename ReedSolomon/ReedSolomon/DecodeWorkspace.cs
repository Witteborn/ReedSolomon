using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Witteborn.ReedSolomon;

/// <summary>Reusable decode metadata and at most 128 cached reconstruction plans.</summary>
/// <remarks>Use one workspace per concurrent worker. Payload buffers are not retained after a call.</remarks>
public sealed class DecodeWorkspace
{
    internal readonly byte[][] Inputs;
    internal readonly byte[][] Outputs;
    internal readonly Dictionary<ShardPresenceKey, ReconstructionPlan> Plans = new();

    public DecodeWorkspace(int dataShards, int parityShards)
    {
        var hasInvalidShardCounts = dataShards <= 0 || parityShards <= 0 || dataShards > Galois.FIELD_SIZE - parityShards;

        if (hasInvalidShardCounts)
        {
            throw new ArgumentException(
                $"Shard counts must be positive and total at most {Galois.FIELD_SIZE}, but got {dataShards} data and {parityShards} parity shards.");
        }

        Inputs = new byte[dataShards][];
        Outputs = new byte[parityShards][];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void BindBuffers(byte[][] shards, ReconstructionPlan plan)
    {
        for (var inputIndex = 0; inputIndex < plan.InputShardIndices.Length; inputIndex++)
        {
            Inputs[inputIndex] = shards[plan.InputShardIndices[inputIndex]];
        }

        for (var outputIndex = 0; outputIndex < plan.MissingDataIndices.Length; outputIndex++)
        {
            Outputs[outputIndex] = shards[plan.MissingDataIndices[outputIndex]];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReleaseBuffers()
    {
        Array.Clear(Inputs);
        Array.Clear(Outputs);
    }
}
