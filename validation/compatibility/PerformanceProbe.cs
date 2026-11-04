using System.Diagnostics;

namespace CompatibilityValidation;

/// <summary>Times one operation of one codec version on one logical processor in this process.</summary>
/// <remarks>
/// "managed" operations return freshly allocated results; "reuse" operations keep the caller's buffers.
/// Decoding always restores the first and the last data shard. Results are verified outside the timed batches.
/// </remarks>
internal sealed class PerformanceProbe
{
    private const int DataShardCount = 10;
    private const int ParityShardCount = 2;
    private const int TotalShardCount = DataShardCount + ParityShardCount;
    private const int LastDataShardIndex = DataShardCount - 1;
    private const int BatchCount = 5;
    private const uint PayloadSeed = 1712;
    private static readonly TimeSpan MinimumWarmup = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MinimumBatch = TimeSpan.FromMilliseconds(200);

    private readonly CodecUnderTest codec = new(DataShardCount, ParityShardCount);
    private readonly byte[] payload;
    private readonly int shardLength;
    private readonly byte[][] encodedShards;
    private readonly byte[][] workingShards;
    private readonly bool[] present = Enumerable.Repeat(element: true, TotalShardCount).ToArray();

    private PerformanceProbe(int payloadBytes)
    {
        payload = Payload.Create(payloadBytes, PayloadSeed);
        shardLength = (payloadBytes - 1) / DataShardCount + 1;
        encodedShards = CreateEncodedShards();
        workingShards = encodedShards.Select(shard => shard.ToArray()).ToArray();
        present[0] = false;
        present[LastDataShardIndex] = false;
    }

    internal static PerformanceResult Run(string codecVersion, string operationName, int payloadBytes)
    {
        PinToOneProcessor();

        var probe = new PerformanceProbe(payloadBytes);
        var (operation, verification) = probe.SelectOperation(operationName);
        var result = probe.Measure(codecVersion, operationName, payloadBytes, operation);

        verification();

        return result;
    }

    private static void PinToOneProcessor()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            var currentProcess = Process.GetCurrentProcess();
            var availableProcessors = currentProcess.ProcessorAffinity.ToInt64();
            var firstAvailableProcessor = availableProcessors & -availableProcessors;

            currentProcess.ProcessorAffinity = (nint)firstAvailableProcessor;
        }
    }

    private byte[][] CreateEncodedShards()
    {
        var shards = new byte[TotalShardCount][];

        for (var shardIndex = 0; shardIndex < TotalShardCount; shardIndex++)
        {
            shards[shardIndex] = new byte[shardLength];
        }

        for (var dataShardIndex = 0; dataShardIndex < DataShardCount; dataShardIndex++)
        {
            var blockStart = dataShardIndex * shardLength;
            var bytesToCopy = Math.Min(shardLength, Math.Max(0, payload.Length - blockStart));

            payload.AsSpan(Math.Min(blockStart, payload.Length), bytesToCopy).CopyTo(shards[dataShardIndex]);
        }

        codec.EncodeParity(shards);

        return shards;
    }

    private (Action Operation, Action Verification) SelectOperation(string operationName)
    {
        return operationName switch
        {
            "managed-encode" => (ManagedEncode, VerifyManagedEncode),
            "managed-decode2" => (ManagedDecode, VerifyManagedDecode),
            "reuse-encode" => (ReuseEncode, VerifyReusedBuffers),
            "reuse-decode2" => (ReuseDecode, VerifyReusedBuffers),
            _ => throw new ArgumentException($"Unknown operation '{operationName}'.", nameof(operationName))
        };
    }

    private void ManagedEncode()
    {
#if MANAGED_BYTE_API
        codec.ManagedEncode(payload);
#else
        throw new NotSupportedException("This published version has no unsigned allocating API.");
#endif
    }

    private void ManagedDecode()
    {
#if MANAGED_BYTE_API
        var shards = (byte[][])encodedShards.Clone();
        var paddingSize = shardLength * DataShardCount - payload.Length;

        shards[0] = null!;
        shards[LastDataShardIndex] = null!;
        codec.ManagedDecode(shards, paddingSize);
#else
        throw new NotSupportedException("This published version has no unsigned allocating API.");
#endif
    }

    private void ReuseEncode()
    {
        codec.EncodeParity(workingShards);
    }

    private void ReuseDecode()
    {
        codec.RestoreData(workingShards, present);
    }

    private PerformanceResult Measure(string codecVersion, string operationName, int payloadBytes, Action operation)
    {
        var warmupClock = Stopwatch.StartNew();

        do
        {
            operation();
        }
        while (warmupClock.Elapsed < MinimumWarmup);

        var operationsPerBatch = CountOperationsForMinimumBatch(operation);
        var batchMilliseconds = new double[BatchCount];
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var totalOperations = 0L;

        for (var batchIndex = 0; batchIndex < BatchCount; batchIndex++)
        {
            batchMilliseconds[batchIndex] = TimeBatch(operation, operationsPerBatch);
            totalOperations += operationsPerBatch;
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Array.Sort(batchMilliseconds);

        return new PerformanceResult(
            codecVersion,
            operationName,
            payloadBytes,
            batchMilliseconds[BatchCount / 2],
            allocatedBytes / totalOperations,
            BatchCount,
            operationsPerBatch);
    }

    private static int CountOperationsForMinimumBatch(Action operation)
    {
        var operations = 1;

        while (true)
        {
            var clock = Stopwatch.StartNew();

            for (var operationIndex = 0; operationIndex < operations; operationIndex++)
            {
                operation();
            }

            if (clock.Elapsed >= MinimumBatch)
            {
                return operations;
            }

            operations *= 2;
        }
    }

    private static double TimeBatch(Action operation, int operations)
    {
        var clock = Stopwatch.StartNew();

        for (var operationIndex = 0; operationIndex < operations; operationIndex++)
        {
            operation();
        }

        return clock.Elapsed.TotalMilliseconds / operations;
    }

    private void VerifyManagedEncode()
    {
#if MANAGED_BYTE_API
        var encoded = codec.ManagedEncode(payload);
        var isVerified = encoded.Zip(encodedShards).All(pair => pair.First.SequenceEqual(pair.Second));

        ThrowIfNotVerified(isVerified);
#endif
    }

    private void VerifyManagedDecode()
    {
#if MANAGED_BYTE_API
        var shards = (byte[][])encodedShards.Clone();

        shards[0] = null!;
        shards[LastDataShardIndex] = null!;

        var decoded = codec.ManagedDecode(shards, shardLength * DataShardCount - payload.Length);

        ThrowIfNotVerified(decoded.SequenceEqual(payload));
#endif
    }

    private void VerifyReusedBuffers()
    {
        var isVerified = workingShards.Zip(encodedShards).All(pair => pair.First.SequenceEqual(pair.Second));

        ThrowIfNotVerified(isVerified);
    }

    private static void ThrowIfNotVerified(bool isVerified)
    {
        if (isVerified == false)
        {
            throw new InvalidOperationException("The measured operation produced unexpected bytes.");
        }
    }
}
