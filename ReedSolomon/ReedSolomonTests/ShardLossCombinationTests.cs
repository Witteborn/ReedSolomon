using Witteborn.ReedSolomon;
using Codec = Witteborn.ReedSolomon.ReedSolomon;

namespace ReedSolomonTests;

[TestClass]
public sealed class ShardLossCombinationTests
{
    private const byte DirtyBufferValue = 0xA5;

    [TestMethod]
    [DynamicData(nameof(ShardLossCases.ExhaustiveLossCases), typeof(ShardLossCases))]
    public void ManagedDecode_RecoversOrRejectsLosses_WhenEverySmallCombinationIsTested(ShardLossCase testCase)
    {
        AssertManagedRecovery(testCase);
    }

    [TestMethod]
    [DynamicData(nameof(ShardLossCases.ExhaustiveLossCases), typeof(ShardLossCases))]
    public void ReconstructDataInto_RecoversOrRejectsLosses_WhenEverySmallCombinationIsTested(ShardLossCase testCase)
    {
        AssertWorkspaceRecovery(testCase);
    }

    [TestMethod]
    [DynamicData(nameof(ShardLossCases.LargerLossCases), typeof(ShardLossCases))]
    public void ManagedDecode_RecoversOrRejectsLosses_WhenShardRatiosAndCountsVary(ShardLossCase testCase)
    {
        AssertManagedRecovery(testCase);
    }

    [TestMethod]
    [DynamicData(nameof(ShardLossCases.LargerLossCases), typeof(ShardLossCases))]
    public void ReconstructDataInto_RecoversOrRejectsLosses_WhenShardRatiosAndCountsVary(ShardLossCase testCase)
    {
        AssertWorkspaceRecovery(testCase);
    }

    [TestMethod]
    [DynamicData(nameof(ShardLossCases.RandomLossCases), typeof(ShardLossCases))]
    public void ManagedDecode_RecoversOrRejectsLosses_WhenSeededRandomCombinationsAreTested(ShardLossCase testCase)
    {
        AssertManagedRecovery(testCase);
    }

    [TestMethod]
    [DynamicData(nameof(ShardLossCases.RandomLossCases), typeof(ShardLossCases))]
    public void ReconstructDataInto_RecoversOrRejectsLosses_WhenSeededRandomCombinationsAreTested(ShardLossCase testCase)
    {
        AssertWorkspaceRecovery(testCase);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Decode_RestoresPayload_WhenExampleMixesDataAndParityLossAcrossApis(bool useSimd)
    {
        const int dataShardCount = 4;
        const int parityShardCount = 2;
        const int missingDataShardIndex = 1;
        const int missingParityShardIndex = 5;

        var data = Enumerable.Range(0, 17).Select(value => (byte)value).ToArray();
        var codec = new Codec(dataShardCount, parityShardCount, useSimd);
        var shards = codec.ManagedEncode(data);
        var expectedShards = shards.Select(shard => (byte[])shard.Clone()).ToArray();
        var paddingSize = shards[0].Length * dataShardCount - data.Length;

        shards[missingDataShardIndex] = null!;
        shards[missingParityShardIndex] = null!;

        // A decoder in a later session must need only the surviving shards and padding.
        codec = new Codec(dataShardCount, parityShardCount, useSimd);

        CollectionAssert.AreEqual(data, codec.ManagedDecode(shards, paddingSize));
        CollectionAssert.AreEqual(expectedShards[missingDataShardIndex], shards[missingDataShardIndex]);
        Assert.IsNull(shards[missingParityShardIndex], "Managed decoding restores data and leaves missing parity null.");

        // Reusing these shards requires an allocated parity buffer and regenerated parity.
        shards[missingParityShardIndex] = new byte[shards[0].Length];
        codec.EncodeParity(shards);

        for (var shardIndex = 0; shardIndex < shards.Length; shardIndex++)
        {
            CollectionAssert.AreEqual(expectedShards[shardIndex], shards[shardIndex]);
        }

        var shardPresence = Enumerable.Repeat(element: true, shards.Length).ToArray();

        shardPresence[0] = false;
        shardPresence[2] = false;
        shards[0].AsSpan().Fill(DirtyBufferValue);
        shards[2].AsSpan().Fill(DirtyBufferValue);
        codec.ReconstructDataInto(shards, shardPresence, new DecodeWorkspace(dataShardCount, parityShardCount));

        var recoveredData = shards.Take(dataShardCount).SelectMany(shard => shard).Take(data.Length).ToArray();

        CollectionAssert.AreEqual(data, recoveredData);
    }

    private static void AssertManagedRecovery(ShardLossCase testCase)
    {
        var data = CreatePayload(testCase);
        var codec = new Codec(testCase.DataShardCount, testCase.ParityShardCount, testCase.UseSimd);
        var shards = codec.ManagedEncode(data);
        var expectedShards = shards.Select(shard => (byte[])shard.Clone()).ToArray();
        var paddingSize = shards[0].Length * testCase.DataShardCount - data.Length;

        foreach (var shardIndex in testCase.MissingShardIndices)
        {
            shards[shardIndex] = null!;
        }

        codec = new Codec(testCase.DataShardCount, testCase.ParityShardCount, testCase.UseSimd);

        if (testCase.MissingShardIndices.Length > testCase.ParityShardCount)
        {
            Assert.ThrowsExactly<ArgumentException>(() => codec.ManagedDecode(shards, paddingSize), testCase.ToString());

            return;
        }

        CollectionAssert.AreEqual(data, codec.ManagedDecode(shards, paddingSize), testCase.ToString());

        for (var shardIndex = 0; shardIndex < shards.Length; shardIndex++)
        {
            var isMissingParity = shardIndex >= testCase.DataShardCount && testCase.MissingShardIndices.Contains(shardIndex);

            if (isMissingParity)
            {
                Assert.IsNull(shards[shardIndex], $"Missing parity shard {shardIndex} must remain null. {testCase}");

                continue;
            }

            CollectionAssert.AreEqual(expectedShards[shardIndex], shards[shardIndex], $"Shard {shardIndex}. {testCase}");
        }
    }

    private static void AssertWorkspaceRecovery(ShardLossCase testCase)
    {
        var data = CreatePayload(testCase);
        var codec = new Codec(testCase.DataShardCount, testCase.ParityShardCount, testCase.UseSimd);
        var shards = codec.ManagedEncode(data);
        var expectedShards = shards.Select(shard => (byte[])shard.Clone()).ToArray();
        var shardPresence = Enumerable.Repeat(element: true, shards.Length).ToArray();

        foreach (var shardIndex in testCase.MissingShardIndices)
        {
            shardPresence[shardIndex] = false;
            shards[shardIndex].AsSpan().Fill(DirtyBufferValue);
        }

        var originalPresence = (bool[])shardPresence.Clone();
        var workspace = new DecodeWorkspace(testCase.DataShardCount, testCase.ParityShardCount);

        if (testCase.MissingShardIndices.Length > testCase.ParityShardCount)
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => codec.ReconstructDataInto(shards, shardPresence, workspace), testCase.ToString());

            return;
        }

        codec.ReconstructDataInto(shards, shardPresence, workspace);

        CollectionAssert.AreEqual(originalPresence, shardPresence, $"Presence flags must not change. {testCase}");

        for (var shardIndex = 0; shardIndex < shards.Length; shardIndex++)
        {
            var isMissingParity = shardIndex >= testCase.DataShardCount && shardPresence[shardIndex] == false;

            if (isMissingParity)
            {
                var expectedDirtyBuffer = Enumerable.Repeat(DirtyBufferValue, testCase.ShardLength).ToArray();

                CollectionAssert.AreEqual(expectedDirtyBuffer, shards[shardIndex], $"Missing parity must remain untouched. {testCase}");

                continue;
            }

            CollectionAssert.AreEqual(expectedShards[shardIndex], shards[shardIndex], $"Shard {shardIndex}. {testCase}");
        }
    }

    private static byte[] CreatePayload(ShardLossCase testCase)
    {
        // Vary padding reproducibly; single-byte shards retain one payload byte per data shard.
        var paddingSize = testCase.ShardLength == 1 ? 0 : testCase.Seed % testCase.DataShardCount;
        var data = new byte[testCase.DataShardCount * testCase.ShardLength - paddingSize];
        var random = new Random(testCase.Seed);

        random.NextBytes(data);

        return data;
    }
}
