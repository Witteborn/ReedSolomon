using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;
using Witteborn.ReedSolomon;
using Codec = Witteborn.ReedSolomon.ReedSolomon;

namespace ReedSolomonTests;

[TestClass]
public sealed class MemoryRangeTests
{
    private const int StandardDataShardCount = 10;
    private const int StandardParityShardCount = 2;
    private const int StandardShardCount = StandardDataShardCount + StandardParityShardCount;
    private const int SmallShardLength = 64;
    private const int Avx2VectorSizeBytes = 32;
    private const int DefaultBlockSizeBytes = 8192;
    private const int RandomSeed = 1712;

    // An odd guard size places every packed range at an offset that is not aligned to any vector width.
    private const int GuardSizeBytes = 3;
    private const byte GuardValue = 0xC3;
    private const byte DirtyBufferValue = 0xA5;

    [TestMethod]
    [DynamicData(nameof(ShardLossCases.ExhaustiveLossCases), typeof(ShardLossCases))]
    public void MemoryOverloads_MatchArrayResults_WhenEverySmallCombinationIsTested(ShardLossCase testCase)
    {
        AssertPackedRangeRecovery(testCase);
    }

    [TestMethod]
    [DynamicData(nameof(ShardLossCases.RandomLossCases), typeof(ShardLossCases))]
    public void MemoryOverloads_MatchArrayResults_WhenSeededRandomCombinationsAreTested(ShardLossCase testCase)
    {
        AssertPackedRangeRecovery(testCase);
    }

    [TestMethod]
    [CombinatorialData]
    public void MemoryOverloads_MatchArrayResults_WhenRangesStartAtUnalignedOffsets(
        bool useSimd,
        [CombinatorialValues(1, 4)] int unrollFactor,
        [CombinatorialValues(StandardParityShardCount, 4)] int parityShardCount,
        [CombinatorialValues(0, 1, 7, Avx2VectorSizeBytes - 1)] int rangeOffset,
        [CombinatorialValues(1, Avx2VectorSizeBytes - 1, Avx2VectorSizeBytes + 1, DefaultBlockSizeBytes + 1)] int shardLength)
    {
        const int trailingGuardSizeBytes = 5;

        var shardCount = StandardDataShardCount + parityShardCount;
        var codec = new Codec(StandardDataShardCount, parityShardCount, useSimd, unroll: unrollFactor);
        var data = CreateRandomBytes(StandardDataShardCount * shardLength, RandomSeed + shardLength);
        var expectedShards = codec.ManagedEncode(data);
        var buffers = Enumerable.Range(0, shardCount)
            .Select(_ => Enumerable.Repeat(GuardValue, rangeOffset + shardLength + trailingGuardSizeBytes).ToArray())
            .ToArray();
        var ranges = buffers
            .Select(buffer => buffer.AsMemory(rangeOffset, shardLength))
            .ToArray();

        CopyDataShards(expectedShards, ranges, StandardDataShardCount);
        codec.EncodeParity(ranges);

        AssertRangesEqual(expectedShards, ranges, "after encoding");

        var shardPresence = Enumerable.Repeat(element: true, shardCount).ToArray();

        shardPresence[0] = false;
        shardPresence[StandardDataShardCount - 1] = false;
        ranges[0].Span.Fill(DirtyBufferValue);
        ranges[StandardDataShardCount - 1].Span.Fill(DirtyBufferValue);

        codec.ReconstructDataInto(ranges, shardPresence, new DecodeWorkspace(StandardDataShardCount, parityShardCount));

        AssertRangesEqual(expectedShards, ranges, "after reconstruction");

        foreach (var buffer in buffers)
        {
            var guardBytes = buffer.Take(rangeOffset).Concat(buffer.Skip(rangeOffset + shardLength));

            Assert.IsTrue(guardBytes.All(value => value == GuardValue), "Bytes outside the ranges must stay unchanged.");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MemoryOverloads_MatchManagedEncode_WhenOneBufferSetServesChunksOfDifferentLengths(bool useSimd)
    {
        const int headerSizeBytes = 1;

        var chunkLengths = new[] { 20_000, 17, 8_193, 1, 65_537, 300 };
        var maximumShardLength = chunkLengths.Max() / StandardDataShardCount + 1;
        var codec = new Codec(StandardDataShardCount, StandardParityShardCount, useSimd);
        var workspace = new DecodeWorkspace(StandardDataShardCount, StandardParityShardCount);

        // Each stored shard starts with a header byte, so the codec writes behind it.
        var storedShards = Enumerable.Range(0, StandardShardCount)
            .Select(_ => Enumerable.Repeat(DirtyBufferValue, headerSizeBytes + maximumShardLength).ToArray())
            .ToArray();

        for (var chunkIndex = 0; chunkIndex < chunkLengths.Length; chunkIndex++)
        {
            var chunk = CreateRandomBytes(chunkLengths[chunkIndex], RandomSeed + chunkIndex);
            var expectedShards = codec.ManagedEncode(chunk);
            var shardLength = expectedShards[0].Length;
            var ranges = storedShards
                .Select(storedShard => storedShard.AsMemory(headerSizeBytes, shardLength))
                .ToArray();

            // Data ranges still hold bytes of the previous chunk, so the padding is cleared like ManagedEncode does.
            for (var dataShardIndex = 0; dataShardIndex < StandardDataShardCount; dataShardIndex++)
            {
                var chunkOffset = Math.Min(chunk.Length, dataShardIndex * shardLength);
                var chunkBytes = chunk.AsSpan(chunkOffset, Math.Min(shardLength, chunk.Length - chunkOffset));
                var dataRange = ranges[dataShardIndex].Span;

                dataRange.Clear();
                chunkBytes.CopyTo(dataRange);
            }

            codec.EncodeParity(ranges);

            AssertRangesEqual(expectedShards, ranges, $"chunk {chunkIndex} after encoding");

            var shardPresence = Enumerable.Range(0, StandardShardCount)
                .Select(shardIndex => shardIndex != 1 && shardIndex != StandardDataShardCount - 2)
                .ToArray();

            ranges[1].Span.Fill(DirtyBufferValue);
            ranges[StandardDataShardCount - 2].Span.Fill(DirtyBufferValue);
            codec.ReconstructDataInto(ranges, shardPresence, workspace);

            AssertRangesEqual(expectedShards, ranges, $"chunk {chunkIndex} after reconstruction");
            Assert.IsTrue(
                storedShards.All(storedShard => storedShard[0] == DirtyBufferValue),
                $"Header bytes must stay unchanged. Chunk {chunkIndex}.");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MemoryOverloads_DoNotAllocate_WhenWarm(bool useSimd)
    {
        const int warmCallCount = 64;

        var codec = new Codec(StandardDataShardCount, StandardParityShardCount, useSimd);
        var workspace = new DecodeWorkspace(StandardDataShardCount, StandardParityShardCount);
        var buffer = CreateGuardedBuffer(StandardShardCount, DefaultBlockSizeBytes + 1);
        var ranges = CreatePackedRanges(buffer, StandardShardCount, DefaultBlockSizeBytes + 1);
        var shardPresence = Enumerable.Range(0, StandardShardCount)
            .Select(shardIndex => shardIndex != 0 && shardIndex != StandardDataShardCount - 1)
            .ToArray();

        for (var callIndex = 0; callIndex < warmCallCount; callIndex++)
        {
            codec.EncodeParity(ranges);
            codec.ReconstructDataInto(ranges, shardPresence, workspace);
        }

        var allocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();

        for (var callIndex = 0; callIndex < warmCallCount; callIndex++)
        {
            codec.EncodeParity(ranges);
            codec.ReconstructDataInto(ranges, shardPresence, workspace);
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBytesBefore;

        Assert.AreEqual(0L, allocatedBytes, "Warm calls with memory ranges must not allocate.");
    }

    [TestMethod]
    public void EncodeParity_AcceptsRanges_WhenTheyAreAdjacentInOneBuffer()
    {
        var codec = new Codec(StandardDataShardCount, StandardParityShardCount);
        var data = CreateRandomBytes(StandardDataShardCount * SmallShardLength, RandomSeed);
        var expectedShards = codec.ManagedEncode(data);
        var buffer = new byte[StandardShardCount * SmallShardLength];
        var ranges = Enumerable.Range(0, StandardShardCount)
            .Select(shardIndex => buffer.AsMemory(shardIndex * SmallShardLength, SmallShardLength))
            .ToArray();

        CopyDataShards(expectedShards, ranges, StandardDataShardCount);
        codec.EncodeParity(ranges);

        AssertRangesEqual(expectedShards, ranges, "adjacent ranges");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MemoryOverloads_MatchArrayResults_WhenRangesLieOutsideTheManagedHeap(bool useSimd)
    {
        const int shardLength = DefaultBlockSizeBytes + 1;

        var codec = new Codec(StandardDataShardCount, StandardParityShardCount, useSimd);
        var data = CreateRandomBytes(StandardDataShardCount * shardLength, RandomSeed);
        var expectedShards = codec.ManagedEncode(data);
        var bufferLength = CreateGuardedBuffer(StandardShardCount, shardLength).Length;

        using var nativeMemory = new NativeMemoryManager(bufferLength);

        var buffer = nativeMemory.Memory;

        buffer.Span.Fill(GuardValue);

        var ranges = CreatePackedRanges(buffer, StandardShardCount, shardLength);

        CopyDataShards(expectedShards, ranges, StandardDataShardCount);
        codec.EncodeParity(ranges);

        AssertRangesEqual(expectedShards, ranges, "after encoding native memory");
        AssertGuardsUnchanged(buffer.Span, StandardShardCount, shardLength, "after encoding native memory");

        var shardPresence = Enumerable.Range(0, StandardShardCount)
            .Select(shardIndex => shardIndex != 0 && shardIndex != StandardDataShardCount - 1)
            .ToArray();

        ranges[0].Span.Fill(DirtyBufferValue);
        ranges[StandardDataShardCount - 1].Span.Fill(DirtyBufferValue);
        codec.ReconstructDataInto(ranges, shardPresence, new DecodeWorkspace(StandardDataShardCount, StandardParityShardCount));

        AssertRangesEqual(expectedShards, ranges, "after reconstructing native memory");
        AssertGuardsUnchanged(buffer.Span, StandardShardCount, shardLength, "after reconstructing native memory");
    }

    [TestMethod]
    public void EncodeParity_NamesBothShards_WhenNativeRangesOverlap()
    {
        const int dataShardIndex = 4;

        var codec = new Codec(StandardDataShardCount, StandardParityShardCount);
        var ranges = CreateSeparateRanges(StandardShardCount, SmallShardLength);

        using var nativeMemory = new NativeMemoryManager(2 * SmallShardLength);

        ranges[dataShardIndex] = nativeMemory.Memory.Slice(0, SmallShardLength);
        ranges[StandardShardCount - 1] = nativeMemory.Memory.Slice(SmallShardLength - 1, SmallShardLength);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => codec.EncodeParity(ranges));

        StringAssert.Contains(
            exception.Message,
            $"Writable shard {StandardShardCount - 1} must not share its buffer with shard {dataShardIndex}.");
    }

    [TestMethod]
    [DataRow(5, DisplayName = "Data shard read by the codec")]
    [DataRow(StandardShardCount - 1, DisplayName = "Parity shard written by the codec")]
    public void EncodeParity_ThrowsWithoutTouchingOtherMemory_WhenCallerReplacesShardDuringCall(int replacedShardIndex)
    {
        var codec = new Codec(StandardDataShardCount, StandardParityShardCount);
        var ranges = CreateSeparateRanges(StandardShardCount, SmallShardLength);

        // The replacement is one byte long but lies at the start of a larger buffer. Without the
        // length check the codec would read or write the guard bytes behind it.
        var foreignBuffer = Enumerable.Repeat(GuardValue, 4 * SmallShardLength).ToArray();

        using var tamperingMemory = new TamperingMemoryManager(
            new byte[SmallShardLength],
            () => ranges[replacedShardIndex] = foreignBuffer.AsMemory(0, 1));

        ranges[0] = tamperingMemory.UnreadMemory;

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => codec.EncodeParity(ranges));

        StringAssert.Contains(exception.Message, $"Shard {replacedShardIndex} changed during the call.");
        StringAssert.Contains(exception.Message, $"validated with {SmallShardLength} bytes but now has 1 bytes");
        Assert.IsTrue(foreignBuffer.All(value => value == GuardValue), "Memory outside the replaced shard must stay unchanged.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReconstructDataInto_ThrowsWithoutTouchingOtherMemory_WhenCallerReplacesTargetDuringCall(bool useSimd)
    {
        const int missingShardIndex = 2;

        var codec = new Codec(StandardDataShardCount, StandardParityShardCount, useSimd);
        var ranges = CreateSeparateRanges(StandardShardCount, SmallShardLength);
        var foreignBuffer = Enumerable.Repeat(GuardValue, 4 * SmallShardLength).ToArray();
        var shardPresence = Enumerable.Range(0, StandardShardCount)
            .Select(shardIndex => shardIndex != missingShardIndex)
            .ToArray();

        using var tamperingMemory = new TamperingMemoryManager(
            new byte[SmallShardLength],
            () => ranges[missingShardIndex] = foreignBuffer.AsMemory(0, 1));

        ranges[0] = tamperingMemory.UnreadMemory;

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => codec.ReconstructDataInto(
            ranges,
            shardPresence,
            new DecodeWorkspace(StandardDataShardCount, StandardParityShardCount)));

        StringAssert.Contains(exception.Message, $"Shard {missingShardIndex} changed during the call.");
        Assert.IsTrue(foreignBuffer.All(value => value == GuardValue), "Memory outside the replaced shard must stay unchanged.");
    }

    [TestMethod]
    public void MemoryOverloads_ThrowArgumentNullException_WhenArgumentsAreNull()
    {
        var codec = new Codec(StandardDataShardCount, StandardParityShardCount);
        var ranges = CreateSeparateRanges(StandardShardCount, SmallShardLength);
        var shardPresence = Enumerable.Repeat(element: true, StandardShardCount).ToArray();
        var workspace = new DecodeWorkspace(StandardDataShardCount, StandardParityShardCount);

        Assert.ThrowsExactly<ArgumentNullException>(() => codec.EncodeParity((Memory<byte>[])null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => codec.ReconstructDataInto((Memory<byte>[])null!, shardPresence, workspace));
        Assert.ThrowsExactly<ArgumentNullException>(() => codec.ReconstructDataInto(ranges, shardPresence, null!));
    }

    [TestMethod]
    [DataRow(StandardShardCount - 1)]
    [DataRow(StandardShardCount + 1)]
    public void MemoryOverloads_NameExpectedAndReceivedCounts_WhenRangeCountIsWrong(int rangeCount)
    {
        var codec = new Codec(StandardDataShardCount, StandardParityShardCount);
        var ranges = CreateSeparateRanges(rangeCount, SmallShardLength);
        var shardPresence = Enumerable.Repeat(element: true, rangeCount).ToArray();
        var workspace = new DecodeWorkspace(StandardDataShardCount, StandardParityShardCount);
        var expectedMessage = $"Expected {StandardShardCount} shards ({StandardDataShardCount} data and {StandardParityShardCount} parity), but got {rangeCount}.";

        var encodeException = Assert.ThrowsExactly<ArgumentException>(() => codec.EncodeParity(ranges));
        var reconstructException = Assert.ThrowsExactly<ArgumentException>(
            () => codec.ReconstructDataInto(ranges, shardPresence, workspace));

        StringAssert.Contains(encodeException.Message, expectedMessage);
        StringAssert.Contains(reconstructException.Message, expectedMessage);
    }

    [TestMethod]
    [DataRow(1, SmallShardLength - 1)]
    [DataRow(5, SmallShardLength + 1)]
    [DataRow(StandardShardCount - 1, 0)]
    public void MemoryOverloads_NameShardAndLengths_WhenRangeLengthDiffers(int shardIndex, int rangeLength)
    {
        var codec = new Codec(StandardDataShardCount, StandardParityShardCount);
        var ranges = CreateSeparateRanges(StandardShardCount, SmallShardLength);
        var shardPresence = Enumerable.Repeat(element: true, StandardShardCount).ToArray();
        var workspace = new DecodeWorkspace(StandardDataShardCount, StandardParityShardCount);
        var expectedMessage = $"Shard {shardIndex} must exist and have {SmallShardLength} bytes like shard 0, but has {rangeLength} bytes.";

        ranges[shardIndex] = new byte[rangeLength];

        var encodeException = Assert.ThrowsExactly<ArgumentException>(() => codec.EncodeParity(ranges));
        var reconstructException = Assert.ThrowsExactly<ArgumentException>(
            () => codec.ReconstructDataInto(ranges, shardPresence, workspace));

        StringAssert.Contains(encodeException.Message, expectedMessage);
        StringAssert.Contains(reconstructException.Message, expectedMessage);
    }

    [TestMethod]
    public void MemoryOverloads_RejectRanges_WhenFirstRangeIsEmpty()
    {
        var codec = new Codec(StandardDataShardCount, StandardParityShardCount);
        var ranges = CreateSeparateRanges(StandardShardCount, SmallShardLength);
        var shardPresence = Enumerable.Repeat(element: true, StandardShardCount).ToArray();
        var workspace = new DecodeWorkspace(StandardDataShardCount, StandardParityShardCount);

        ranges[0] = Memory<byte>.Empty;

        var encodeException = Assert.ThrowsExactly<ArgumentException>(() => codec.EncodeParity(ranges));
        var reconstructException = Assert.ThrowsExactly<ArgumentException>(
            () => codec.ReconstructDataInto(ranges, shardPresence, workspace));

        StringAssert.Contains(encodeException.Message, "Shard 0 must exist and contain at least one byte");
        StringAssert.Contains(reconstructException.Message, "Shard 0 must exist and contain at least one byte");
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(SmallShardLength)]
    public void EncodeParity_NamesBothShards_WhenParityRangeOverlapsDataRange(int overlapBytes)
    {
        const int dataShardIndex = 3;

        var codec = new Codec(StandardDataShardCount, StandardParityShardCount);
        var ranges = CreateSeparateRanges(StandardShardCount, SmallShardLength);
        var sharedBuffer = new byte[2 * SmallShardLength];
        var originalShardBytes = (byte[])sharedBuffer.Clone();

        ranges[dataShardIndex] = sharedBuffer.AsMemory(0, SmallShardLength);
        ranges[StandardDataShardCount] = sharedBuffer.AsMemory(SmallShardLength - overlapBytes, SmallShardLength);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => codec.EncodeParity(ranges));

        StringAssert.Contains(
            exception.Message,
            $"Writable shard {StandardDataShardCount} must not share its buffer with shard {dataShardIndex}.");
        CollectionAssert.AreEqual(originalShardBytes, sharedBuffer, "A rejected call must not write.");
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(SmallShardLength)]
    public void ReconstructDataInto_NamesBothShards_WhenMissingDataRangeOverlapsAnotherRange(int overlapBytes)
    {
        const int missingShardIndex = 2;
        const int presentShardIndex = 7;

        var codec = new Codec(StandardDataShardCount, StandardParityShardCount);
        var ranges = CreateSeparateRanges(StandardShardCount, SmallShardLength);
        var sharedBuffer = new byte[2 * SmallShardLength];
        var shardPresence = Enumerable.Range(0, StandardShardCount)
            .Select(shardIndex => shardIndex != missingShardIndex)
            .ToArray();

        ranges[presentShardIndex] = sharedBuffer.AsMemory(0, SmallShardLength);
        ranges[missingShardIndex] = sharedBuffer.AsMemory(SmallShardLength - overlapBytes, SmallShardLength);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => codec.ReconstructDataInto(
            ranges,
            shardPresence,
            new DecodeWorkspace(StandardDataShardCount, StandardParityShardCount)));

        StringAssert.Contains(
            exception.Message,
            $"Writable shard {missingShardIndex} must not share its buffer with shard {presentShardIndex}.");
    }

    [TestMethod]
    public void ReconstructDataInto_NamesReceivedCounts_WhenWorkspaceOrFlagsDoNotMatch()
    {
        var codec = new Codec(StandardDataShardCount, StandardParityShardCount);
        var ranges = CreateSeparateRanges(StandardShardCount, SmallShardLength);
        var shardPresence = Enumerable.Repeat(element: true, StandardShardCount).ToArray();

        var workspaceException = Assert.ThrowsExactly<ArgumentException>(
            () => codec.ReconstructDataInto(ranges, shardPresence, new DecodeWorkspace(9, 3)));
        var flagException = Assert.ThrowsExactly<ArgumentException>(() => codec.ReconstructDataInto(
            ranges,
            shardPresence[..(StandardShardCount - 1)],
            new DecodeWorkspace(StandardDataShardCount, StandardParityShardCount)));

        StringAssert.Contains(workspaceException.Message, "a workspace for 9 data and 3 parity shards");
        StringAssert.Contains(flagException.Message, $"but got {StandardShardCount - 1} flags");
    }

    private static void AssertPackedRangeRecovery(ShardLossCase testCase)
    {
        var shardCount = testCase.DataShardCount + testCase.ParityShardCount;
        var codec = new Codec(testCase.DataShardCount, testCase.ParityShardCount, testCase.UseSimd);
        var data = CreateRandomBytes(testCase.DataShardCount * testCase.ShardLength, testCase.Seed);
        var expectedShards = codec.ManagedEncode(data);
        var buffer = CreateGuardedBuffer(shardCount, testCase.ShardLength);
        var ranges = CreatePackedRanges(buffer, shardCount, testCase.ShardLength);

        CopyDataShards(expectedShards, ranges, testCase.DataShardCount);

        for (var parityShardIndex = testCase.DataShardCount; parityShardIndex < shardCount; parityShardIndex++)
        {
            ranges[parityShardIndex].Span.Fill(DirtyBufferValue);
        }

        codec.EncodeParity(ranges);

        AssertRangesEqual(expectedShards, ranges, $"after encoding. {testCase}");
        AssertGuardsUnchanged(buffer, shardCount, testCase.ShardLength, testCase.ToString());

        var shardPresence = Enumerable.Repeat(element: true, shardCount).ToArray();

        foreach (var shardIndex in testCase.MissingShardIndices)
        {
            shardPresence[shardIndex] = false;
            ranges[shardIndex].Span.Fill(DirtyBufferValue);
        }

        var originalPresence = (bool[])shardPresence.Clone();
        var workspace = new DecodeWorkspace(testCase.DataShardCount, testCase.ParityShardCount);

        if (testCase.MissingShardIndices.Length > testCase.ParityShardCount)
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => codec.ReconstructDataInto(ranges, shardPresence, workspace), testCase.ToString());

            return;
        }

        codec.ReconstructDataInto(ranges, shardPresence, workspace);

        CollectionAssert.AreEqual(originalPresence, shardPresence, $"Presence flags must not change. {testCase}");
        AssertGuardsUnchanged(buffer, shardCount, testCase.ShardLength, testCase.ToString());

        for (var shardIndex = 0; shardIndex < shardCount; shardIndex++)
        {
            var isMissingParity = shardIndex >= testCase.DataShardCount && shardPresence[shardIndex] == false;
            var expectedShard = isMissingParity
                ? Enumerable.Repeat(DirtyBufferValue, testCase.ShardLength).ToArray()
                : expectedShards[shardIndex];

            CollectionAssert.AreEqual(expectedShard, ranges[shardIndex].ToArray(), $"Shard {shardIndex}. {testCase}");
        }
    }

    // Layout: guard, shard 0, guard, shard 1, ..., guard. Every guard byte holds GuardValue.
    private static byte[] CreateGuardedBuffer(int shardCount, int shardLength)
    {
        var bufferLength = GuardSizeBytes + shardCount * (shardLength + GuardSizeBytes);

        return Enumerable.Repeat(GuardValue, bufferLength).ToArray();
    }

    private static Memory<byte>[] CreatePackedRanges(Memory<byte> buffer, int shardCount, int shardLength)
    {
        var slotLength = shardLength + GuardSizeBytes;

        return Enumerable.Range(0, shardCount)
            .Select(shardIndex => buffer.Slice(GuardSizeBytes + shardIndex * slotLength, shardLength))
            .ToArray();
    }

    private static Memory<byte>[] CreateSeparateRanges(int shardCount, int shardLength)
    {
        return Enumerable.Range(0, shardCount)
            .Select(_ => new Memory<byte>(new byte[shardLength]))
            .ToArray();
    }

    private static void AssertGuardsUnchanged(ReadOnlySpan<byte> buffer, int shardCount, int shardLength, string context)
    {
        var slotLength = shardLength + GuardSizeBytes;

        for (var guardIndex = 0; guardIndex <= shardCount; guardIndex++)
        {
            var guardBytes = buffer.Slice(guardIndex * slotLength, GuardSizeBytes).ToArray();

            Assert.IsTrue(
                guardBytes.All(value => value == GuardValue),
                $"Guard bytes {guardIndex} outside the ranges must stay unchanged. {context}");
        }
    }

    private static void CopyDataShards(byte[][] sourceShards, Memory<byte>[] ranges, int dataShardCount)
    {
        for (var shardIndex = 0; shardIndex < dataShardCount; shardIndex++)
        {
            sourceShards[shardIndex].CopyTo(ranges[shardIndex]);
        }
    }

    private static void AssertRangesEqual(byte[][] expectedShards, Memory<byte>[] ranges, string context)
    {
        Assert.AreEqual(expectedShards.Length, ranges.Length, $"Shard counts differ {context}.");

        for (var shardIndex = 0; shardIndex < expectedShards.Length; shardIndex++)
        {
            CollectionAssert.AreEqual(expectedShards[shardIndex], ranges[shardIndex].ToArray(), $"Shard {shardIndex} differs {context}.");
        }
    }

    private static byte[] CreateRandomBytes(int length, int seed)
    {
        var bytes = new byte[length];
        var random = new Random(seed);

        random.NextBytes(bytes);

        return bytes;
    }
}
