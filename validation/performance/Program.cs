using System.Diagnostics;
using System.Reflection;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using PerformanceValidation;
using ReferenceDecodeWorkspace = ReferenceImplementation.DecodeWorkspace;
using ReferenceCodec = ReferenceImplementation.ReedSolomon;
using LibraryDecodeWorkspace = Witteborn.ReedSolomon.DecodeWorkspace;
using LibraryCodec = Witteborn.ReedSolomon.ReedSolomon;

const int MediumPayloadSizeMebibytes = 16;
const int BytesPerKibibyte = 1024;
const int SimdBlockAlignmentBytes = 64;
const int SmallPayloadSizeBytes = 64 * BytesPerKibibyte;
const int LargePayloadSizeMebibytes = 420;
const int RandomSeed = 1712;
const int LargePayloadThresholdMebibytes = 64;
const int LargeEncodeBlockSizeBytes = 32768;
const int DefaultBlockSizeBytes = 8192;
const int LargeDecodeBlockSizeBytes = 131072;
const int DataShardCount = 10;
const int ParityShardCount = 2;
const int TotalShardCount = DataShardCount + ParityShardCount;
const int LastDataShardIndex = DataShardCount - 1;
const int SingleVectorUnrollFactor = 1;
const int FourVectorUnrollFactor = 4;
const byte FirstDirtyBufferValue = 0xA5;
const byte SecondDirtyBufferValue = 0x5A;

if (OperatingSystem.IsWindows())
{
    var benchmarkProcess = Process.GetCurrentProcess();
    var availableProcessors = benchmarkProcess.ProcessorAffinity.ToInt64();
    var firstAvailableProcessor = availableProcessors & -availableProcessors;

    benchmarkProcess.ProcessorAffinity = (nint)firstAvailableProcessor;
}

var libraryDebugFlags = typeof(LibraryCodec).Assembly.GetCustomAttribute<DebuggableAttribute>();

if (libraryDebugFlags?.IsJITOptimizerDisabled == true)
{
    throw new Exception("Library disables JIT optimization.");
}

Console.WriteLine(JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    avx2 = Avx2.IsSupported,
    control = args.Contains("--control"),
    jit_optimizer_disabled = libraryDebugFlags?.IsJITOptimizerDisabled ?? false
}));

foreach (var payloadSizeBytes in new[]
{
    DataShardCount * SimdBlockAlignmentBytes,
    SmallPayloadSizeBytes,
    MediumPayloadSizeMebibytes * BytesPerKibibyte * BytesPerKibibyte,
    LargePayloadSizeMebibytes * BytesPerKibibyte * BytesPerKibibyte
})
{
    var input = new byte[payloadSizeBytes];

    var random = new Random(RandomSeed);

    random.NextBytes(input);

    var useLargeBlocks = payloadSizeBytes > LargePayloadThresholdMebibytes * BytesPerKibibyte * BytesPerKibibyte;
    var encodeBlockSize = useLargeBlocks ? LargeEncodeBlockSizeBytes : DefaultBlockSizeBytes;
    var decodeBlockSize = useLargeBlocks ? LargeDecodeBlockSizeBytes : DefaultBlockSizeBytes;
    var unrollFactor = useLargeBlocks ? FourVectorUnrollFactor : SingleVectorUnrollFactor;
    var referenceEncoder = new ReferenceCodec(DataShardCount, ParityShardCount, simd: true, encodeBlockSize, unrollFactor);
    var libraryEncoder = new LibraryCodec(DataShardCount, ParityShardCount, simd: true, encodeBlockSize, unrollFactor);
    var referenceDecoder = new ReferenceCodec(DataShardCount, ParityShardCount, simd: true, decodeBlockSize, unrollFactor);
    var libraryDecoder = new LibraryCodec(DataShardCount, ParityShardCount, simd: true, decodeBlockSize, unrollFactor);
    var referenceShards = referenceEncoder.ManagedEncode(input);
    var libraryShards = libraryEncoder.ManagedEncode(input);

    for (var shardIndex = 0; shardIndex < TotalShardCount; shardIndex++)
    {
        if (referenceShards[shardIndex].AsSpan().SequenceEqual(libraryShards[shardIndex]) == false)
        {
            throw new Exception("Encoded shards differ from the reference implementation.");
        }
    }

    var shardPresence = Enumerable.Range(0, TotalShardCount)
        .Select(shardIndex => shardIndex != 0 && shardIndex != LastDataShardIndex)
        .ToArray();
    var referenceWorkspace = new ReferenceDecodeWorkspace(DataShardCount, ParityShardCount);
    var libraryWorkspace = new LibraryDecodeWorkspace(DataShardCount, ParityShardCount);

    libraryShards[0].AsSpan().Fill(FirstDirtyBufferValue);
    libraryShards[LastDataShardIndex].AsSpan().Fill(SecondDirtyBufferValue);
    libraryDecoder.ReconstructDataInto(libraryShards, shardPresence, libraryWorkspace);

    for (var shardIndex = 0; shardIndex < DataShardCount; shardIndex++)
    {
        if (referenceShards[shardIndex].AsSpan().SequenceEqual(libraryShards[shardIndex]) == false)
        {
            throw new Exception("Library reconstruction failed.");
        }
    }

    // Both implementations use the same buffers so memory alignment cannot favor either one.
    var referenceEncode = () => referenceEncoder.EncodeParity(libraryShards);
    var libraryEncode = () => libraryEncoder.EncodeParity(libraryShards);
    var referenceDecode = () => referenceDecoder.ReconstructDataInto(libraryShards, shardPresence, referenceWorkspace);
    var libraryDecode = () => libraryDecoder.ReconstructDataInto(libraryShards, shardPresence, libraryWorkspace);

    if (args.Contains("--control"))
    {
        libraryEncode = referenceEncode;
        libraryDecode = referenceDecode;
    }

    Console.WriteLine(JsonSerializer.Serialize(PerformanceMeasurement.Compare(
        "EncodeParity", payloadSizeBytes, referenceEncode, libraryEncode)));
    Console.WriteLine(JsonSerializer.Serialize(PerformanceMeasurement.Compare(
        "ReconstructDataInto", payloadSizeBytes, referenceDecode, libraryDecode)));

    for (var shardIndex = 0; shardIndex < TotalShardCount; shardIndex++)
    {
        if (referenceShards[shardIndex].AsSpan().SequenceEqual(libraryShards[shardIndex]) == false)
        {
            throw new Exception("Payload changed during measurement.");
        }
    }
}

Console.WriteLine("Reference implementation comparison and reconstruction passed.");
