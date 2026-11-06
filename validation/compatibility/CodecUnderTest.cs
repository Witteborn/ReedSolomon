#if LEGACY_BUFFER_API == false
using Witteborn.ReedSolomon;
#endif

namespace CompatibilityValidation;

/// <summary>Hides the API differences between the local codec and the published packages.</summary>
/// <remarks>
/// The same probe source is compiled once per codec version, so every version runs identical checks.
/// Versions before 3.0.0 offer the signed-byte buffer API; version 2.0.0 also offers unsigned overloads.
/// Version 3.0.0 and the working tree share the current API.
/// </remarks>
internal sealed class CodecUnderTest
{
    private readonly Codec codec;
#if LEGACY_BUFFER_API == false
    private readonly DecodeWorkspace workspace;
#endif
#if SIGNED_MANAGED_ENCODE
    private readonly int dataShardCount;
    private readonly int parityShardCount;
#endif

    internal CodecUnderTest(int dataShardCount, int parityShardCount)
    {
        codec = new Codec(dataShardCount, parityShardCount);
#if LEGACY_BUFFER_API == false
        workspace = new DecodeWorkspace(dataShardCount, parityShardCount);
#endif
#if SIGNED_MANAGED_ENCODE
        this.dataShardCount = dataShardCount;
        this.parityShardCount = parityShardCount;
#endif
    }

    internal void EncodeParity(byte[][] shards)
    {
#if LEGACY_BUFFER_API && MANAGED_BYTE_API
        codec.EncodeParity(shards, 0, shards[0].Length);
#elif LEGACY_BUFFER_API
        var signedShards = ToSignedShards(shards);

        codec.EncodeParity(signedShards, 0, shards[0].Length);
        CopyToUnsignedShards(signedShards, shards);
#else
        codec.EncodeParity(shards);
#endif
    }

    internal void RestoreData(byte[][] shards, bool[] present)
    {
#if LEGACY_BUFFER_API && MANAGED_BYTE_API
        codec.DecodeMissing(shards, present, 0, shards[0].Length);
#elif LEGACY_BUFFER_API
        var signedShards = ToSignedShards(shards);

        codec.DecodeMissing(signedShards, present, 0, shards[0].Length);
        CopyToUnsignedShards(signedShards, shards);
#else
        codec.ReconstructDataInto(shards, present, workspace);
#endif
    }

#if SIGNED_MANAGED_ENCODE
    internal byte[][] ManagedEncode(byte[] data)
    {
        var signedData = Array.ConvertAll(data, value => unchecked((sbyte)value));
        var signedShards = codec.ManagedEncode(signedData, dataShardCount, parityShardCount);

        return Array.ConvertAll(
            signedShards,
            shard => Array.ConvertAll(shard, value => unchecked((byte)value)));
    }
#endif

#if MANAGED_BYTE_API
    internal byte[][] ManagedEncode(byte[] data)
    {
        return codec.ManagedEncode(data);
    }

    internal byte[] ManagedDecode(byte[][] shards, int paddingSize)
    {
        return codec.ManagedDecode(shards, paddingSize);
    }
#endif

#if LEGACY_BUFFER_API && MANAGED_BYTE_API == false
    private static sbyte[][] ToSignedShards(byte[][] shards)
    {
        return Array.ConvertAll(
            shards,
            shard => Array.ConvertAll(shard, value => unchecked((sbyte)value)));
    }

    private static void CopyToUnsignedShards(sbyte[][] signedShards, byte[][] shards)
    {
        for (var shardIndex = 0; shardIndex < shards.Length; shardIndex++)
        {
            shards[shardIndex] = Array.ConvertAll(signedShards[shardIndex], value => unchecked((byte)value));
        }
    }
#endif
}
