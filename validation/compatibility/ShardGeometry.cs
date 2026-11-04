namespace CompatibilityValidation;

internal readonly record struct ShardGeometry(int DataShardCount, int ParityShardCount)
{
    internal int TotalShardCount
    {
        get
        {
            return DataShardCount + ParityShardCount;
        }
    }
}
