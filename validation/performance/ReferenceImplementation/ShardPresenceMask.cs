namespace ReferenceImplementation;

internal readonly record struct ShardPresenceMask(ulong Shards0To63, ulong Shards64To127, ulong Shards128To191, ulong Shards192To255);
