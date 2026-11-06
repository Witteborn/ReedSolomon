# Reed-Solomon for .NET

Reed-Solomon erasure coding for .NET. The codec splits data into data shards and adds parity
shards. When shards are lost, the original data can be restored as long as no more shards are
missing than there are parity shards.

```bash
dotnet add package ReedSolomon
```

The library requires .NET 10 and uses AVX2 or ARM64 NEON when the hardware supports them. A codec
supports up to 256 shards in total.

## Quick start

```csharp
using Witteborn.ReedSolomon;

const int dataShardCount = 10;
const int parityShardCount = 2;

var codec = new ReedSolomon(dataShardCount, parityShardCount);
var originalData = File.ReadAllBytes("input.bin");

var encodedShards = codec.ManagedEncode(originalData);
var paddingSize = encodedShards[0].Length * dataShardCount - originalData.Length;

// Later, mark every lost shard with null.
byte[]?[] shards = encodedShards;
shards[0] = null;
shards[dataShardCount - 1] = null;

var recoveredData = codec.ManagedDecode(shards, paddingSize);
```

`ManagedEncode` splits nonempty input into equal-length data shards, zero-pads the last ones and
returns the data shards followed by the parity shards. `ManagedDecode` restores the missing data
shards in the given array and returns the original data without padding. Missing parity shards stay
`null`.

A lost shard is always `null`. A shard that contains only zeroes is valid data and is never treated
as lost.

## What you need to store

The codec stores no metadata. To recover data later, keep:

- the data shard count and the parity shard count
- the index of every shard, because shards are not interchangeable
- the padding size or the original data length
- a checksum for every shard

The codec does not detect corrupted bytes. Check each shard against its checksum and pass a shard
that fails the check as `null`.

## Upgrading from 1.x or 2.x

Shards stored by any published version stay valid and do not need to be re-encoded. The API changed
in version 3, and lost shards must now be `null` instead of zero-filled. The
[upgrade guide](https://github.com/Witteborn/ReedSolomon/blob/master/UPGRADING.md) lists every change
and its replacement.

## Reusing buffers

For repeated operations, allocate equal-length, nonempty buffers once. The first `dataShardCount`
buffers hold data, and the remaining buffers receive parity.

```csharp
const int dataShardCount = 10;
const int parityShardCount = 2;
const int shardCount = dataShardCount + parityShardCount;
const int shardSizeBytes = 8192;

var codec = new ReedSolomon(dataShardCount, parityShardCount);
var workspace = new DecodeWorkspace(dataShardCount, parityShardCount);
var shards = Enumerable.Range(0, shardCount)
    .Select(_ => new byte[shardSizeBytes])
    .ToArray();

// Fill the data shards with the payload before encoding.
codec.EncodeParity(shards);

var shardPresence = Enumerable.Repeat(element: true, shardCount).ToArray();

shardPresence[0] = false;
shardPresence[dataShardCount - 1] = false;

codec.ReconstructDataInto(shards, shardPresence, workspace);
```

Reconstruction writes only the missing data shards. Their buffers must stay allocated, and their
previous contents do not matter. Presence flags and parity shards stay unchanged. To rebuild lost
parity, call `EncodeParity` once all data shards are restored. Two shards must never share a buffer.

Once warm, `EncodeParity` and `ReconstructDataInto` do not allocate.

### Memory ranges

When shard lengths change from one chunk to the next, pass `Memory<byte>` ranges instead of whole
arrays. A range may start at any offset of a larger buffer. One set of buffers sized for the longest
shard therefore serves every shorter shard, and the codec can write behind a header without copying.

```csharp
const int headerSizeBytes = 1;
const int maximumShardLengthBytes = 1_700_000;

// Allocate once for the longest shard, plus the header that precedes each stored shard.
var storedShards = Enumerable.Range(0, shardCount)
    .Select(_ => new byte[headerSizeBytes + maximumShardLengthBytes])
    .ToArray();

// For each chunk, select ranges of the current shard length behind the header.
var shardLength = 1_234_567;
var ranges = storedShards
    .Select(storedShard => storedShard.AsMemory(headerSizeBytes, shardLength))
    .ToArray();

// Fill the data ranges, including zero padding, then encode or reconstruct.
codec.EncodeParity(ranges);
codec.ReconstructDataInto(ranges, shardPresence, workspace);
```

The rules of the array overloads apply to every range, and the shard bytes are identical. Bytes
outside the ranges stay unchanged. A range that the codec writes must not overlap any other range.
Data ranges still hold the bytes of the previous chunk, so clear the padding before encoding.

## Concurrency

A codec can be shared by several threads that use separate buffers. Every concurrent worker needs its
own `DecodeWorkspace` with the codec's shard counts. Replacing a shard with one of another length while a
call is running throws `InvalidOperationException`, and the codec never touches memory outside the shards.

## Performance and tuning

As a reference value, encoding parity for 420 MiB with 10 data and 2 parity shards takes about 32 ms
on one logical processor of an AMD Ryzen 9 5950X. Timings depend on the hardware.
[`validation/compatibility`](https://github.com/Witteborn/ReedSolomon/tree/master/validation/compatibility)
measures them on your own machine.

```csharp
const int dataShardCount = 10;
const int parityShardCount = 2;
const int blockSizeBytes = 32768;
const int unrollFactor = 4;

var scalarCodec = new ReedSolomon(dataShardCount, parityShardCount, simd: false);
var tunedCodec = new ReedSolomon(
    dataShardCount,
    parityShardCount,
    blockSize: blockSizeBytes,
    unroll: unrollFactor);
```

`simd: false` forces the scalar code path. `blockSize` is a positive multiple of 64 bytes and
defaults to 8192. `unroll` accepts 1, 2 or 4 and defaults to 1. Change these settings only after
measuring your own workload.

## Development

Building the repository requires the .NET 10 SDK.

```bash
dotnet build ReedSolomon/ReedSolomon.slnx -c Release
dotnet test ReedSolomon/ReedSolomonTests/ReedSolomonTests.csproj -c Release
dotnet test ReedSolomon/ReedSolomonTests/ReedSolomonTests.csproj -c Release --environment DOTNET_EnableHWIntrinsic=0
dotnet run --project ReedSolomon/ReadmeExample -c Release
```

The second test run disables hardware intrinsics and covers the scalar code path. The tests check
every possible loss pattern for 1 to 6 data shards combined with 1 to 6 parity shards through both
decoding APIs, with and without SIMD. Larger and randomized cases go up to 256 shards.

Two tools compare the codec with other implementations:

```bash
dotnet run --project validation/performance/Validation.csproj -c Release
bash validation/compatibility/run.sh
```

The first compares encoded and reconstructed bytes and timings with a separate reference
implementation. The second compares shard bytes, behavior and performance with every published
package.

## License

MIT, see [LICENSE](https://github.com/Witteborn/ReedSolomon/blob/master/LICENSE). To report a
security issue, follow [SECURITY.md](https://github.com/Witteborn/ReedSolomon/blob/master/SECURITY.md).

The very first version of this library was a C# port of
[Backblaze's JavaReedSolomon](https://github.com/Backblaze/JavaReedSolomon) by Gökhan Witteborn.
Since then, the implementation has been changed significantly to make it much faster and more
robust, while the encoded data stays compatible. Thanks to Backblaze for publishing the original.
