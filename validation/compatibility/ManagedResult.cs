namespace CompatibilityValidation;

/// <summary>Shard layout and recovery of the allocating API for one geometry and payload length.</summary>
public sealed record ManagedResult(
    string Case,
    string ShardsHash,
    int LossPatternCount,
    int RestoredPatternCount,
    int RejectedPatternCount);
