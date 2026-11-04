namespace CompatibilityValidation;

/// <summary>Records what callers observe in the scenarios where a drop-in upgrade could behave differently.</summary>
internal static class BehaviorProbe
{
#if MANAGED_BYTE_API
    private const int DataShardCount = 10;
    private const int ParityShardCount = 2;
    private const int TotalShardCount = DataShardCount + ParityShardCount;
    private const int ShardLength = 64;
    private const int PayloadLength = DataShardCount * ShardLength;
    private const string RestoredOriginal = "returned the original payload";
    private const string WrongBytes = "returned WRONG bytes without an error";
    private const uint PayloadSeed = 1712;

    internal static BehaviorResult[] Run()
    {
        return
        [
            Observe("Zero-filled buffer instead of null for one lost data shard", () => DecodeWithZeroFilledShards(0)),
            Observe("Zero-filled buffers instead of null for two lost data shards", () => DecodeWithZeroFilledShards(0, 9)),
            Observe("Zero-filled buffers for more lost shards than parity", () => DecodeWithZeroFilledShards(0, 1, 2)),
            Observe("Intact data shard that really contains only zeroes", () => DecodePayloadWithZeroShards(3)),
            Observe("Intact payload with more zero shards than parity", () => DecodePayloadWithZeroShards(1, 2, 3)),
            Observe("Intact payload of only zeroes", DecodeAllZeroPayload),
            Observe("Null data shard after decoding", DescribeRestoredDataShard),
            Observe("Null parity shard after decoding", DescribeRestoredParityShard),
            Observe("Encoding an empty payload", DescribeEmptyPayloadEncoding),
            Observe("Decoding zero-length shards", DecodeZeroLengthShards),
            Observe("Encoding a null payload", () => DescribeEncoding(CreateCodec().ManagedEncode(null!))),
            Observe("More null shards than parity", () => DecodeWithNullShards(0, 1, 2)),
            Observe("Shard array one entry too short", () => DecodeResized(TotalShardCount - 1)),
            Observe("Shard array one entry too long", () => DecodeResized(TotalShardCount + 1)),
            Observe("Shards of unequal length", DecodeUnequalShards),
            Observe("Negative padding", () => DecodeWithPadding(-1)),
            Observe("Padding equal to the shard data length", () => DecodeWithPadding(PayloadLength)),
            Observe("Padding above the shard data length", () => DecodeWithPadding(PayloadLength + 1)),
            Observe("Omitted padding on padded data", DecodeOmittedPadding),
            Observe("Zero data shards", () => DescribeConstruction(0, ParityShardCount)),
            Observe("Zero parity shards", () => DescribeConstruction(DataShardCount, 0)),
            Observe("More than 256 total shards", () => DescribeConstruction(200, 57))
        ];
    }

    private static BehaviorResult Observe(string scenario, Func<string> scenarioAction)
    {
        try
        {
            return new BehaviorResult(scenario, scenarioAction());
        }
        catch (Exception exception)
        {
            return new BehaviorResult(scenario, $"threw {exception.GetType().Name}");
        }
    }

    private static CodecUnderTest CreateCodec()
    {
        return new CodecUnderTest(DataShardCount, ParityShardCount);
    }

    private static byte[] CreatePayload()
    {
        return Payload.Create(PayloadLength, PayloadSeed);
    }

    private static byte[] CreatePayloadWithZeroShards(int[] zeroShards)
    {
        var payload = CreatePayload();

        foreach (var zeroShard in zeroShards)
        {
            payload.AsSpan(zeroShard * ShardLength, ShardLength).Clear();
        }

        return payload;
    }

    private static string DescribeDecoded(byte[] decoded, byte[] payload)
    {
        return decoded.SequenceEqual(payload) ? RestoredOriginal : WrongBytes;
    }

    private static string DecodeWithZeroFilledShards(params int[] lostShards)
    {
        var codec = CreateCodec();
        var payload = CreatePayload();
        var shards = codec.ManagedEncode(payload);

        foreach (var lostShard in lostShards)
        {
            shards[lostShard] = new byte[ShardLength];
        }

        return DescribeDecoded(codec.ManagedDecode(shards, paddingSize: 0), payload);
    }

    private static string DecodePayloadWithZeroShards(params int[] zeroShards)
    {
        var codec = CreateCodec();
        var payload = CreatePayloadWithZeroShards(zeroShards);
        var shards = codec.ManagedEncode(payload);

        return DescribeDecoded(codec.ManagedDecode(shards, paddingSize: 0), payload);
    }

    private static string DecodeAllZeroPayload()
    {
        var codec = CreateCodec();
        var payload = new byte[PayloadLength];
        var shards = codec.ManagedEncode(payload);

        return DescribeDecoded(codec.ManagedDecode(shards, paddingSize: 0), payload);
    }

    private static string DescribeRestoredDataShard()
    {
        var codec = CreateCodec();
        var originalShards = codec.ManagedEncode(CreatePayload());
        var shards = originalShards.ToArray();

        shards[0] = null!;
        codec.ManagedDecode(shards, paddingSize: 0);

        if (shards[0] is null)
        {
            return "stays null";
        }

        return shards[0].SequenceEqual(originalShards[0])
            ? "contains the restored bytes"
            : "contains a zero-filled buffer, not the restored bytes";
    }

    private static string DescribeRestoredParityShard()
    {
        var codec = CreateCodec();
        var originalShards = codec.ManagedEncode(CreatePayload());
        var shards = originalShards.ToArray();

        shards[DataShardCount] = null!;
        codec.ManagedDecode(shards, paddingSize: 0);

        if (shards[DataShardCount] is null)
        {
            return "stays null";
        }

        return shards[DataShardCount].SequenceEqual(originalShards[DataShardCount])
            ? "contains the restored bytes"
            : "contains a zero-filled buffer, not the restored bytes";
    }

    private static string DescribeEmptyPayloadEncoding()
    {
        return DescribeEncoding(CreateCodec().ManagedEncode([]));
    }

    private static string DescribeEncoding(byte[][] shards)
    {
        return $"returned {shards.Length} shards of {shards[0].Length} bytes";
    }

    private static string DecodeZeroLengthShards()
    {
        var shards = Enumerable.Range(0, TotalShardCount)
            .Select(_ => Array.Empty<byte>())
            .ToArray();
        var decoded = CreateCodec().ManagedDecode(shards, paddingSize: 0);

        return $"returned {decoded.Length} bytes";
    }

    private static string DecodeWithNullShards(params int[] lostShards)
    {
        var codec = CreateCodec();
        var payload = CreatePayload();
        var shards = codec.ManagedEncode(payload);

        foreach (var lostShard in lostShards)
        {
            shards[lostShard] = null!;
        }

        return DescribeDecoded(codec.ManagedDecode(shards, paddingSize: 0), payload);
    }

    private static string DecodeResized(int shardCount)
    {
        var codec = CreateCodec();
        var payload = CreatePayload();
        var shards = codec.ManagedEncode(payload);

        Array.Resize(ref shards, shardCount);

        return DescribeDecoded(codec.ManagedDecode(shards, paddingSize: 0), payload);
    }

    private static string DecodeUnequalShards()
    {
        var codec = CreateCodec();
        var payload = CreatePayload();
        var shards = codec.ManagedEncode(payload);

        shards[1] = shards[1][..(ShardLength - 1)];

        return DescribeDecoded(codec.ManagedDecode(shards, paddingSize: 0), payload);
    }

    private static string DecodeWithPadding(int paddingSize)
    {
        var codec = CreateCodec();
        var shards = codec.ManagedEncode(CreatePayload());
        var decoded = codec.ManagedDecode(shards, paddingSize);

        return $"returned {decoded.Length} bytes";
    }

    private static string DecodeOmittedPadding()
    {
        var codec = CreateCodec();
        var shards = codec.ManagedEncode(Payload.Create(PayloadLength - 1, PayloadSeed));
        var decoded = codec.ManagedDecode(shards, paddingSize: 0);

        return $"returned {decoded.Length} bytes for a {PayloadLength - 1} byte payload";
    }

    private static string DescribeConstruction(int dataShardCount, int parityShardCount)
    {
        _ = new CodecUnderTest(dataShardCount, parityShardCount);

        return "created a codec";
    }
#else
    internal static BehaviorResult[] Run()
    {
        return [];
    }
#endif
}
