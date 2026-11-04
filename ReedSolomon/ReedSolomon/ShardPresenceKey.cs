using System.Runtime.CompilerServices;

namespace Witteborn.ReedSolomon;

/// <summary>Identifies a set of available shards for the reconstruction-plan cache.</summary>
/// <remarks>Each bit represents one shard. Four groups of 64 bits cover the maximum 256 shards without allocation.</remarks>
internal readonly record struct ShardPresenceKey(
    ulong Shards0To63,
    ulong Shards64To127,
    ulong Shards128To191,
    ulong Shards192To255)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ShardPresenceKey Create(ReadOnlySpan<bool> present)
    {
        const int shardsPerGroup = 64;

        var firstGroup = 0UL;
        var secondGroup = 0UL;
        var thirdGroup = 0UL;
        var fourthGroup = 0UL;

        for (var shardIndex = 0; shardIndex < present.Length; shardIndex++)
        {
            if (present[shardIndex] == false)
            {
                continue;
            }

            var presenceBit = 1UL << (shardIndex % shardsPerGroup);

            switch (shardIndex / shardsPerGroup)
            {
                case 0:
                    firstGroup |= presenceBit;
                    break;
                case 1:
                    secondGroup |= presenceBit;
                    break;
                case 2:
                    thirdGroup |= presenceBit;
                    break;
                default:
                    fourthGroup |= presenceBit;
                    break;
            }
        }

        return new ShardPresenceKey(firstGroup, secondGroup, thirdGroup, fourthGroup);
    }
}
