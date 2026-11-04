namespace Witteborn.ReedSolomon;

internal readonly record struct ManagedShardInfo(
    int ShardLength,
    List<int> MissingDataIndices,
    List<int> AvailableParityIndices);
