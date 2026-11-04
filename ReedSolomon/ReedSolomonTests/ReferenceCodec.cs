using Witteborn.ReedSolomon;

namespace ReedSolomonTests;

internal static class ReferenceCodec
{
    internal static byte[][] Encode(byte[] data, int dataShardCount, int parityShardCount)
    {
        var shardLength = (data.Length - 1) / dataShardCount + 1;
        var matrix = new Matrix(dataShardCount + parityShardCount, dataShardCount);

        for (var rowIndex = 0; rowIndex < dataShardCount + parityShardCount; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < dataShardCount; columnIndex++)
            {
                matrix.Set(rowIndex, columnIndex, Galois.Exp(unchecked((sbyte)rowIndex), columnIndex));
            }
        }

        matrix = matrix.Times(matrix.Submatrix(0, 0, dataShardCount, dataShardCount).Invert());

        var shards = Enumerable.Range(0, dataShardCount + parityShardCount)
            .Select(_ => new byte[shardLength])
            .ToArray();

        for (var byteIndex = 0; byteIndex < data.Length; byteIndex++)
        {
            shards[byteIndex / shardLength][byteIndex % shardLength] = data[byteIndex];
        }

        for (var rowIndex = dataShardCount; rowIndex < shards.Length; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < dataShardCount; columnIndex++)
            {
                for (var byteIndex = 0; byteIndex < shardLength; byteIndex++)
                {
                    shards[rowIndex][byteIndex] ^= unchecked((byte)Galois.Multiply(
                        matrix.Get(rowIndex, columnIndex),
                        unchecked((sbyte)shards[columnIndex][byteIndex])));
                }
            }
        }

        return shards;
    }
}
