namespace PerformanceValidation;

internal sealed record PerformanceResult(
    string Operation,
    int PayloadSizeBytes,
    long BaselineAllocatedBytesPerCall,
    long CurrentAllocatedBytesPerCall,
    double[] BaselineSamplesMilliseconds,
    double[] CurrentSamplesMilliseconds)
{
    public double BaselineMedianMilliseconds
    {
        get
        {
            return BaselineSamplesMilliseconds[BaselineSamplesMilliseconds.Length / 2];
        }
    }
    public double CurrentMedianMilliseconds
    {
        get
        {
            return CurrentSamplesMilliseconds[CurrentSamplesMilliseconds.Length / 2];
        }
    }
    public double Ratio
    {
        get
        {
            return CurrentMedianMilliseconds / BaselineMedianMilliseconds;
        }
    }
}
