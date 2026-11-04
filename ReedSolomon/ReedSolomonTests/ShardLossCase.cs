namespace ReedSolomonTests;

public sealed record ShardLossCase(
    int DataShardCount,
    int ParityShardCount,
    int[] MissingShardIndices,
    bool UseSimd,
    int ShardLength,
    int Seed)
{
    public override string ToString()
    {
        return $"data={DataShardCount}, parity={ParityShardCount}, missing=[{string.Join(",", MissingShardIndices)}], "
            + $"simd={UseSimd}, shardLength={ShardLength}, seed={Seed}";
    }
}
