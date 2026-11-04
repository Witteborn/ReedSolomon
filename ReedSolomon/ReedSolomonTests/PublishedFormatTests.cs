using System.Security.Cryptography;
using Codec = Witteborn.ReedSolomon.ReedSolomon;

namespace ReedSolomonTests;

/// <summary>Protects the shard bytes already stored by users of the published packages.</summary>
/// <remarks>
/// The expected values were produced by the published ReedSolomon 2.0.0 package through
/// validation/compatibility, which also compares all earlier published versions.
/// </remarks>
[TestClass]
public sealed class PublishedFormatTests
{
    [TestMethod]
    [DataRow(4, 2, 19, "2751A3275560499E53F3C626CA07F270D8BB6C208FFC437851A9929108FDCC74")]
    [DataRow(10, 2, 640, "26007075C1E25368CFBB3546FFFEF2AE7A219CEFB6C34752097E131543D5DA31")]
    [DataRow(10, 4, 1, "4CE2C8859554B40525C6AB5B5615D6AA36A2BA6B4795A3F93BBDFAC6EBFE41D4")]
    [DataRow(17, 3, 4097, "07C4E6AAF156F4A207A044766AF1BC4E0624866F0B062AA77A35579A735BE771")]
    [DataRow(128, 128, 4097, "A93B3689FCF694B827F24B39E5FD7565F5B3A9B20DFA15C696309329B37E562F")]
    [DataRow(255, 1, 4097, "B370F4BF5ECA452B3CF215D19D99F39CB7E741DB829BBE6BBD55753A1BF88433")]
    [DataRow(1, 255, 19, "F5254B73D001A687296ACEA2DC2ECAD32BBBBF67617408AF5E2E8776B1857EEF")]
    public void ManagedEncode_ProducesPublishedShardBytes_WhenEncodingSamePayload(
        int dataShardCount,
        int parityShardCount,
        int payloadLength,
        string expectedShardsHash)
    {
        var payload = CreatePayload(dataShardCount, parityShardCount, payloadLength);
        var codec = new Codec(dataShardCount, parityShardCount);

        var shards = codec.ManagedEncode(payload);

        var shardBytes = shards.SelectMany(shard => shard).ToArray();

        Assert.AreEqual(expectedShardsHash, Convert.ToHexString(SHA256.HashData(shardBytes)));
    }

    [TestMethod]
    [DataRow(0, 1, DisplayName = "Two lost data shards")]
    [DataRow(2, 5, DisplayName = "One lost data shard and one lost parity shard")]
    [DataRow(4, 5, DisplayName = "Two lost parity shards")]
    public void ManagedDecode_RestoresPayload_WhenShardsWereStoredByPublishedPackage(int firstLostShard, int secondLostShard)
    {
        const int dataShardCount = 4;
        const int parityShardCount = 2;
        const int paddingSize = 3;

        var storedShards = new[] { "AAECAwQ=", "BQYHCAk=", "CgsMDQ4=", "DxAAAAA=", "FKi6B2w=", "EeeJGGs=" };
        var shards = Array.ConvertAll(storedShards, Convert.FromBase64String);
        var expectedPayload = Enumerable.Range(0, 17).Select(value => (byte)value).ToArray();
        var codec = new Codec(dataShardCount, parityShardCount);

        shards[firstLostShard] = null!;
        shards[secondLostShard] = null!;

        var decoded = codec.ManagedDecode(shards, paddingSize);

        CollectionAssert.AreEqual(expectedPayload, decoded);
    }

    [TestMethod]
    public void ManagedDecode_TreatsBufferAsData_WhenLostShardIsZeroFilledInsteadOfNull()
    {
        const int dataShardCount = 4;
        const int parityShardCount = 2;
        const int shardLength = 8;

        var payload = CreatePayload(dataShardCount, parityShardCount, dataShardCount * shardLength);
        var codec = new Codec(dataShardCount, parityShardCount);
        var shards = codec.ManagedEncode(payload);

        shards[0] = new byte[shardLength];

        var decoded = codec.ManagedDecode(shards);

        CollectionAssert.AreEqual(new byte[shardLength], decoded[..shardLength]);
        CollectionAssert.AreEqual(payload[shardLength..], decoded[shardLength..]);
    }

    /// <summary>Recreates the deterministic payload of validation/compatibility.</summary>
    private static byte[] CreatePayload(int dataShardCount, int parityShardCount, int payloadLength)
    {
        const uint multiplier = 1664525;
        const uint increment = 1013904223;
        const int highByteShift = 24;
        const int dataShardSeedFactor = 1000;

        var payload = new byte[payloadLength];
        var generatorState = (uint)(dataShardCount * dataShardSeedFactor + parityShardCount + payloadLength);

        for (var byteIndex = 0; byteIndex < payloadLength; byteIndex++)
        {
            generatorState = unchecked(generatorState * multiplier + increment);
            payload[byteIndex] = (byte)(generatorState >> highByteShift);
        }

        return payload;
    }
}
