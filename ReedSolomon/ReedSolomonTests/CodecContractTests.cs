using System.Diagnostics;
using System.Reflection;
using Witteborn.ReedSolomon;
using Codec = Witteborn.ReedSolomon.ReedSolomon;

namespace ReedSolomonTests;

[TestClass]
public sealed class CodecContractTests
{
    private const int StandardParityShardCount = 2;
    private const int SimdBlockAlignmentBytes = 64;
    private const int StandardDataShardCount = 10;
    private const int StandardShardCount = StandardDataShardCount + StandardParityShardCount;
    private const int RandomSeed = 1712;
    private const byte FirstDirtyBufferValue = 0xA5;
    private const byte SecondDirtyBufferValue = 0x5A;

    [TestMethod]
    public void Library_KeepsJitOptimizationEnabled_WhenBuilt()
    {
        var debugFlags = typeof(Codec).Assembly.GetCustomAttribute<DebuggableAttribute>();

        Assert.IsFalse(debugFlags?.IsJITOptimizerDisabled ?? false);
    }

    [TestMethod]
    [DataRow(0, StandardParityShardCount)]
    [DataRow(-1, StandardParityShardCount)]
    [DataRow(StandardParityShardCount, 0)]
    [DataRow(StandardParityShardCount, -1)]
    [DataRow(Galois.FIELD_SIZE - 1, StandardParityShardCount)]
    [DataRow(int.MaxValue, 1)]
    [DataRow(1, int.MaxValue)]
    public void CodecConstructor_Throws_WhenShardCountsAreInvalid(int dataShardCount, int parityShardCount)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Codec(dataShardCount, parityShardCount));
        Assert.ThrowsExactly<ArgumentException>(() => new DecodeWorkspace(dataShardCount, parityShardCount));
    }

    [TestMethod]
    [DataRow(-SimdBlockAlignmentBytes)]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(SimdBlockAlignmentBytes - 1)]
    [DataRow(SimdBlockAlignmentBytes + 1)]
    public void CodecConstructor_Throws_WhenBlockSizeIsInvalid(int blockSize)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Codec(
            StandardDataShardCount,
            StandardParityShardCount,
            blockSize: blockSize));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(3)]
    [DataRow(5)]
    public void CodecConstructor_Throws_WhenUnrollFactorIsInvalid(int unrollFactor)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Codec(
            StandardDataShardCount,
            StandardParityShardCount,
            unroll: unrollFactor));
    }

    [TestMethod]
    public void ManagedApi_RejectsInput_WhenBuffersOrPaddingAreInvalid()
    {
        const int alignedPayloadSizeBytes = StandardDataShardCount * SimdBlockAlignmentBytes;

        var codec = new Codec(StandardDataShardCount, StandardParityShardCount);
        var workspace = new DecodeWorkspace(StandardDataShardCount, StandardParityShardCount);
        var shards = codec.ManagedEncode(new byte[alignedPayloadSizeBytes]);
        var shardPresence = Enumerable.Repeat(element: true, StandardShardCount).ToArray();

        Assert.ThrowsExactly<ArgumentNullException>(() => codec.ManagedEncode(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => codec.ManagedDecode(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => codec.EncodeParity(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => codec.ReconstructDataInto(shards, shardPresence, null!));
        Assert.ThrowsExactly<ArgumentException>(() => codec.EncodeParity(shards[..11]));
        Assert.ThrowsExactly<ArgumentException>(() => codec.ManagedDecode(shards[..11]));
        Assert.ThrowsExactly<ArgumentException>(() => codec.ManagedDecode(new byte[StandardShardCount][]));
        Assert.ThrowsExactly<ArgumentException>(() => codec.EncodeParity(Enumerable.Range(
            0,
            StandardShardCount).Select(_ => Array.Empty<byte>()).ToArray()));
        Assert.ThrowsExactly<ArgumentException>(() => codec.ManagedDecode(shards, -1));
        Assert.ThrowsExactly<ArgumentException>(() => codec.ManagedDecode(shards, alignedPayloadSizeBytes));
        Assert.ThrowsExactly<ArgumentException>(() => codec.ReconstructDataInto(
            shards,
            shardPresence,
            new DecodeWorkspace(9, 3)));
        Assert.ThrowsExactly<ArgumentException>(() => codec.ReconstructDataInto(
            shards,
            shardPresence[..11],
            workspace));
        shards[0] = null!;
        Assert.ThrowsExactly<ArgumentException>(() => codec.EncodeParity(shards));
        Assert.ThrowsExactly<ArgumentException>(() => codec.ReconstructDataInto(shards, shardPresence, workspace));
    }

    [TestMethod]
    public void ReconstructDataInto_RestoresData_WhenAllPresenceGroupsAreUsed()
    {
        const int maximumGeometryDataShardCount = 200;
        const int maximumGeometryParityShardCount = Galois.FIELD_SIZE - maximumGeometryDataShardCount;

        foreach (var useSimd in new[]
        {
            false,
            true
        })
        {
            foreach (var (dataShardCount, parityShardCount, missingShardIndices) in new[]
            {
                (1, Galois.FIELD_SIZE - 1, new[] { 0 }),
                (maximumGeometryDataShardCount, maximumGeometryParityShardCount, new[] { 0, SimdBlockAlignmentBytes, 128, maximumGeometryDataShardCount - 1, Galois.FIELD_SIZE - 1 })
            })
            {
                var codec = new Codec(dataShardCount, parityShardCount, useSimd);
                var data = new byte[dataShardCount * (SimdBlockAlignmentBytes + 1)];

                var random = new Random(RandomSeed);

                random.NextBytes(data);

                var shards = codec.ManagedEncode(data);
                var expected = shards.Select(shard => (byte[])shard.Clone()).ToArray();
                var shardPresence = Enumerable.Range(
                    0,
                    shards.Length).Select(shardIndex => missingShardIndices.Contains(shardIndex) == false).ToArray();

                foreach (var shardIndex in missingShardIndices)
                {
                    shards[shardIndex].AsSpan().Fill(FirstDirtyBufferValue);
                }

                codec.ReconstructDataInto(shards, shardPresence, new DecodeWorkspace(dataShardCount, parityShardCount));

                for (var shardIndex = 0; shardIndex < dataShardCount; shardIndex++)
                {
                    CollectionAssert.AreEqual(expected[shardIndex], shards[shardIndex]);
                }

                foreach (var shardIndex in missingShardIndices)
                {
                    shards[shardIndex] = null!;
                }

                CollectionAssert.AreEqual(data, codec.ManagedDecode(shards));
            }
        }
    }

    [TestMethod]
    public void ReconstructDataInto_IsolatesPayloads_WhenPlansOrWorkspacesChange()
    {
        const int extendedDataShardCount = 16;
        const int extendedParityShardCount = 4;

        const int payloadVersionCount = 2;
        const int extendedShardCount = extendedDataShardCount + extendedParityShardCount;

        const int multiVectorTailShardLength = 129;

        var codec = new Codec(extendedDataShardCount, extendedParityShardCount);
        var workspace = new DecodeWorkspace(extendedDataShardCount, extendedParityShardCount);

        for (var version = 0; version < payloadVersionCount; version++)
        {
            var data = new byte[extendedDataShardCount * (SimdBlockAlignmentBytes + 1)];

            var random = new Random(RandomSeed + version);

            random.NextBytes(data);

            var encoded = codec.ManagedEncode(data);

            for (var firstLostShardIndex = 0; firstLostShardIndex < extendedShardCount; firstLostShardIndex++)
            {
                for (
var secondLostShardIndex = firstLostShardIndex + 1;
secondLostShardIndex < extendedShardCount;
secondLostShardIndex++)
                {
                    var shards = encoded.Select(shard => (byte[])shard.Clone()).ToArray();
                    var shardPresence = Enumerable.Range(
                        0,
                        extendedShardCount).Select(shardIndex => shardIndex != firstLostShardIndex && shardIndex != secondLostShardIndex).ToArray();

                    shards[firstLostShardIndex].AsSpan().Fill(FirstDirtyBufferValue);
                    shards[secondLostShardIndex].AsSpan().Fill(SecondDirtyBufferValue);
                    codec.ReconstructDataInto(shards, shardPresence, workspace);

                    for (var shardIndex = 0; shardIndex < extendedDataShardCount; shardIndex++)
                    {
                        CollectionAssert.AreEqual(encoded[shardIndex], shards[shardIndex]);
                    }
                }
            }
        }

        Parallel.For(0, extendedDataShardCount, version =>
        {
            var data = new byte[extendedDataShardCount * multiVectorTailShardLength];

            var random = new Random(version);

            random.NextBytes(data);

            var shards = codec.ManagedEncode(data);
            var shardPresence = Enumerable.Range(
                0,
                extendedShardCount).Select(shardIndex => shardIndex != version && shardIndex != 19).ToArray();

            shards[version].AsSpan().Fill(FirstDirtyBufferValue);
            codec.ReconstructDataInto(
                shards,
                shardPresence,
                new DecodeWorkspace(extendedDataShardCount, extendedParityShardCount));
            CollectionAssert.AreEqual(data, shards.Take(extendedDataShardCount).SelectMany(shard => shard).ToArray());
        });
    }

    [TestMethod]
    public void ReconstructDataInto_ReusesAndReleasesBuffers_WhenWorkspaceIsWarm()
    {
        const int defaultBlockSizeBytes = 8192;
        const int reconstructionIterations = 64;

        var codec = new Codec(StandardDataShardCount, StandardParityShardCount);
        var workspace = new DecodeWorkspace(StandardDataShardCount, StandardParityShardCount);
        var shards = codec.ManagedEncode(new byte[StandardDataShardCount * (defaultBlockSizeBytes + 1)]);
        var shardPresence = Enumerable.Range(
            0,
            StandardShardCount).Select(shardIndex => shardIndex != 0 && shardIndex != 9).ToArray();

        for (var iteration = 0; iteration < reconstructionIterations; iteration++)
        {
            codec.ReconstructDataInto(shards, shardPresence, workspace);
        }

        var allocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < reconstructionIterations; iteration++)
        {
            codec.ReconstructDataInto(shards, shardPresence, workspace);
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBytesBefore;

        Assert.AreEqual(0L, allocatedBytes, "Warm reconstruction should reuse caller buffers and metadata.");

        foreach (var fieldName in new[]
        {
            "Inputs",
            "Outputs"
        })
        {
            var workspaceBuffers = (byte[][])typeof(DecodeWorkspace).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(workspace)!;

            Assert.IsTrue(workspaceBuffers.All(buffer => buffer is null), "Workspace must release payload references.");
        }
    }

    [TestMethod]
    public void ReconstructDataInto_PreservesFlagsAndParity_WhenRestoringData()
    {
        const int smallDataShardCount = 4;

        var codec = new Codec(smallDataShardCount, StandardParityShardCount);
        var data = new byte[17];
        var encoded = codec.ManagedEncode(data);
        var shardPresence = new[]
        {
            false,
            true,
            true,
            true,
            false,
            true
        };
        var originalFlags = (bool[])shardPresence.Clone();

        encoded[0].AsSpan().Fill(FirstDirtyBufferValue);
        encoded[4].AsSpan().Fill(SecondDirtyBufferValue);
        codec.ReconstructDataInto(
            encoded,
            shardPresence,
            new DecodeWorkspace(smallDataShardCount, StandardParityShardCount));
        Assert.IsTrue(encoded[0].All(value => value == 0));
        Assert.IsTrue(encoded[4].All(value => value == SecondDirtyBufferValue));
        CollectionAssert.AreEqual(originalFlags, shardPresence);
        encoded[0] = null!;
        encoded[4] = null!;
        CollectionAssert.AreEqual(data, codec.ManagedDecode(encoded, paddingSize: 3));
        Assert.IsNull(encoded[4]);
        encoded[4] = new byte[encoded[0].Length];
        codec.EncodeParity(encoded);
        Assert.IsTrue(encoded[4].All(value => value == 0));
    }
}
