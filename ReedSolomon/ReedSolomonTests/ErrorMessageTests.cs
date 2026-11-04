using Codec = Witteborn.ReedSolomon.ReedSolomon;

namespace ReedSolomonTests;

[TestClass]
public sealed class ErrorMessageTests
{
    private const int DataShardCount = 10;
    private const int ParityShardCount = 2;
    private const int TotalShardCount = DataShardCount + ParityShardCount;
    private const int ShardLength = 64;

    [TestMethod]
    public void CodecConstructor_NamesReceivedCounts_WhenShardCountsAreInvalid()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => new Codec(200, 57));

        StringAssert.Contains(exception.Message, "200 data and 57 parity");
    }

    [TestMethod]
    public void ManagedDecode_NamesExpectedAndReceivedCounts_WhenShardArrayHasWrongLength()
    {
        var codec = new Codec(DataShardCount, ParityShardCount);
        var shards = codec.ManagedEncode(new byte[DataShardCount * ShardLength]);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => codec.ManagedDecode(shards[..(TotalShardCount - 1)]));

        StringAssert.Contains(exception.Message, $"Expected {TotalShardCount} shards");
        StringAssert.Contains(exception.Message, $"but got {TotalShardCount - 1}");
    }

    [TestMethod]
    public void ManagedDecode_NamesRequiredAndPresentCounts_WhenTooManyShardsAreMissing()
    {
        var codec = new Codec(DataShardCount, ParityShardCount);
        var shards = codec.ManagedEncode(new byte[DataShardCount * ShardLength]);

        shards[0] = null!;
        shards[1] = null!;
        shards[2] = null!;

        var exception = Assert.ThrowsExactly<ArgumentException>(() => codec.ManagedDecode(shards));

        StringAssert.Contains(exception.Message, $"at least {DataShardCount} shards");
        StringAssert.Contains(exception.Message, $"only {TotalShardCount - 3} are present");
    }

    [TestMethod]
    public void ManagedDecode_NamesReceivedPadding_WhenPaddingCoversAllData()
    {
        var paddedDataLength = DataShardCount * ShardLength;
        var codec = new Codec(DataShardCount, ParityShardCount);
        var shards = codec.ManagedEncode(new byte[paddedDataLength]);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => codec.ManagedDecode(shards, paddedDataLength));

        StringAssert.Contains(exception.Message, $"but got {paddedDataLength}");
    }

    [TestMethod]
    public void EncodeParity_NamesAffectedShard_WhenBufferIsShared()
    {
        var codec = new Codec(DataShardCount, ParityShardCount);
        var shards = codec.ManagedEncode(new byte[DataShardCount * ShardLength]);

        shards[DataShardCount] = shards[0];

        var exception = Assert.ThrowsExactly<ArgumentException>(() => codec.EncodeParity(shards));

        StringAssert.Contains(exception.Message, $"shard {DataShardCount} must not share its buffer with shard 0");
    }
}
