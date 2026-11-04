using Codec = Witteborn.ReedSolomon.ReedSolomon;

namespace ReedSolomonTests;

[TestClass]
public sealed class UpgradeGuideTests
{
    private const int DataShardCount = 4;
    private const int ParityShardCount = 2;
    private const int PayloadLength = 17;

    [TestMethod]
    [DataRow(17, 4, 3)]
    [DataRow(640, 10, 0)]
    [DataRow(1, 10, 9)]
    [DataRow(639, 10, 1)]
    public void GetPaddingSize_MatchesEncodedShards_WhenPayloadIsEncoded(int dataLength, int dataShardCount, int expectedPaddingSize)
    {
        var codec = new Codec(dataShardCount, ParityShardCount);

        var shards = codec.ManagedEncode(new byte[dataLength]);

        var paddingFromShards = shards[0].Length * dataShardCount - dataLength;

        Assert.AreEqual(expectedPaddingSize, UpgradeGuideSnippets.GetPaddingSize(dataLength, dataShardCount));
        Assert.AreEqual(paddingFromShards, UpgradeGuideSnippets.GetPaddingSize(dataLength, dataShardCount));
    }

    [TestMethod]
    public void IsParityCorrect_ReturnsTrue_WhenShardsWereStoredByPublishedPackage()
    {
        var codec = new Codec(DataShardCount, ParityShardCount);

        var isParityCorrect = UpgradeGuideSnippets.IsParityCorrect(codec, CreateStoredShards(), DataShardCount);

        Assert.IsTrue(isParityCorrect);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Changed data shard")]
    [DataRow(DataShardCount, DisplayName = "Changed parity shard")]
    public void IsParityCorrect_ReturnsFalse_WhenShardByteChanged(int changedShardIndex)
    {
        var codec = new Codec(DataShardCount, ParityShardCount);
        var shards = CreateStoredShards();

        shards[changedShardIndex][0] ^= 1;

        Assert.IsFalse(UpgradeGuideSnippets.IsParityCorrect(codec, shards, DataShardCount));
    }

    [TestMethod]
    public void IsParityCorrect_ReturnsFalse_WhenLostShardWasStoredAsZeroes()
    {
        var codec = new Codec(DataShardCount, ParityShardCount);
        var shards = CreateStoredShards();

        shards[1] = new byte[shards[1].Length];

        Assert.IsFalse(UpgradeGuideSnippets.IsParityCorrect(codec, shards, DataShardCount));
    }

    [TestMethod]
    public void IsParityCorrect_LeavesShardsUnchanged_WhenChecking()
    {
        var codec = new Codec(DataShardCount, ParityShardCount);
        var shards = CreateStoredShards();

        UpgradeGuideSnippets.IsParityCorrect(codec, shards, DataShardCount);

        var expectedShards = CreateStoredShards();

        for (var shardIndex = 0; shardIndex < shards.Length; shardIndex++)
        {
            CollectionAssert.AreEqual(expectedShards[shardIndex], shards[shardIndex]);
        }
    }

    [TestMethod]
    public void ReconstructDataInto_ReplacesDecodeMissing_WhenFollowedByEncodeParity()
    {
        var codec = new Codec(DataShardCount, ParityShardCount);
        var workspace = new Witteborn.ReedSolomon.DecodeWorkspace(DataShardCount, ParityShardCount);
        var expectedShards = CreateStoredShards();
        var shards = CreateStoredShards();
        var shardPresence = Enumerable.Repeat(element: true, shards.Length).ToArray();

        shardPresence[0] = false;
        shardPresence[DataShardCount] = false;
        shards[0].AsSpan().Clear();
        shards[DataShardCount].AsSpan().Clear();

        codec.ReconstructDataInto(shards, shardPresence, workspace);
        codec.EncodeParity(shards);

        for (var shardIndex = 0; shardIndex < shards.Length; shardIndex++)
        {
            CollectionAssert.AreEqual(expectedShards[shardIndex], shards[shardIndex]);
        }
    }

    [TestMethod]
    public void ManagedDecode_ReturnsPayloadLength_WhenPaddingComesFromGuide()
    {
        var codec = new Codec(DataShardCount, ParityShardCount);
        var paddingSize = UpgradeGuideSnippets.GetPaddingSize(PayloadLength, DataShardCount);

        var decoded = codec.ManagedDecode(CreateStoredShards(), paddingSize);

        Assert.HasCount(PayloadLength, decoded);
    }

    /// <summary>Shards of the payload 0 to 16, encoded by the published ReedSolomon 2.0.0 package.</summary>
    private static byte[][] CreateStoredShards()
    {
        var storedShards = new[] { "AAECAwQ=", "BQYHCAk=", "CgsMDQ4=", "DxAAAAA=", "FKi6B2w=", "EeeJGGs=" };

        return Array.ConvertAll(storedShards, Convert.FromBase64String);
    }
}
