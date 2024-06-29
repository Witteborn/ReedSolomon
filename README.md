# Reed-Solomon Algorithm Implementation

This project is a C# implementation of the Reed-Solomon error correction algorithm, based on Backblaze's original Java implementation. The library provides robust error correction capabilities and includes comprehensive unit tests.

## Description

Creating and maintaining projects can be challenging. This implementation simplifies integration of Reed-Solomon error correction into your applications. Originally used privately, I decided to publish this implementation under the MIT License.

If you find this project helpful, please consider starring it and sharing it with others.

## Installation

To install the Reed-Solomon library, simply add it to your project using NuGet Package Manager:

```bash
Install-Package Witteborn.ReedSolomon
```

## Usage

Using the Reed-Solomon library is straightforward. Here's a simple example of how to encode and decode data:

### Example: Encode and Decode

```csharp
using Witteborn.ReedSolomon;
using System;
using System.Linq;

class Program
{
    static void Main()
    {
        const int dataShardCount = 4;
        const int parityShardCount = 2;
        const int totalShardCount = dataShardCount + parityShardCount;
        const int shardSize = 4;

        // Create the shards array with data and empty parity shards
        sbyte[][] shards = new sbyte[totalShardCount][]
        {
            new sbyte[] { 0, 1, 2, 3 },
            new sbyte[] { 4, 5, 6, 7 },
            new sbyte[] { 8, 9, 10, 11 },
            new sbyte[] { 12, 13, 14, 15 },
            new sbyte[shardSize], // Parity shard 1
            new sbyte[shardSize]  // Parity shard 2
        };

        // Initialize Reed-Solomon with data shards and parity shards
        ReedSolomon rs = new ReedSolomon(dataShardCount, parityShardCount);

        // Encode the data with Reed-Solomon to generate parity shards
        rs.EncodeParity(shards, 0, shardSize);

        // Simulate loss of one shard (e.g., network transmission loss)
        shards[1] = null; // Simulating the shard is lost
        bool[] shardPresent = shards.Select(shard => shard != null).ToArray();

        // Decode the remaining shards to recover original data
        rs.DecodeMissing(shards, shardPresent, 0, shardSize);

        // Print the decoded data (should match original data)
        Console.WriteLine("Decoded data:");
        foreach (var shard in shards.Where(s => s != null))
        {
            Console.WriteLine(string.Join(" ", shard));
        }
    }
}
```

## Contribution

Contributions via pull requests are welcome. Please open an issue first and review our [Contribution Guidelines](./CONTRIBUTING.md) and [Code of Conduct](./CODE_OF_CONDUCT.md).

## Support

For assistance, refer to our [Support](./SUPPORT.md) file.

## Security

To report security issues or vulnerabilities, refer to our [Security](./SECURITY.md) file.

## Acknowledgments

- **Backblaze** for the original Java implementation that inspired this project.

And a big **Thank You** to all contributors, issue reporters, and supporters who have helped make this project possible.
