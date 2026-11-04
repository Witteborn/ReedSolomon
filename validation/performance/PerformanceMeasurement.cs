using System.Diagnostics;

namespace PerformanceValidation;

internal static class PerformanceMeasurement
{
    internal static PerformanceResult Compare(string operation, int payloadSizeBytes, Action baseline, Action current)
    {
        const int sampleCount = 41;

        WarmUp(baseline, current);

        var baselineAllocatedBytes = MeasureAllocations(baseline);
        var currentAllocatedBytes = MeasureAllocations(current);

        if (baselineAllocatedBytes != currentAllocatedBytes)
        {
            throw new InvalidOperationException($"Allocation counts differ for {operation}.");
        }

        var batchSize = ChooseBatchSize(baseline);
        var baselineSamples = new double[sampleCount];
        var currentSamples = new double[sampleCount];

        for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            if (sampleIndex % 2 == 0)
            {
                baselineSamples[sampleIndex] = MeasureBatch(baseline, batchSize);
                currentSamples[sampleIndex] = MeasureBatch(current, batchSize);
            }
            else
            {
                currentSamples[sampleIndex] = MeasureBatch(current, batchSize);
                baselineSamples[sampleIndex] = MeasureBatch(baseline, batchSize);
            }
        }

        Array.Sort(baselineSamples);
        Array.Sort(currentSamples);

        return new PerformanceResult(
            operation, payloadSizeBytes, baselineAllocatedBytes, currentAllocatedBytes, baselineSamples, currentSamples);
    }

    private static void WarmUp(Action baseline, Action current)
    {
        const int minimumWarmupMilliseconds = 1000;
        const int minimumWarmupOperations = 16;

        var stopwatch = Stopwatch.StartNew();
        var operationCount = 0;

        do
        {
            baseline();
            current();
            operationCount++;
        }
        while (stopwatch.ElapsedMilliseconds < minimumWarmupMilliseconds || operationCount < minimumWarmupOperations);
    }

    private static long MeasureAllocations(Action operation)
    {
        const int allocationMeasurementCalls = 16;

        var allocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < allocationMeasurementCalls; iteration++)
        {
            operation();
        }

        return (GC.GetAllocatedBytesForCurrentThread() - allocatedBytesBefore) / allocationMeasurementCalls;
    }

    private static int ChooseBatchSize(Action operation)
    {
        const double minimumOperationMilliseconds = 0.000001;
        const double targetBatchMilliseconds = 0.5;
        const int maximumBatchSize = 1024;

        var stopwatch = Stopwatch.StartNew();

        operation();

        var operationMilliseconds = Math.Max(minimumOperationMilliseconds, stopwatch.Elapsed.TotalMilliseconds);

        return Math.Clamp((int)(targetBatchMilliseconds / operationMilliseconds), 1, maximumBatchSize);
    }

    private static double MeasureBatch(Action operation, int batchSize)
    {
        const int minimumSampleMilliseconds = 10;

        var stopwatch = Stopwatch.StartNew();
        var operationCount = 0;

        do
        {
            for (var iteration = 0; iteration < batchSize; iteration++)
            {
                operation();
            }

            operationCount += batchSize;
        }
        while (stopwatch.ElapsedMilliseconds < minimumSampleMilliseconds);

        return stopwatch.Elapsed.TotalMilliseconds / operationCount;
    }
}
