using Witteborn.ReedSolomon;

ManagedExample_SByte();

Console.WriteLine();
Console.WriteLine();

ManagedExample_Byte();

Console.WriteLine();
Console.WriteLine();

Example_SByte();




void ManagedExample_SByte()
{
    Console.WriteLine("Managed Example SByte");
    Console.WriteLine("---------------------");
    const int dataShardCount = 4;
    const int parityShardCount = 2;

    // Initialize Reed-Solomon with data shards and parity shards
    ReedSolomon rs = new ReedSolomon(dataShardCount, parityShardCount);

    // Example data to encode
    sbyte[] data = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
    Console.WriteLine("Data:");
    Console.WriteLine(string.Join(" ", data));

    // Encode the data using ManagedEncode to produce shards
    var shards = rs.ManagedEncode(data, dataShardCount, parityShardCount);

    Console.WriteLine("Encoded Data:");

    foreach (var shard in shards)
    {
        Console.WriteLine(string.Join(" ", shard));
    }

    // Simulate loss of one shard 
    shards[1] = null;

    Console.WriteLine("Encoded with missing Data:");
    foreach (var shard in shards)
    {
        if (shard == null)
        {
            Console.WriteLine("null");
        }
        else
        {
            Console.WriteLine(string.Join(" ", shard));
        }
    }

    // Decode the remaining shards using ManagedDecode to recover original data
    var decodedData = rs.ManagedDecode(shards, dataShardCount, parityShardCount);

    Console.WriteLine("Decoded data:");
    Console.WriteLine(string.Join(" ", decodedData));
}

void Example_SByte()
{
    Console.WriteLine("Example SByte");
    Console.WriteLine("-------------");


    const int dataShardCount = 4;
    const int parityShardCount = 2;
    const int totalShardCount = dataShardCount + parityShardCount;
    const int shardSize = 4;

    // Create the shards array with data and empty parity shards
    sbyte[][] shards =
    [
            [0, 1, 2, 3],
            [4, 5, 6, 7],
            [8, 9, 10, 11],
            [12, 13, 14, 15],
            new sbyte[shardSize], // Parity shard 1
            new sbyte[shardSize]  // Parity shard 2
    ];

    Console.WriteLine("Shards:");
    foreach (var shard in shards)
    {
        Console.WriteLine(string.Join(" ", shard));
    }

    // Initialize Reed-Solomon with data shards and parity shards
    ReedSolomon rs = new ReedSolomon(dataShardCount, parityShardCount);



    // Encode the data with Reed-Solomon to generate parity shards
    rs.EncodeParity(shards, 0, shardSize);

    Console.WriteLine("Encoded data:");

    foreach (var shard in shards)
    {
        Console.WriteLine(string.Join(" ", shard));
    }

    // Simulate loss of one shard (e.g., network transmission loss)
    shards[1] = null; // Simulating the shard is lost

    Console.WriteLine("Encoded with missing Data:");
    foreach (var shard in shards)
    {
        if (shard == null)
        {
            Console.WriteLine("null");
        }
        else
        {
            Console.WriteLine(string.Join(" ", shard));
        }
    }

    bool[] shardPresent = shards.Select(shard => shard != null).ToArray();

    //Manual null fix
    for (int i = 0; i < shards.Length; i++)
    {
        if (shards[i] == null)
        {
            shards[i] = new sbyte[shardSize];
        }
    }

    // Decode the remaining shards to recover original data
    rs.DecodeMissing(shards, shardPresent, 0, shardSize);

    // Print the decoded data (should match original data)
    Console.WriteLine("Decoded data:");
    foreach (var shard in shards.Where(s => s != null))
    {
        Console.WriteLine(string.Join(" ", shard));
    }
}


void ManagedExample_Byte()
{
    Console.WriteLine("Managed Example Byte");
    Console.WriteLine("--------------------");
    const int dataShardCount = 4;
    const int parityShardCount = 2;

    // Initialize Reed-Solomon with data shards and parity shards
    ReedSolomon rs = new ReedSolomon(dataShardCount, parityShardCount);

    // Example data to encode
    byte[] data = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
    Console.WriteLine("Data:");
    Console.WriteLine(string.Join(" ", data));

    // Encode the data using ManagedEncode to produce shards
    var shards = rs.ManagedEncode(data, dataShardCount, parityShardCount);

    Console.WriteLine("Encoded Data:");
    foreach (var shard in shards)
    {
        Console.WriteLine(string.Join(" ", shard));
    }

    // Simulate loss of one shard 
    shards[1] = null;

    Console.WriteLine("Encoded with missing Data:");
    foreach (var shard in shards)
    {
        if (shard == null)
        {
            Console.WriteLine("null");
        }
        else
        {
            Console.WriteLine(string.Join(" ", shard));
        }
    }

    // Decode the remaining shards using ManagedDecode to recover original data
    var decodedData = rs.ManagedDecode(shards, dataShardCount, parityShardCount);

    Console.WriteLine("Decoded data:");
    Console.WriteLine(string.Join(" ", decodedData));
}