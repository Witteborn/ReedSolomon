namespace CompatibilityValidation;

/// <summary>What one caller-visible scenario returned or threw.</summary>
public sealed record BehaviorResult(string Scenario, string Observation);
