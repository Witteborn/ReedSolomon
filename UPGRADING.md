# Upgrading to ReedSolomon 3

**Your stored shards stay valid and do not need to be re-encoded.** Version 3 keeps the
Reed-Solomon code of every published version and only changes the .NET API around it.
Most applications need three things:

1. Move to .NET 10 and update the package.
2. Replace the removed overloads using the [table below](#replace-removed-api).
3. **Mark lost shards with `null`.** This is the one change the compiler cannot find. See
   [the zero-filled shard trap](#the-zero-filled-shard-trap).

Every statement about behavior and data in this guide is measured with
[`validation/compatibility`](validation/compatibility/README.md), which runs the same checks against
each published package and against the current code. Run `bash validation/compatibility/run.sh` to
repeat them. The replacement code below is covered by `ReedSolomonTests/UpgradeGuideTests.cs`.

## Data

| Question | Answer |
|---|---|
| Must existing shards be re-encoded? | No. Parity from 1.0.0 through 2.0.0 is byte-identical in all 234 measured cases. |
| Does the shard layout of `ManagedEncode` change? | No. Shards from 1.1.0 through 2.0.0 are byte-identical in all 172 measured cases. 1.0.0 had no such method. |
| Can old shards be recovered by version 3? | Yes. All 58,170 measured loss patterns restore the original data. |
| Can old and new versions run side by side? | Yes, shards can be exchanged in both directions. Each version keeps its own decoding rules for zero-filled shards. |
| Can I roll back? | Yes. Version 3 writes the same bytes, so nothing has to be converted back. |
| Which metadata do I need to keep? | The same as before: data shard count, parity shard count and padding size or original length. |

### Check stored data before you switch

The upgrade changes how decoding treats zero-filled shards. If your storage may contain them, or
you want a safety check, verify shard sets with the [`IsParityCorrect`](#isparitycorrect)
replacement. A failed check means at least one shard does not belong to the set, but not which
one. Use your per-shard checksums to find it, then mark that shard as `null` and decode.

```csharp
foreach (var storedShardSet in storedShardSets)
{
    var codec = new ReedSolomon(storedShardSet.DataShardCount, storedShardSet.ParityShardCount);

    if (IsParityCorrect(codec, storedShardSet.Shards, storedShardSet.DataShardCount) == false)
    {
        ReportDamagedShardSet(storedShardSet);
    }
}
```

The codec does not detect corruption. As with earlier versions, verify the integrity of each
shard before treating it as present.

## The zero-filled shard trap

Earlier decoders treated any shard containing only zeroes as lost. Code could therefore mark a lost
shard with an empty buffer. Version 3 treats every non-null buffer as real data, because the old rule
rejected valid payloads and could not tell a lost shard from a shard of zeroes.

```csharp
// Worked up to 2.0.0. Version 3 returns wrong bytes without an error.
shards[lostShardIndex] ??= new byte[shardLength];
var data = codec.ManagedDecode(shards, paddingSize);

// Correct: a missing shard is null.
shards[lostShardIndex] = null;
var data = codec.ManagedDecode(shards, paddingSize);
```

Search your code for buffers allocated next to `ManagedDecode` and for `allowAllZeroes`. The parity
check below finds such a shard only if it runs before decoding.

## Replace removed API

The first three rows also apply to code that already compiles.

| Up to 2.0.0 | Version 3 |
|---|---|
| `new ReedSolomon(data, parity)` | Unchanged in source. Recompile: the constructor gained optional tuning parameters. |
| `ManagedEncode(byte[] data)` | Unchanged. An empty payload now throws `ArgumentException`. |
| `ManagedDecode(byte[][] shards, int padding)` | Unchanged signature. Zero-filled shards are data, as described above. |
| `ManagedEncode(byte[] data, data, parity)` | `new ReedSolomon(data, parity).ManagedEncode(data)` |
| `ManagedDecode(byte[][] shards, data, parity, padding)` | `new ReedSolomon(data, parity).ManagedDecode(shards, padding)` |
| `GetPaddingSize(length)` | [See below](#getpaddingsize) |
| `EncodeParity(byte[][] shards, 0, shardLength)` | `EncodeParity(shards)` |
| `EncodeParity(shards, offset, count)` | Copy the range into separate buffers and call `EncodeParity(buffers)`. |
| `DecodeMissing(byte[][] shards, present, 0, shardLength)` | `ReconstructDataInto(shards, present, workspace)`, then `EncodeParity(shards)` if parity was lost too. |
| `IsParityCorrect(shards, 0, shardLength)` | [See below](#isparitycorrect) |
| All `sbyte[]` overloads | Use the `byte[]` methods as shown in [Signed bytes](#signed-bytes). |
| `ManagedDecode(sbyte[][] shards, allowAllZeroes: true, ...)` | `ManagedDecode(byte[]?[] shards, padding)` |
| `ManagedDecode(sbyte[][] shards, allowAllZeroes: false, ...)` | Set lost shards to `null` first, then use the same method. |

Reusing buffers avoids allocations during reconstruction:

```csharp
var workspace = new DecodeWorkspace(dataShardCount, parityShardCount);

codec.ReconstructDataInto(shards, shardPresence, workspace);

// Unlike DecodeMissing, reconstruction restores data only.
codec.EncodeParity(shards);
```

Keep one `DecodeWorkspace` per concurrent worker. Buffers of lost shards must stay allocated.

### GetPaddingSize

```csharp
private static int GetPaddingSize(int dataLength, int dataShardCount)
{
    var bytesInLastPartialRow = dataLength % dataShardCount;

    return bytesInLastPartialRow == 0 ? 0 : dataShardCount - bytesInLastPartialRow;
}
```

After encoding, `shards[0].Length * dataShardCount - data.Length` gives the same value.

### IsParityCorrect

```csharp
private static bool IsParityCorrect(ReedSolomon codec, byte[][] shards, int dataShardCount)
{
    var recalculatedShards = shards
        .Select((shard, shardIndex) => shardIndex < dataShardCount ? shard : new byte[shard.Length])
        .ToArray();

    codec.EncodeParity(recalculatedShards);

    return Enumerable.Range(dataShardCount, shards.Length - dataShardCount)
        .All(shardIndex => recalculatedShards[shardIndex].SequenceEqual(shards[shardIndex]));
}
```

### Signed bytes

Prefer `byte[]` throughout. Where another part of an application still holds `sbyte[]`, convert at
the boundary. The stored bytes are the same.

```csharp
var unsignedShard = Array.ConvertAll(signedShard, value => unchecked((byte)value));
```

## Behavior changes

Measured against the published 2.0.0 package.

| Scenario | 2.0.0 | Version 3 |
|---|---|---|
| Zero-filled buffer for a lost shard | Restored the original payload | **Returns wrong bytes without an error** |
| Valid payload with more zero shards than parity | Threw `ArgumentException` | Returns the payload |
| Valid payload of only zeroes | Threw `ArgumentException` | Returns the payload |
| Null data shard after `ManagedDecode` | Zero-filled buffer in the caller's array | Restored bytes in the caller's array |
| Null parity shard after `ManagedDecode` | Zero-filled buffer in the caller's array | Stays `null` |
| Encoding an empty payload | 12 shards of 0 bytes | Throws `ArgumentException` |
| Shard array one entry too long | Threw `IndexOutOfRangeException` | Throws `ArgumentException` |
| Shards of unequal length | Returned wrong bytes without an error | Throws `ArgumentException` |
| Negative padding | Ignored | Throws `ArgumentException` |
| Padding at or above the data length | Returned 0 bytes | Throws `ArgumentException` |

Code that reads a parity shard after `ManagedDecode` must handle `null`. To rebuild it, allocate a
buffer for it and call `EncodeParity`.

Across 38,828 measured loss patterns with `ManagedDecode`, 2.0.0 rejected 7,717 because a shard of a
short payload happened to contain only zeroes. Version 3 restores all of them.

## Why these changes were made

| Change | Reason |
|---|---|
| .NET 10 only | One target keeps the library simple and lets it rely on current vector instructions. The older targets `netstandard2.0`, `net7.0`, `net8.0` and `net9.0` are gone. |
| `sbyte[]` overloads removed | Up to 2.0.0, every `byte[]` call converted all data to `sbyte[]` and back. The code now works directly on `byte[]`. |
| Shard counts per call removed | These overloads built a separate codec for each call and ignored the instance. A codec now represents one shard count, and its matrix is cached. |
| `offset` and `byteCount` removed | Whole shards are what the processing blocks and reconstruction plans are built for. |
| `DecodeMissing` replaced | `ReconstructDataInto` reuses caller buffers and plans, and does not allocate once warm. It restores data only, so parity is rebuilt only when needed. |
| `allowAllZeroes` removed | `null` is now the only marker for a lost shard, which removes the rejected payloads described above. |
| `GetPaddingSize` and `IsParityCorrect` removed | They do not depend on codec internals and can be written with the code above. |
| Class sealed | It was not designed for inheritance and had no virtual members. |
| Stricter argument checks | Invalid input now fails with a message naming the values instead of producing wrong results. |

The published 2.0.0 package also shipped with the JIT optimizer disabled, so it was much slower than
its source code allowed. As a reference value, encoding parity for 420 MiB with 10 data and 2 parity
shards on one logical processor of an AMD Ryzen 9 5950X took 21.6 s with 2.0.0 and 32 ms with
version 3. Timings depend on the hardware, so run `bash validation/compatibility/run.sh` to measure
on your own machine.

## Upgrading from 1.x

The data statements above apply. The API differs further: 1.0.0 offers only the `sbyte[]` buffer
methods, 1.1.0 through 1.2.1 use `ManagedEncode(data, dataShards, parityShards)`, and padding parameters
arrived in 1.3.0. Replace those calls as shown for `sbyte[]` above. Decoders before 1.3.0 returned the
padded data, so if your application stored only that form, pass padding `0` and keep your existing
length handling.
