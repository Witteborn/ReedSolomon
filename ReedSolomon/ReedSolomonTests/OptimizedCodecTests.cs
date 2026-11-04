using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;
using Witteborn.ReedSolomon;
using Codec = Witteborn.ReedSolomon.ReedSolomon;

namespace ReedSolomonTests;

[TestClass]
public sealed class OptimizedCodecTests
{
    private const int StandardDataShardCount = 10;
    private const int StandardParityShardCount = 2;
    private const int StandardShardCount = StandardDataShardCount + StandardParityShardCount;
    private const int SmallDataShardCount = 4;
    private const int ExtendedDataShardCount = 16;
    private const int ExtendedParityShardCount = 4;

    private const int Avx2VectorSizeBytes = 32;
    private const int SimdBlockAlignmentBytes = 64;
    private const int DefaultBlockSizeBytes = 8192;
    private const int LargeEncodeBlockSizeBytes = 32768;
    private const int LargeDecodeBlockSizeBytes = 131072;
    private const int SingleVectorUnrollFactor = 1;
    private const int TwoVectorUnrollFactor = 2;
    private const int FourVectorUnrollFactor = 4;

    private const int ShortPayloadSizeBytes = 19;
    private const int AlignedPayloadSizeBytes = StandardDataShardCount * SimdBlockAlignmentBytes;
    private const int SmallPayloadSizeBytes = 4096;
    private const int LargePayloadSizeBytes = 64 * 1024 + 1;
    private const int PatternMultiplier = 149;
    private const int PatternDivisor = 7;
    private const byte FirstDirtyBufferValue = 0xA5;
    private const byte SecondDirtyBufferValue = 0x5A;

    [TestMethod]
    [CombinatorialData]
    public void ManagedDecode_RestoresPayload_WhenOneOrTwoShardsAreMissing(
        bool useSimd,
        [CombinatorialValues(StandardDataShardCount, SmallDataShardCount, ExtendedDataShardCount)] int dataShardCount,
        [CombinatorialValues(
            1, ShortPayloadSizeBytes, Avx2VectorSizeBytes - 1, Avx2VectorSizeBytes, Avx2VectorSizeBytes + 1,
            AlignedPayloadSizeBytes, SmallPayloadSizeBytes, LargePayloadSizeBytes)] int payloadLength)
    {
        const int noLostShard = -1;

        var parityShardCount = dataShardCount == ExtendedDataShardCount
            ? ExtendedParityShardCount
            : StandardParityShardCount;
        var payload = Enumerable.Range(0, payloadLength)
            .Select(byteIndex =>
            {
                var multipliedIndex = byteIndex * PatternMultiplier;
                var groupOffset = byteIndex / PatternDivisor;
                var patternValue = multipliedIndex + groupOffset;

                return (byte)(patternValue % Galois.FIELD_SIZE);
            })
            .ToArray();
        var referenceShards = ReferenceCodec.Encode(payload, dataShardCount, parityShardCount);
        var codec = new Codec(dataShardCount, parityShardCount, useSimd);
        var encodedShards = codec.ManagedEncode(payload);

        AssertShardsEqual(referenceShards, encodedShards);

        for (var firstLostShardIndex = noLostShard; firstLostShardIndex < encodedShards.Length; firstLostShardIndex++)
        {
            for (
var secondLostShardIndex = firstLostShardIndex;
secondLostShardIndex < encodedShards.Length;
secondLostShardIndex++)
            {
                var hasSecondLossWithoutFirst = firstLostShardIndex == noLostShard && secondLostShardIndex != noLostShard;

                if (hasSecondLossWithoutFirst)
                {
                    continue;
                }

                int[] missingShardIndices;

                if (firstLostShardIndex == noLostShard)
                {
                    missingShardIndices = Array.Empty<int>();
                }
                else if (firstLostShardIndex == secondLostShardIndex)
                {
                    missingShardIndices = [firstLostShardIndex];
                }
                else
                {
                    missingShardIndices = [firstLostShardIndex, secondLostShardIndex];
                }

                var damagedShards = encodedShards
                    .Select((shard, shardIndex) => missingShardIndices.Contains(shardIndex) ? null : (byte[])shard.Clone())
                    .ToArray();
                var paddingSize = encodedShards[0].Length * dataShardCount - payload.Length;

                var decodedPayload = codec.ManagedDecode(damagedShards, paddingSize);

                CollectionAssert.AreEqual(payload, decodedPayload,
                    $"Recovery failed for missing shards [{firstLostShardIndex}, {secondLostShardIndex}].");
            }
        }
    }

    [TestMethod]
    [CombinatorialData]
    public void ManagedDecode_RestoresPayload_WhenBytesHaveAConstantValue(
        bool useSimd,
        [CombinatorialValues((byte)0, (byte)127, (byte)128, byte.MaxValue)] byte value)
    {
        var payload = Enumerable.Repeat(value, SmallPayloadSizeBytes + 1).ToArray();
        var codec = new Codec(StandardDataShardCount, StandardParityShardCount, useSimd);
        var shards = codec.ManagedEncode(payload);
        var paddingSize = shards[0].Length * StandardDataShardCount - payload.Length;

        shards[0] = null!;
        shards[StandardDataShardCount - 1] = null!;

        CollectionAssert.AreEqual(payload, codec.ManagedDecode(shards, paddingSize));
    }

    [TestMethod]
    [CombinatorialData]
    public void ManagedDecode_RestoresPayload_WhenSeveralShardsAreMissing(
        bool useSimd,
        [CombinatorialValues(3, ExtendedParityShardCount)] int missingShardCount)
    {
        var payload = Enumerable.Range(0, LargePayloadSizeBytes).Select(byteIndex => (byte)byteIndex).ToArray();
        var codec = new Codec(ExtendedDataShardCount, ExtendedParityShardCount, useSimd);
        var shards = codec.ManagedEncode(payload);
        var paddingSize = shards[0].Length * ExtendedDataShardCount - payload.Length;

        for (var shardIndex = 0; shardIndex < missingShardCount; shardIndex++)
        {
            shards[shardIndex] = null!;
        }

        CollectionAssert.AreEqual(payload, codec.ManagedDecode(shards, paddingSize));
    }

    [TestMethod]
    [CombinatorialData]
    public void ManagedDecode_MatchesReference_WhenPayloadCrossesProcessingBoundaries(
        bool useSimd,
        [CombinatorialValues(
            Avx2VectorSizeBytes - 1, Avx2VectorSizeBytes, Avx2VectorSizeBytes + 1,
            DefaultBlockSizeBytes - 1, DefaultBlockSizeBytes, DefaultBlockSizeBytes + 1)] int shardLength)
    {
        const int boundaryRandomSeed = 1201026;

        var payload = new byte[StandardDataShardCount * shardLength];

        var random = new Random(boundaryRandomSeed + shardLength);

        random.NextBytes(payload);

        var referenceShards = ReferenceCodec.Encode(payload, StandardDataShardCount, StandardParityShardCount);
        var codec = new Codec(StandardDataShardCount, StandardParityShardCount, useSimd);
        var encodedShards = codec.ManagedEncode(payload);

        AssertShardsEqual(referenceShards, encodedShards);

        foreach (var missingShardIndices in new[] {
new[] { 0 },
new[] { 0, StandardDataShardCount - 1 }
})
        {
            var damagedShards = encodedShards
                .Select((shard, shardIndex) => missingShardIndices.Contains(shardIndex) ? null : (byte[])shard.Clone())
                .ToArray();

            CollectionAssert.AreEqual(payload, codec.ManagedDecode(damagedShards));
        }
    }

    [TestMethod]
    [CombinatorialData]
    public void ManagedDecode_RestoresPayload_WhenDataAndParityShardsAreMissing(bool useSimd)
    {
        const int mixedLossRandomSeed = 801026;

        foreach (var missingShardIndices in new[] {
new[] { 0, 7, 15, 16 },
new[] { 0, 3, 8, 15 }
})
        {
            var payload = new byte[ExtendedDataShardCount * (DefaultBlockSizeBytes + 1)];

            var random = new Random(mixedLossRandomSeed);

            random.NextBytes(payload);

            var codec = new Codec(ExtendedDataShardCount, ExtendedParityShardCount, useSimd);
            var shards = codec.ManagedEncode(payload);

            foreach (var shardIndex in missingShardIndices)
            {
                shards[shardIndex] = null!;
            }

            CollectionAssert.AreEqual(payload, codec.ManagedDecode(shards));
        }
    }

    [TestMethod]
    [CombinatorialData]
    public void Codec_IsolatesPayloads_WhenFirstUsedConcurrently(bool useSimd)
    {
        const int concurrentDataShardCount = 7;
        const int concurrentParityShardCount = 3;

        const int parallelRandomSeed = 771026;
        const int parallelIterations = 32;

        const int parallelWorkerCount = 4;

        var payload = new byte[concurrentDataShardCount * (DefaultBlockSizeBytes + 1)];

        var random = new Random(parallelRandomSeed);

        random.NextBytes(payload);

        var referenceShards = ReferenceCodec.Encode(payload, concurrentDataShardCount, concurrentParityShardCount);
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = parallelWorkerCount };

        Parallel.For(0, parallelIterations, parallelOptions, _ =>
        {
            var codec = new Codec(concurrentDataShardCount, concurrentParityShardCount, useSimd);
            var shards = codec.ManagedEncode(payload);

            AssertShardsEqual(referenceShards, shards);

            shards[0] = null!;
            shards[concurrentDataShardCount - 2] = null!;

            CollectionAssert.AreEqual(payload, codec.ManagedDecode(shards));
        });
    }

    [TestMethod]
    [CombinatorialData]
    public void ManagedApi_RejectsInput_WhenShardsOrCountsAreInvalid(bool useSimd)
    {
        var codec = new Codec(StandardDataShardCount, StandardParityShardCount, useSimd);
        var insufficientShards = codec.ManagedEncode(new byte[SmallPayloadSizeBytes]);

        for (var shardIndex = 0; shardIndex < StandardParityShardCount + 1; shardIndex++)
        {
            insufficientShards[shardIndex] = null!;
        }

        Assert.Throws<ArgumentException>(() => codec.ManagedDecode(insufficientShards));

        var unequalShards = codec.ManagedEncode(new byte[SmallPayloadSizeBytes]);

        unequalShards[StandardParityShardCount] = new byte[1];

        Assert.Throws<ArgumentException>(() => codec.EncodeParity(unequalShards));
        Assert.Throws<ArgumentException>(() => codec.ManagedDecode(unequalShards));

        var aliasedShards = codec.ManagedEncode(new byte[SmallPayloadSizeBytes]);

        aliasedShards[StandardShardCount - 1] = aliasedShards[StandardDataShardCount];

        Assert.Throws<ArgumentException>(() => codec.EncodeParity(aliasedShards));

        aliasedShards = codec.ManagedEncode(new byte[SmallPayloadSizeBytes]);
        aliasedShards[StandardDataShardCount] = aliasedShards[0];

        Assert.Throws<ArgumentException>(() => codec.EncodeParity(aliasedShards));
        Assert.Throws<ArgumentException>(() => codec.ManagedEncode([]));
        Assert.Throws<ArgumentException>(() => new Codec(0, StandardParityShardCount, useSimd));
        Assert.Throws<ArgumentException>(() => new Codec(Galois.FIELD_SIZE - 1, StandardParityShardCount, useSimd));
    }

    [TestMethod]
    [CombinatorialData]
    public void ReconstructDataInto_RestoresPayload_WhenBuffersContainOldData(
        bool useSimd,
        [CombinatorialValues(SingleVectorUnrollFactor, TwoVectorUnrollFactor, FourVectorUnrollFactor)] int unrollFactor,
        [CombinatorialValues(
            1, Avx2VectorSizeBytes - 1, Avx2VectorSizeBytes, Avx2VectorSizeBytes + 1,
            SimdBlockAlignmentBytes - 1, SimdBlockAlignmentBytes, SimdBlockAlignmentBytes + 1,
            DefaultBlockSizeBytes - 1, DefaultBlockSizeBytes, DefaultBlockSizeBytes + 1)] int shardLength)
    {
        const int patternVersionStride = 31;
        const int payloadVersionCount = 2;

        var codec = new Codec(
            StandardDataShardCount,
            StandardParityShardCount,
            useSimd,
            DefaultBlockSizeBytes,
            unrollFactor);
        var workspace = new DecodeWorkspace(StandardDataShardCount, StandardParityShardCount);

        for (var version = 0; version < payloadVersionCount; version++)
        {
            var data = Enumerable.Range(0, StandardDataShardCount * shardLength)
                .Select(byteIndex =>
                {
                    var multipliedIndex = byteIndex * PatternMultiplier;
                    var groupOffset = byteIndex / PatternDivisor;
                    var patternValue = multipliedIndex + groupOffset;
                    var versionOffset = version * patternVersionStride;

                    return (byte)(patternValue + versionOffset);
                })
                .ToArray();
            var referenceShards = ReferenceCodec.Encode(data, StandardDataShardCount, StandardParityShardCount);
            var encodedShards = codec.ManagedEncode(data);

            AssertShardsEqual(referenceShards, encodedShards);

            for (var firstLostShardIndex = 0; firstLostShardIndex < StandardShardCount; firstLostShardIndex++)
            {
                for (
var secondLostShardIndex = firstLostShardIndex + 1;
secondLostShardIndex < StandardShardCount;
secondLostShardIndex++)
                {
                    var recoveryShards = encodedShards.Select(shard => (byte[])shard.Clone()).ToArray();
                    var shardPresence = Enumerable.Range(0, StandardShardCount)
                        .Select(shardIndex => shardIndex != firstLostShardIndex && shardIndex != secondLostShardIndex)
                        .ToArray();

                    recoveryShards[firstLostShardIndex].AsSpan().Fill(FirstDirtyBufferValue);
                    recoveryShards[secondLostShardIndex].AsSpan().Fill(SecondDirtyBufferValue);

                    codec.ReconstructDataInto(recoveryShards, shardPresence, workspace);

                    var recoveredData = recoveryShards.Take(StandardDataShardCount).SelectMany(shard => shard).ToArray();

                    CollectionAssert.AreEqual(data, recoveredData);
                }
            }
        }
    }

    [TestMethod]
    public void ReconstructDataInto_RejectsInput_WhenBuffersOrPresenceFlagsAreInvalid()
    {
        var codec = new Codec(StandardDataShardCount, StandardParityShardCount);
        var workspace = new DecodeWorkspace(StandardDataShardCount, StandardParityShardCount);
        var aliasedShards = codec.ManagedEncode(new byte[AlignedPayloadSizeBytes]);
        var shardPresence = Enumerable.Range(0, StandardShardCount).Select(shardIndex => shardIndex != 0).ToArray();

        aliasedShards[0] = aliasedShards[1];

        Assert.Throws<ArgumentException>(() => codec.ReconstructDataInto(aliasedShards, shardPresence, workspace));

        var cleanShards = codec.ManagedEncode(new byte[AlignedPayloadSizeBytes]);

        Assert.Throws<ArgumentException>(() => codec.ReconstructDataInto(
            cleanShards,
            new bool[StandardShardCount - 1],
            workspace));
        Assert.Throws<ArgumentException>(() => codec.ReconstructDataInto(
            cleanShards,
            new bool[StandardShardCount],
            workspace));
    }

    [TestMethod]
    [DataRow(SimdBlockAlignmentBytes, SingleVectorUnrollFactor)]
    [DataRow(LargeEncodeBlockSizeBytes, FourVectorUnrollFactor)]
    [DataRow(LargeDecodeBlockSizeBytes, FourVectorUnrollFactor)]
    public void Codec_MatchesScalarResults_WhenProcessingBlockBoundaries(int blockSize, int unroll)
    {
        foreach (var shardLength in new[] { blockSize - 1, blockSize, blockSize + 1 })
        {
            var data = new byte[StandardDataShardCount * shardLength];

            var random = new Random(shardLength);

            random.NextBytes(data);

            var expected = new Codec(StandardDataShardCount, StandardParityShardCount, simd: false).ManagedEncode(data);
            var codec = new Codec(
                StandardDataShardCount,
                StandardParityShardCount,
                blockSize: blockSize,
                unroll: unroll);
            var actual = codec.ManagedEncode(data);
            var shardPresence = Enumerable.Range(0, StandardShardCount)
                .Select(shardIndex => shardIndex != 0 && shardIndex != StandardDataShardCount - 1)
                .ToArray();

            AssertShardsEqual(expected, actual);

            actual[0].AsSpan().Fill(FirstDirtyBufferValue);
            actual[StandardDataShardCount - 1].AsSpan().Fill(SecondDirtyBufferValue);

            codec.ReconstructDataInto(
                actual,
                shardPresence,
                new DecodeWorkspace(StandardDataShardCount, StandardParityShardCount));

            for (var shardIndex = 0; shardIndex < StandardDataShardCount; shardIndex++)
            {
                CollectionAssert.AreEqual(expected[shardIndex], actual[shardIndex]);
            }
        }
    }

    private static void AssertShardsEqual(byte[][] expected, byte[][] actual)
    {
        Assert.AreEqual(expected.Length, actual.Length, "Shard counts differ.");

        for (var shardIndex = 0; shardIndex < expected.Length; shardIndex++)
        {
            CollectionAssert.AreEqual(expected[shardIndex], actual[shardIndex], $"Shard {shardIndex} differs.");
        }
    }
}
