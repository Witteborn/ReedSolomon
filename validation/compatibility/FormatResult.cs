namespace CompatibilityValidation;

/// <summary>Parity bytes and buffer-level recovery for one geometry and shard length.</summary>
public sealed record FormatResult(
    string Case,
    string ParityHash,
    int LossPatternCount,
    int RestoredPatternCount);
