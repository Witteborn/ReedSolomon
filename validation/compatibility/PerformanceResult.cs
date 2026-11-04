namespace CompatibilityValidation;

/// <summary>The median of one fresh process. Runs of several processes form the published comparison.</summary>
public sealed record PerformanceResult(
    string CodecVersion,
    string Operation,
    int PayloadBytes,
    double MedianMilliseconds,
    long AllocatedBytesPerOperation,
    int Batches,
    int OperationsPerBatch);
