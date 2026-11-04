namespace CompatibilityValidation;

/// <summary>Everything one codec version produced, compared later against the other versions.</summary>
public sealed record CompatibilityReport(
    string CodecVersion,
    string Runtime,
    string CodecAssemblySha256,
    bool JitOptimizerDisabled,
    string[] SampleShardsBase64,
    FormatResult[] Formats,
    ManagedResult[] ManagedCases,
    BehaviorResult[] Behaviors);
