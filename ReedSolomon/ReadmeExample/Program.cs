using Witteborn.ReedSolomon;

const int dataShardCount = 4;
const int parityShardCount = 2;
const byte firstDirtyBufferValue = 0xA5;
const byte secondDirtyBufferValue = 0x5A;

var codec = new ReedSolomon(dataShardCount, parityShardCount);
var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

// null marks a missing shard; zeros are always valid data.

var shards = codec.ManagedEncode(data);
var paddedDataLength = shards[0].Length * dataShardCount;
var paddingSize = paddedDataLength - data.Length;

shards[1] = null!;
shards[5] = null!;

var decodedData = codec.ManagedDecode(shards, paddingSize);

if (decodedData.AsSpan().SequenceEqual(data) == false)
{
    throw new Exception("Managed recovery failed.");
}

Console.WriteLine($"Managed recovery: {string.Join(" ", decodedData)}");

// ManagedDecode leaves missing parity null. Rebuild it before a new loss scenario.
for (var shardIndex = dataShardCount; shardIndex < shards.Length; shardIndex++)
{
    if (shards[shardIndex] is null)
    {
        shards[shardIndex] = new byte[shards[0].Length];
    }
}

codec.EncodeParity(shards);

// Reuse caller-owned buffers and one workspace per concurrent worker.
var workspace = new DecodeWorkspace(dataShardCount, parityShardCount);
var shardPresence = Enumerable.Repeat(element: true, dataShardCount + parityShardCount).ToArray();

shardPresence[0] = false;
shardPresence[2] = false;
shards[0].AsSpan().Fill(firstDirtyBufferValue);
shards[2].AsSpan().Fill(secondDirtyBufferValue);
codec.ReconstructDataInto(shards, shardPresence, workspace);

var recoveredData = shards
    .Take(dataShardCount)
    .SelectMany(shard => shard)
    .Take(data.Length)
    .ToArray();

if (recoveredData.AsSpan().SequenceEqual(data) == false)
{
    throw new Exception("Workspace recovery failed.");
}

Console.WriteLine($"Workspace recovery: {string.Join(" ", recoveredData)}");
