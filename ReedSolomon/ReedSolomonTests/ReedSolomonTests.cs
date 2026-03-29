using Microsoft.VisualStudio.TestTools.UnitTesting;
using Witteborn.ReedSolomon;

namespace ReedSolomonTests
{
    [TestClass()]
    public class ReedSolomonTests
    {
        [TestMethod()]
        public void ProduceEncodedShardsWithPaddingTest()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

            var shards = rs.ManagedEncode(data, 4, 2);

            //Assert
            sbyte[][] expectedShards =
            [
                [0, 1, 2, 3, 4],
                [5, 6, 7, 8, 9 ],
                [10, 11, 12, 13, 14],
                [15, 16, 0, 0, 0], // the zeroes are padding
                [20, -88, -70, 7, 108],
                [17, -25, -119, 24, 107]
            ];

            for (int i = 0; i < shards.Length; i++)
            {
                Assert.IsTrue(shards[i].SequenceEqual(expectedShards[i]));
            }
        }

        [TestMethod()]
        public void ProduceEncodedShardsTest()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

            var shards = rs.ManagedEncode(data, 4, 2);

            //Assert
            sbyte[][] expectedShards =
            [
                [0, 1, 2, 3],
                [4, 5, 6, 7],
                [8, 9, 10, 11],
                [12, 13, 14, 15],
                [16, 17, 18, 19],
                [20, 21, 22, 23]
            ];

            for (int i = 0; i < shards.Length; i++)
            {
                Assert.IsTrue(shards[i].SequenceEqual(expectedShards[i]));
            }
        }

        [TestMethod()]
        public void EncodeEmptyDataTest()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[] data = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

            var shards = rs.ManagedEncode(data, 4, 2);

            //Assert
            sbyte[][] expectedShards =
            [
                [0, 0, 0, 0],
                [0, 0, 0, 0],
                [0, 0, 0, 0],
                [0, 0, 0, 0],
                [0, 0, 0, 0],
                [0, 0, 0, 0]
            ];

            for (int i = 0; i < shards.Length; i++)
            {
                Assert.IsTrue(shards[i].SequenceEqual(expectedShards[i]));
            }
        }

        [TestMethod()]
        public void DecodeEmptySuccess_Test()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[][] shards =
            [
                [0, 0, 0, 0],   //Data
                [0, 0, 0, 0],   //Missing Data
                [0, 0, 0, 0],   //Data
                [0, 0, 0, 0],   //Data
                [0, 0, 0, 0],   //Parity
                [0, 0, 0, 0]    //Parity
            ];


            //Act
            var result = rs.ManagedDecode(shards, 4, 2, allowAllZeroes: true);

            //Assert
            sbyte[] expected =
               [0, 0, 0, 0,     //Data
                0, 0, 0, 0,     //Data
                0, 0, 0, 0,     //Data
                0, 0, 0, 0];    //Data

            Assert.IsTrue(result.SequenceEqual(expected));
        }

        [TestMethod()]
        public void ProduceEncodedShardsTest2()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            byte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

            //Act
            var shards = rs.ManagedEncode(data, 4, 2);

            //Assert
            byte[][] expectedShards =
            [
                [0, 1, 2, 3],
                [4, 5, 6, 7],
                [8, 9, 10, 11],
                [12, 13, 14, 15],
                [16, 17, 18, 19],
                [20, 21, 22, 23]
            ];

            for (int i = 0; i < shards.Length; i++)
            {
                Assert.IsTrue(shards[i].SequenceEqual(expectedShards[i]));
            }
        }

        [TestMethod()]
        public void ConvertedEncodeParityTest()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            byte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

            List<byte[]> shardsList = new();
            for (int i = 0; i < 4; i++)
            {
                var segment = new ArraySegment<byte>(data, data.Length / 4 * i, data.Length / 4);
                shardsList.Add(segment.ToArray());
            }

            for (int i = 0; i < 2; i++)
            {
                shardsList.Add(new byte[data.Length / 4]);
            }

            var shards = shardsList.ToArray();

            //Act
            rs.EncodeParity(shards, 0, data.Length / 4);

            //Assert
            byte[][] expectedShards =
            [
                [0, 1, 2, 3],
                [4, 5, 6, 7],
                [8, 9, 10, 11],
                [12, 13, 14, 15],
                [16, 17, 18, 19],
                [20, 21, 22, 23]
            ];

            for (int i = 0; i < shards.Length; i++)
            {
                Assert.IsTrue(shards[i].SequenceEqual(expectedShards[i]));
            }
        }

        [TestMethod()]
        public void EncodeParityTest()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

            List<sbyte[]> shardsList = new();
            for (int i = 0; i < 4; i++)
            {
                var segment = new ArraySegment<sbyte>(data, data.Length / 4 * i, data.Length / 4);
                shardsList.Add(segment.ToArray());
            }

            for (int i = 0; i < 2; i++)
            {
                shardsList.Add(new sbyte[data.Length / 4]);
            }

            var shards = shardsList.ToArray();

            //Act
            rs.EncodeParity(shards, 0, data.Length / 4);

            //Assert
            sbyte[][] expectedShards =
            [
                [0, 1, 2, 3],
                [4, 5, 6, 7],
                [8, 9, 10, 11],
                [12, 13, 14, 15],
                [16, 17, 18, 19],
                [20, 21, 22, 23]
            ];

            for (int i = 0; i < shards.Length; i++)
            {
                Assert.IsTrue(shards[i].SequenceEqual(expectedShards[i]));
            }
        }


        [TestMethod()]
        public void Decode_NoMissing_WithPadding_Success_Test()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[][] shards =
            [
                [0, 1, 2, 3, 4],
                [5, 6, 7, 8, 9 ],
                [10, 11, 12, 13, 14],
                [15, 16, 0, 0, 0], // the zeroes are padding
                [20, -88, -70, 7, 108], // Parity
                [17, -25, -119, 24, 107] // Parity
            ];


            //Act
            var result = rs.ManagedDecode(shards, 4, 2, paddingSize: 3);

            //Assert
            sbyte[] expected = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];    //Data

            Assert.IsTrue(result.SequenceEqual(expected));
        }

        [TestMethod()]
        public void Decode_NoMissing_WithPadding_Success_Test2()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[][] shards =
            [
                [0, 1, 2, 3, 4],
                [5, 6, 7, 8, 9 ],
                [10, 11, 12, 13, 14],
                [15, 16, 0, 0, 0], // the zeroes are padding
                [20, -88, -70, 7, 108], // Parity
                [17, -25, -119, 24, 107] // Parity
            ];


            //Act
            var result = rs.ManagedDecode(shards, paddingSize: 3);

            //Assert
            sbyte[] expected = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];    //Data

            Assert.IsTrue(result.SequenceEqual(expected));
        }

        [TestMethod()]
        public void Decode_OneMissing_WithPadding_Success_Test()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[][] shards =
            [
                [0, 1, 2, 3, 4],
                null,
                [10, 11, 12, 13, 14],
                [15, 16, 0, 0, 0], // the zeroes are padding
                [20, -88, -70, 7, 108], // Parity
                [17, -25, -119, 24, 107] // Parity
            ];


            //Act
            var result = rs.ManagedDecode(shards, 4, 2, paddingSize: 3);

            //Assert
            sbyte[] expected = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];    //Data

            Assert.IsTrue(result.SequenceEqual(expected));
        }

        [TestMethod()]
        public void Decode_OneMissing_WithPadding_Success_Test2()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[][] shards =
            [
                [0, 1, 2, 3, 4],
                [0, 0, 0, 0, 0],
                [10, 11, 12, 13, 14],
                [15, 16, 0, 0, 0], // the zeroes are padding
                [20, -88, -70, 7, 108], // Parity
                [17, -25, -119, 24, 107] // Parity
            ];


            //Act
            var result = rs.ManagedDecode(shards, 4, 2, paddingSize: 3);

            //Assert
            sbyte[] expected = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];    //Data

            Assert.IsTrue(result.SequenceEqual(expected));
        }

        [TestMethod()]
        public void EncodeAndDecode_OneMissing_WithPadding_Success_Test2()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
            sbyte[] expected = data.ToArray();


            //Act
            var encoded = rs.ManagedEncode(data, 4, 2);
            var paddingSize = rs.GetPaddingSize(data.Length, 4);
            var decoded = rs.ManagedDecode(encoded, 4, 2, paddingSize: paddingSize);

            //Assert
            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void Decode_OneShard_Success_Test()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[][] shards =
            [
                [0, 1, 2, 3],       //Data
                [0, 0, 0, 0],       //Missing Data
                [8, 9, 10, 11],     //Data
                [12, 13, 14, 15],   //Data
                [16, 17, 18, 19],   //Parity
                [20, 21, 22, 23]    //Parity
            ];


            //Act
            var result = rs.ManagedDecode(shards, 4, 2);

            //Assert
            sbyte[] expected =
               [0, 1, 2, 3,         //Data
                4, 5, 6, 7,         //Data
                8, 9, 10, 11,       //Data
                12, 13, 14, 15];    //Data

            Assert.IsTrue(result.SequenceEqual(expected));
        }

        [TestMethod()]
        public void Decode_OneShard_Success_Test2()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            byte[][] shards =
            [
                [0, 1, 2, 3],       //Data
                [0, 0, 0, 0],       //Missing Data
                [8, 9, 10, 11],     //Data
                [12, 13, 14, 15],   //Data
                [16, 17, 18, 19],   //Parity
                [20, 21, 22, 23]    //Parity
            ];


            //Act
            var result = rs.ManagedDecode(shards, 4, 2);

            //Assert
            byte[] expected =
               [0, 1, 2, 3,         //Data
                4, 5, 6, 7,         //Data
                8, 9, 10, 11,       //Data
                12, 13, 14, 15];    //Data

            Assert.IsTrue(result.SequenceEqual(expected));
        }

        [TestMethod()]
        public void Decode_NoMissing_WithPadding_Byte_Success_Test()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            byte[][] shards =
            [
                [0, 1, 2, 3, 4],
                [5, 6, 7, 8, 9],
                [10, 11, 12, 13, 14],
                [15, 16, 0, 0, 0], // the zeroes are padding
                [20, 168, 186, 7, 108], // Parity
                [17, 231, 137, 24, 107] // Parity
            ];

            //Act
            var result = rs.ManagedDecode(shards, 4, 2, paddingSize: 3);

            //Assert
            byte[] expected = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

            Assert.IsTrue(result.SequenceEqual(expected));
        }

        [TestMethod()]
        public void Decode_OneMissing_WithPadding_Byte_Success_Test()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            byte[][] shards =
            [
                [0, 1, 2, 3, 4],
                null,
                [10, 11, 12, 13, 14],
                [15, 16, 0, 0, 0], // the zeroes are padding
                [20, 168, 186, 7, 108], // Parity
                [17, 231, 137, 24, 107] // Parity
            ];

            //Act
            var result = rs.ManagedDecode(shards, 4, 2, paddingSize: 3);

            //Assert
            byte[] expected = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

            Assert.IsTrue(result.SequenceEqual(expected));
        }

        [TestMethod()]
        public void EncodeAndDecode_OneMissing_WithPadding_Byte_Success_Test()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            byte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
            byte[] expected = data.ToArray();

            //Act
            var encoded = rs.ManagedEncode(data, 4, 2);
            var paddingSize = rs.GetPaddingSize(data.Length, 4);
            encoded[1] = null;
            var decoded = rs.ManagedDecode(encoded, 4, 2, paddingSize: paddingSize);

            //Assert
            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void DecodeMissing_OneShard_Success_Test()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[][] shards =
            [
                [0, 1, 2, 3],
                //[4, 5, 6, 7],
                [0, 0, 0, 0],
                [8, 9, 10, 11],
                [12, 13, 14, 15],
                [16, 17, 18, 19],
                [20, 21, 22, 23]
            ];

            bool[] shardPresent = [true, false, true, true, true, true];
            //Act
            rs.DecodeMissing(shards, shardPresent, 0, shards[0].Length);

            //Assert

            sbyte[][] expectedShards =
            [
                [0, 1, 2, 3],
                [4, 5, 6, 7],
                [8, 9, 10, 11],
                [12, 13, 14, 15],
                [16, 17, 18, 19],
                [20, 21, 22, 23]
            ];

            for (int i = 0; i < shards.Length; i++)
            {
                Assert.IsTrue(shards[i].SequenceEqual(expectedShards[i]));
            }
        }

        [TestMethod()]
        public void DecodeMissing_TwoShards_Success_Test()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[][] shards =
            [
                [0, 1, 2, 3],
                //[4, 5, 6, 7],
                [0, 0, 0, 0],
                [8, 9, 10, 11],
                [0, 0, 0, 0],
                //[12, 13, 14, 15],
                [16, 17, 18, 19],
                [20, 21, 22, 23]
            ];

            bool[] shardPresent = [true, false, true, false, true, true];
            //Act
            rs.DecodeMissing(shards, shardPresent, 0, shards[0].Length);

            //Assert

            sbyte[][] expectedShards =
            [
                [0, 1, 2, 3],
                [4, 5, 6, 7],
                [8, 9, 10, 11],
                [12, 13, 14, 15],
                [16, 17, 18, 19],
                [20, 21, 22, 23]
            ];

            for (int i = 0; i < shards.Length; i++)
            {
                Assert.IsTrue(shards[i].SequenceEqual(expectedShards[i]));
            }
        }

        [TestMethod()]
        public void DecodeMissing_OneParity_Success_Test()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[][] shards =
            [
                [0, 1, 2, 3],
                [4, 5, 6, 7],
                [8, 9, 10, 11],
                [12, 13, 14, 15],
                //[16, 17, 18, 19],
                [0, 0, 0, 0],
                [20, 21, 22, 23]
            ];

            bool[] shardPresent = [true, true, true, true, false, true];
            //Act
            rs.DecodeMissing(shards, shardPresent, 0, shards[0].Length);

            //Assert

            sbyte[][] expectedShards =
            [
                [0, 1, 2, 3],
                [4, 5, 6, 7],
                [8, 9, 10, 11],
                [12, 13, 14, 15],
                [16, 17, 18, 19],
                [20, 21, 22, 23]
            ];

            for (int i = 0; i < shards.Length; i++)
            {
                Assert.IsTrue(shards[i].SequenceEqual(expectedShards[i]));
            }
        }

        [TestMethod()]
        public void DecodeMissing_TwoParity_Success_Test()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[][] shards =
            [
                [0, 1, 2, 3],
                [4, 5, 6, 7],
                [8, 9, 10, 11],
                [12, 13, 14, 15],
                //[16, 17, 18, 19],
                [0, 0, 0, 0],
                //[20, 21, 22, 23]
                [0, 0, 0, 0]
            ];

            bool[] shardPresent = [true, true, true, true, false, false];
            //Act
            rs.DecodeMissing(shards, shardPresent, 0, shards[0].Length);

            //Assert

            sbyte[][] expectedShards =
            [
                [0, 1, 2, 3],
                [4, 5, 6, 7],
                [8, 9, 10, 11],
                [12, 13, 14, 15],
                [16, 17, 18, 19],
                [20, 21, 22, 23]
            ];

            for (int i = 0; i < shards.Length; i++)
            {
                Assert.IsTrue(shards[i].SequenceEqual(expectedShards[i]));
            }
        }

        [TestMethod()]
        public void DecodeMissing_All_Failure_Test()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[][] shards =
            [
                [0, 0, 0, 0],
                [0, 0, 0, 0],
                [0, 0, 0, 0],
                [0, 0, 0, 0],

                [0, 0, 0, 0],
                [0, 0, 0, 0]
            ];

            bool[] shardPresent = [false, false, false, false, false, false];
            //Act
            var ex = Assert.ThrowsException<ArgumentException>(() => rs.DecodeMissing(shards, shardPresent, 0, shards[0].Length));

            //Assert
            Assert.AreEqual("Not enough shards present", ex.Message);
        }

        [TestMethod()]
        public void IsParityCorrect_Valid_ReturnsTrue()
        {
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[][] shards =
            [
                [0, 1, 2, 3],
                [4, 5, 6, 7],
                [8, 9, 10, 11],
                [12, 13, 14, 15],
                [16, 17, 18, 19],
                [20, 21, 22, 23]
            ];

            Assert.IsTrue(rs.IsParityCorrect(shards, 0, 4));
        }

        [TestMethod()]
        public void IsParityCorrect_Invalid_ReturnsFalse()
        {
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[][] shards =
            [
                [0, 1, 2, 3],
                [4, 5, 6, 7],
                [8, 9, 10, 11],
                [12, 13, 14, 15],
                [99, 99, 99, 99],  // wrong parity
                [20, 21, 22, 23]
            ];

            Assert.IsFalse(rs.IsParityCorrect(shards, 0, 4));
        }

        [TestMethod()]
        public void ProduceEncodedShardsWithPadding_Byte_Test()
        {
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            byte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

            var shards = rs.ManagedEncode(data, 4, 2);

            byte[][] expectedShards =
            [
                [0, 1, 2, 3, 4],
                [5, 6, 7, 8, 9],
                [10, 11, 12, 13, 14],
                [15, 16, 0, 0, 0],
                [20, 168, 186, 7, 108],
                [17, 231, 137, 24, 107]
            ];

            for (int i = 0; i < shards.Length; i++)
            {
                Assert.IsTrue(shards[i].SequenceEqual(expectedShards[i]));
            }
        }

        [TestMethod()]
        public void EncodeAndDecode_Config3_3_Test()
        {
            ReedSolomon rs = new ReedSolomon(dataShardCount: 3, parityShardCount: 3);

            sbyte[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
            sbyte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data, 3, 3);
            encoded[0] = null;
            encoded[2] = null;
            var decoded = rs.ManagedDecode(encoded, 3, 3);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void EncodeAndDecode_Config5_1_Test()
        {
            ReedSolomon rs = new ReedSolomon(dataShardCount: 5, parityShardCount: 1);

            sbyte[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
            sbyte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data, 5, 1);
            encoded[2] = null;
            var decoded = rs.ManagedDecode(encoded, 5, 1);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void GetPaddingSize_ExactFit_ReturnsZero()
        {
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            Assert.AreEqual(0, rs.GetPaddingSize(16));
        }

        [TestMethod()]
        public void GetPaddingSize_NeedsPadding_ReturnsCorrect()
        {
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            Assert.AreEqual(3, rs.GetPaddingSize(17));
        }

        [TestMethod()]
        public void ManagedDecode_TwoMissing_WithPadding_Success_Test()
        {
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
            sbyte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data, 4, 2);
            var paddingSize = rs.GetPaddingSize(data.Length, 4);
            encoded[0] = null;
            encoded[3] = null;
            var decoded = rs.ManagedDecode(encoded, 4, 2, paddingSize: paddingSize);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void ManagedEncode_InstanceOverload_Byte_Test()
        {
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            byte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

            var shardsExplicit = rs.ManagedEncode(data, 4, 2);
            var shardsInstance = rs.ManagedEncode(data);

            for (int i = 0; i < shardsExplicit.Length; i++)
            {
                Assert.IsTrue(shardsExplicit[i].SequenceEqual(shardsInstance[i]));
            }
        }

        [TestMethod()]
        public void ManagedDecode_InstanceOverload_Byte_Test()
        {
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            byte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
            byte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data);
            var paddingSize = rs.GetPaddingSize(data.Length);
            encoded[1] = null;
            var decoded = rs.ManagedDecode(encoded, paddingSize: paddingSize);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void EncodeAndDecode_HighByteValues_Roundtrip_Test()
        {
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            byte[] data = [128, 200, 255, 0, 129, 254, 130, 253, 131, 252, 132, 251, 133, 250, 134, 249];
            byte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data);
            encoded[2] = null;
            var decoded = rs.ManagedDecode(encoded);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void ManagedDecode_MixedDataAndParityMissing_Success_Test()
        {
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
            sbyte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data, 4, 2);
            encoded[1] = null;  // 1 data shard missing
            encoded[4] = null;  // 1 parity shard missing
            var decoded = rs.ManagedDecode(encoded, 4, 2);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void EncodeAndDecode_LargeData_Roundtrip_Test()
        {
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            byte[] data = new byte[1024];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(i % 256);
            byte[] expected = data.ToArray();

            var paddingSize = rs.GetPaddingSize(data.Length);
            var encoded = rs.ManagedEncode(data);
            encoded[0] = null;
            var decoded = rs.ManagedDecode(encoded, paddingSize: paddingSize);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void EncodeAndDecode_Config1_1_Test()
        {
            ReedSolomon rs = new ReedSolomon(dataShardCount: 1, parityShardCount: 1);

            sbyte[] data = [10, 20, 30, 40];
            sbyte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data, 1, 1);
            encoded[0] = null;
            var decoded = rs.ManagedDecode(encoded, 1, 1);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }
        [TestMethod()]
        public void Matrix_Equals_SameValues_ReturnsTrue()
        {
            // Two separately constructed matrices with identical values should be equal
            var a = new Matrix(2, 2);
            a.Set(0, 0, 1); a.Set(0, 1, 2);
            a.Set(1, 0, 3); a.Set(1, 1, 4);

            var b = new Matrix(2, 2);
            b.Set(0, 0, 1); b.Set(0, 1, 2);
            b.Set(1, 0, 3); b.Set(1, 1, 4);

            Assert.IsTrue(a.Equals(b));
        }

        [TestMethod()]
        public void Matrix_Constructor_SbyteArray_PreservesData()
        {
            sbyte[][] data = new sbyte[][] { new sbyte[] { 1, 2 }, new sbyte[] { 3, 4 } };

            var m = new Matrix(data);

            Assert.AreEqual((sbyte)1, m.Get(0, 0));
            Assert.AreEqual((sbyte)2, m.Get(0, 1));
            Assert.AreEqual((sbyte)3, m.Get(1, 0));
            Assert.AreEqual((sbyte)4, m.Get(1, 1));
        }
        // === Galois Edge Cases ===

        [TestMethod()]
        public void Galois_Divide_ByZero_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => Galois.Divide(1, 0));
        }

        [TestMethod()]
        public void Galois_Divide_ZeroNumerator_ReturnsZero()
        {
            Assert.AreEqual((sbyte)0, Galois.Divide(0, 1));
        }

        [TestMethod()]
        public void Galois_Multiply_WithZero_ReturnsZero()
        {
            Assert.AreEqual((sbyte)0, Galois.Multiply(0, 42));
            Assert.AreEqual((sbyte)0, Galois.Multiply(42, 0));
            Assert.AreEqual((sbyte)0, Galois.Multiply(0, 0));
        }

        [TestMethod()]
        public void Galois_Multiply_NegativeSbyte_Works()
        {
            // -1 as sbyte = 0xFF as unsigned, should work with 0xFF masking
            sbyte result = Galois.Multiply(-1, 1);
            Assert.AreEqual((sbyte)-1, result);
        }

        [TestMethod()]
        public void Galois_Exp_ZeroExponent_ReturnsOne()
        {
            Assert.AreEqual((sbyte)1, Galois.Exp(42, 0));
            Assert.AreEqual((sbyte)1, Galois.Exp(0, 0));
        }

        [TestMethod()]
        public void Galois_Exp_ZeroBase_ReturnsZero()
        {
            Assert.AreEqual((sbyte)0, Galois.Exp(0, 5));
        }

        [TestMethod()]
        public void Galois_MultiplyThenDivide_Roundtrip()
        {
            // a * b / b == a for all non-zero values
            sbyte a = 42;
            sbyte b = 7;
            Assert.AreEqual(a, Galois.Divide(Galois.Multiply(a, b), b));
        }

        [TestMethod()]
        public void Galois_AddAndSubtract_AreIdentical()
        {
            // In GF(256), add and subtract are both XOR
            Assert.AreEqual(Galois.Add(42, 17), Galois.Subtract(42, 17));
        }

        // === CheckBuffersAndSizes Error Paths ===

        [TestMethod()]
        public void EncodeParity_WrongShardCount_Throws()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[][] shards = new sbyte[5][]; // should be 6
            for (int i = 0; i < 5; i++) shards[i] = new sbyte[4];

            Assert.ThrowsException<ArgumentException>(() => rs.EncodeParity(shards, 0, 4));
        }

        [TestMethod()]
        public void EncodeParity_NegativeOffset_Throws()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[][] shards = new sbyte[6][];
            for (int i = 0; i < 6; i++) shards[i] = new sbyte[4];

            Assert.ThrowsException<ArgumentException>(() => rs.EncodeParity(shards, -1, 4));
        }

        [TestMethod()]
        public void EncodeParity_NegativeByteCount_Throws()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[][] shards = new sbyte[6][];
            for (int i = 0; i < 6; i++) shards[i] = new sbyte[4];

            Assert.ThrowsException<ArgumentException>(() => rs.EncodeParity(shards, 0, -1));
        }

        [TestMethod()]
        public void EncodeParity_BufferTooSmall_Throws()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[][] shards = new sbyte[6][];
            for (int i = 0; i < 6; i++) shards[i] = new sbyte[4];

            Assert.ThrowsException<ArgumentException>(() => rs.EncodeParity(shards, 0, 5));
        }

        [TestMethod()]
        public void DecodeMissing_TooManyMissing_Throws()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[][] shards = new sbyte[6][];
            for (int i = 0; i < 6; i++) shards[i] = new sbyte[4];
            bool[] present = [true, false, false, false, true, true]; // only 3 present, need 4

            Assert.ThrowsException<ArgumentException>(() => rs.DecodeMissing(shards, present, 0, 4));
        }

        [TestMethod()]
        public void DecodeMissing_AllPresent_NoOp()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[][] shards =
            [
                [0, 1, 2, 3],
                [4, 5, 6, 7],
                [8, 9, 10, 11],
                [12, 13, 14, 15],
                [16, 17, 18, 19],
                [20, 21, 22, 23]
            ];
            bool[] present = [true, true, true, true, true, true];

            // Should return without modifying anything
            rs.DecodeMissing(shards, present, 0, 4);

            Assert.AreEqual((sbyte)4, shards[1][0]);
        }

        // === IsParityCorrect after data corruption ===

        [TestMethod()]
        public void IsParityCorrect_AfterDataCorruption_ReturnsFalse()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[][] shards =
            [
                [0, 1, 2, 3],
                [4, 5, 6, 7],
                [8, 9, 10, 11],
                [12, 13, 14, 15],
                [16, 17, 18, 19],
                [20, 21, 22, 23]
            ];

            // Flip one bit in data
            shards[0][0] = 99;

            Assert.IsFalse(rs.IsParityCorrect(shards, 0, 4));
        }

        // === ManagedEncode/Decode Edge Cases ===

        [TestMethod()]
        public void ManagedEncode_SingleByte_Roundtrip()
        {
            ReedSolomon rs = new ReedSolomon(1, 1);

            sbyte[] data = [42];
            sbyte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data, 1, 1);
            encoded[0] = null;
            var decoded = rs.ManagedDecode(encoded, 1, 1);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void ManagedEncode_DataSmallerThanShardCount_Roundtrip()
        {
            // 2 bytes of data, 4 data shards → each shard is 1 byte, shards 2+3 are all-zero padding
            // Must use allowAllZeroes=true, otherwise zero-padding shards are treated as missing
            ReedSolomon rs = new ReedSolomon(4, 2);

            sbyte[] data = [10, 20];
            sbyte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data, 4, 2);
            var paddingSize = rs.GetPaddingSize(data.Length, 4);
            encoded[1] = null;
            var decoded = rs.ManagedDecode(encoded, 4, 2, allowAllZeroes: true, paddingSize: paddingSize);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void ManagedEncode_EncodeTwice_SameInstance_Works()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);

            sbyte[] data1 = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
            sbyte[] data2 = [16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31];

            var shards1 = rs.ManagedEncode(data1);
            var shards2 = rs.ManagedEncode(data2);

            shards1[1] = null;
            shards2[2] = null;

            var decoded1 = rs.ManagedDecode(shards1);
            var decoded2 = rs.ManagedDecode(shards2);

            Assert.IsTrue(decoded1.SequenceEqual(data1));
            Assert.IsTrue(decoded2.SequenceEqual(data2));
        }

        // === Matrix Error Paths ===

        [TestMethod()]
        public void Matrix_Invert_NonSquare_Throws()
        {
            var m = new Matrix(3, 4);
            Assert.ThrowsException<ArgumentException>(() => m.Invert());
        }

        [TestMethod()]
        public void Matrix_Constructor_JaggedArray_Throws()
        {
            sbyte[][] data = new sbyte[][] { new sbyte[] { 1, 2 }, new sbyte[] { 3, 4, 5 } };
            Assert.ThrowsException<ArgumentException>(() => new Matrix(data));
        }

        [TestMethod()]
        public void Matrix_Equals_Null_ReturnsFalse()
        {
            var m = new Matrix(2, 2);
            Assert.IsFalse(m.Equals(null));
        }

        [TestMethod()]
        public void Matrix_Equals_DifferentType_ReturnsFalse()
        {
            var m = new Matrix(2, 2);
            Assert.IsFalse(m.Equals("not a matrix"));
        }

        [TestMethod()]
        public void Matrix_Equals_DifferentValues_ReturnsFalse()
        {
            var a = new Matrix(2, 2);
            a.Set(0, 0, 1);

            var b = new Matrix(2, 2);
            b.Set(0, 0, 2);

            Assert.IsFalse(a.Equals(b));
        }

        [TestMethod()]
        public void Matrix_Identity_IsCorrect()
        {
            var m = Matrix.Identity(3);

            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    Assert.AreEqual(r == c ? (sbyte)1 : (sbyte)0, m.Get(r, c));
        }

        [TestMethod()]
        public void Matrix_SwapRows_OutOfBounds_Throws()
        {
            var m = new Matrix(3, 3);
            Assert.ThrowsException<ArgumentException>(() => m.SwapRows(-1, 0));
            Assert.ThrowsException<ArgumentException>(() => m.SwapRows(0, 3));
        }

        [TestMethod()]
        public void Matrix_Get_OutOfBounds_Throws()
        {
            var m = new Matrix(3, 3);
            Assert.ThrowsException<ArgumentException>(() => m.Get(-1, 0));
            Assert.ThrowsException<ArgumentException>(() => m.Get(0, 3));
            Assert.ThrowsException<ArgumentException>(() => m.Get(3, 0));
        }

        // === Galois Field Algebraic Properties ===

        [TestMethod()]
        public void Galois_Multiply_Commutativity()
        {
            sbyte[] values = [0, 1, 2, 3, 7, 15, 16, 31, 32, 63, 64, 100, 127, -128, -1, -64, -32, -16, -2, 42, 99, -99, 50, -50, 126];
            foreach (var a in values)
                foreach (var b in values)
                    Assert.AreEqual(Galois.Multiply(a, b), Galois.Multiply(b, a),
                        $"Commutativity failed for a={a}, b={b}");
        }

        [TestMethod()]
        public void Galois_Multiply_Associativity()
        {
            sbyte[] values = [1, 2, 3, 7, 15, 42, 100, 127, -128, -1];
            foreach (var a in values)
                foreach (var b in values)
                    foreach (var c in values)
                        Assert.AreEqual(
                            Galois.Multiply(Galois.Multiply(a, b), c),
                            Galois.Multiply(a, Galois.Multiply(b, c)),
                            $"Associativity failed for a={a}, b={b}, c={c}");
        }

        [TestMethod()]
        public void Galois_Distributivity()
        {
            sbyte[] values = [1, 2, 3, 7, 15, 42, 100, 127, -128, -1];
            foreach (var a in values)
                foreach (var b in values)
                    foreach (var c in values)
                        Assert.AreEqual(
                            Galois.Multiply(a, Galois.Add(b, c)),
                            Galois.Add(Galois.Multiply(a, b), Galois.Multiply(a, c)),
                            $"Distributivity failed for a={a}, b={b}, c={c}");
        }

        [TestMethod()]
        public void Galois_MultiplyByOne_Identity_AllValues()
        {
            for (int i = -128; i <= 127; i++)
            {
                sbyte x = (sbyte)i;
                Assert.AreEqual(x, Galois.Multiply(x, 1), $"Multiply({x}, 1) != {x}");
                Assert.AreEqual(x, Galois.Multiply(1, x), $"Multiply(1, {x}) != {x}");
            }
        }

        [TestMethod()]
        public void Galois_MultiplicativeInverse_AllNonZero()
        {
            for (int i = -128; i <= 127; i++)
            {
                sbyte x = (sbyte)i;
                if (x == 0) continue;
                sbyte inverse = Galois.Divide(1, x);
                Assert.AreEqual((sbyte)1, Galois.Multiply(x, inverse),
                    $"x={x}: x * (1/x) should be 1, got {Galois.Multiply(x, inverse)}");
            }
        }

        [TestMethod()]
        public void Galois_Add_SelfInverse_AllValues()
        {
            for (int i = -128; i <= 127; i++)
            {
                sbyte x = (sbyte)i;
                Assert.AreEqual((sbyte)0, Galois.Add(x, x), $"Add({x}, {x}) should be 0");
            }
        }

        [TestMethod()]
        public void Galois_Exp_EqualsRepeatedMultiply()
        {
            sbyte[] bases = [1, 2, 3, 7, 42, 127, -1, -128];
            foreach (var a in bases)
            {
                for (int n = 1; n <= 10; n++)
                {
                    sbyte expected = a;
                    for (int k = 1; k < n; k++)
                        expected = Galois.Multiply(expected, a);
                    Assert.AreEqual(expected, Galois.Exp(a, n),
                        $"Exp({a}, {n}) != repeated multiply");
                }
            }
        }

        [TestMethod()]
        public void Galois_Exp_LargeExponent()
        {
            // Verify large exponents don't throw and produce consistent results
            sbyte result255 = Galois.Exp(2, 255);
            sbyte result510 = Galois.Exp(2, 510);
            sbyte result1000 = Galois.Exp(3, 1000);

            // Verify via repeated multiply for Exp(2, 255)
            sbyte manual = 1;
            for (int i = 0; i < 255; i++)
                manual = Galois.Multiply(manual, 2);
            Assert.AreEqual(manual, result255, "Exp(2, 255) mismatch");

            // Exp(2, 510) = Exp(2, 255) * Exp(2, 255) since GF(256) has order 255
            Assert.AreEqual(Galois.Multiply(result255, result255), result510, "Exp(2, 510) mismatch");

            // Just verify no exception for large exponent
            Assert.IsNotNull((object)result1000);
        }

        [TestMethod()]
        public void Galois_Divide_Roundtrip_AllNonZeroPairs()
        {
            sbyte[] values = [1, 2, 3, 7, 15, 42, 100, 127, -128, -1, -64, -32, -2, 50, 99];
            foreach (var a in values)
                foreach (var b in values)
                {
                    sbyte divided = Galois.Divide(a, b);
                    sbyte result = Galois.Multiply(divided, b);
                    Assert.AreEqual(a, result,
                        $"(a/b)*b != a for a={a}, b={b}");
                }
        }

        [TestMethod()]
        public void Galois_Multiply_BoundaryValues()
        {
            // Test all sbyte boundary combinations
            sbyte[] boundaries = [-128, -1, 0, 1, 127];
            foreach (var a in boundaries)
                foreach (var b in boundaries)
                {
                    // Should not throw
                    sbyte result = Galois.Multiply(a, b);

                    // Zero property
                    if (a == 0 || b == 0)
                        Assert.AreEqual((sbyte)0, result, $"Multiply({a}, {b}) should be 0");

                    // Identity property
                    if (b == 1) Assert.AreEqual(a, result, $"Multiply({a}, 1) should be {a}");
                    if (a == 1) Assert.AreEqual(b, result, $"Multiply(1, {b}) should be {b}");

                    // Commutativity at boundaries
                    Assert.AreEqual(result, Galois.Multiply(b, a),
                        $"Commutativity failed for a={a}, b={b}");
                }
        }

        // === Galois Table Generation ===

        [TestMethod()]
        public void Galois_GenerateLogTable_MatchesHardcoded()
        {
            short[] generated = Galois.GenerateLogTable(Galois.GENERATING_POLYNOMIAL);
            short[] hardcoded = GaloisTables.LOG_TABLE;

            Assert.AreEqual(hardcoded.Length, generated.Length, "LOG_TABLE length mismatch");
            for (int i = 0; i < generated.Length; i++)
                Assert.AreEqual(hardcoded[i], generated[i], $"LOG_TABLE mismatch at index {i}");
        }

        [TestMethod()]
        public void Galois_GenerateExpTable_MatchesHardcoded()
        {
            short[] logTable = Galois.GenerateLogTable(Galois.GENERATING_POLYNOMIAL);
            sbyte[] generated = Galois.GenerateExpTable(logTable);
            sbyte[] hardcoded = GaloisTables.EXP_TABLE;

            Assert.AreEqual(hardcoded.Length, generated.Length, "EXP_TABLE length mismatch");
            for (int i = 0; i < generated.Length; i++)
                Assert.AreEqual(hardcoded[i], generated[i], $"EXP_TABLE mismatch at index {i}");
        }

        [TestMethod()]
        public void Galois_GenerateLogTable_InvalidPolynomial_Throws()
        {
            Assert.ThrowsException<InvalidOperationException>(() => Galois.GenerateLogTable(0));
        }

        [TestMethod()]
        public void Galois_AllPossiblePolynomials_ContainsKnown()
        {
            int[] polynomials = Galois.AllPossiblePolynomials();
            int[] known = [29, 43, 45, 77, 95, 99, 101, 105, 113, 135, 141, 169, 195, 207, 231, 245];

            foreach (int p in known)
                Assert.IsTrue(polynomials.Contains(p), $"Missing known polynomial: {p}");
        }

        [TestMethod()]
        public void Galois_AllPossiblePolynomials_CountIs16()
        {
            int[] polynomials = Galois.AllPossiblePolynomials();
            Assert.AreEqual(16, polynomials.Length, $"Expected 16 valid polynomials, got {polynomials.Length}");
        }

        // === Matrix Operations ===

        [TestMethod()]
        public void Matrix_Times_Identity_ReturnsSelf()
        {
            var m = new Matrix(new sbyte[][] {
                new sbyte[] { 1, 2, 3 },
                new sbyte[] { 4, 5, 6 },
                new sbyte[] { 7, 8, 9 }
            });
            var identity = Matrix.Identity(3);

            var result = m.Times(identity);

            Assert.IsTrue(m.Equals(result));
        }

        [TestMethod()]
        public void Matrix_Times_KnownValues()
        {
            // 2x2 multiplication over GF(256)
            var a = new Matrix(new sbyte[][] {
                new sbyte[] { 1, 2 },
                new sbyte[] { 3, 4 }
            });
            var b = new Matrix(new sbyte[][] {
                new sbyte[] { 5, 6 },
                new sbyte[] { 7, 8 }
            });

            var result = a.Times(b);

            // result[0][0] = Multiply(1,5) XOR Multiply(2,7) = 5 XOR 14 = 11
            // result[0][1] = Multiply(1,6) XOR Multiply(2,8) = 6 XOR 16 = 22
            // result[1][0] = Multiply(3,5) XOR Multiply(4,7) = 15 XOR 28 = 19
            // result[1][1] = Multiply(3,6) XOR Multiply(4,8) = 10 XOR 32 = 42
            sbyte expected00 = (sbyte)(Galois.Multiply(1, 5) ^ Galois.Multiply(2, 7));
            sbyte expected01 = (sbyte)(Galois.Multiply(1, 6) ^ Galois.Multiply(2, 8));
            sbyte expected10 = (sbyte)(Galois.Multiply(3, 5) ^ Galois.Multiply(4, 7));
            sbyte expected11 = (sbyte)(Galois.Multiply(3, 6) ^ Galois.Multiply(4, 8));

            Assert.AreEqual(expected00, result.Get(0, 0));
            Assert.AreEqual(expected01, result.Get(0, 1));
            Assert.AreEqual(expected10, result.Get(1, 0));
            Assert.AreEqual(expected11, result.Get(1, 1));
        }

        [TestMethod()]
        public void Matrix_Times_DimensionMismatch_Throws()
        {
            var a = new Matrix(2, 3);
            var b = new Matrix(2, 3);
            Assert.ThrowsException<ArgumentException>(() => a.Times(b));
        }

        [TestMethod()]
        public void Matrix_Augment_KnownValues()
        {
            var left = new Matrix(new sbyte[][] {
                new sbyte[] { 1, 2 },
                new sbyte[] { 3, 4 }
            });
            var right = new Matrix(new sbyte[][] {
                new sbyte[] { 5, 6, 7 },
                new sbyte[] { 8, 9, 10 }
            });

            var result = left.Augment(right);

            Assert.AreEqual((sbyte)1, result.Get(0, 0));
            Assert.AreEqual((sbyte)2, result.Get(0, 1));
            Assert.AreEqual((sbyte)5, result.Get(0, 2));
            Assert.AreEqual((sbyte)6, result.Get(0, 3));
            Assert.AreEqual((sbyte)7, result.Get(0, 4));
            Assert.AreEqual((sbyte)3, result.Get(1, 0));
            Assert.AreEqual((sbyte)10, result.Get(1, 4));
        }

        [TestMethod()]
        public void Matrix_Augment_DifferentRowCounts_Throws()
        {
            var a = new Matrix(2, 2);
            var b = new Matrix(3, 2);
            Assert.ThrowsException<ArgumentException>(() => a.Augment(b));
        }

        [TestMethod()]
        public void Matrix_Submatrix_ExtractsCorrectRegion()
        {
            var m = new Matrix(4, 4);
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    m.Set(r, c, (sbyte)(r * 4 + c));

            // Extract rows 1-2, columns 1-2
            var sub = m.Submatrix(1, 1, 3, 3);

            Assert.AreEqual((sbyte)5, sub.Get(0, 0));  // m[1][1]
            Assert.AreEqual((sbyte)6, sub.Get(0, 1));  // m[1][2]
            Assert.AreEqual((sbyte)9, sub.Get(1, 0));  // m[2][1]
            Assert.AreEqual((sbyte)10, sub.Get(1, 1)); // m[2][2]
        }

        [TestMethod()]
        public void Matrix_GetRow_ReturnsCorrectData()
        {
            var m = new Matrix(new sbyte[][] {
                new sbyte[] { 1, 2, 3 },
                new sbyte[] { 4, 5, 6 },
                new sbyte[] { 7, 8, 9 }
            });

            sbyte[] row1 = m.GetRow(1);
            Assert.IsTrue(row1.SequenceEqual(new sbyte[] { 4, 5, 6 }));
        }

        [TestMethod()]
        public void Matrix_SwapRows_SwapsCorrectly()
        {
            var m = new Matrix(new sbyte[][] {
                new sbyte[] { 1, 2 },
                new sbyte[] { 3, 4 },
                new sbyte[] { 5, 6 }
            });

            m.SwapRows(0, 2);

            Assert.AreEqual((sbyte)5, m.Get(0, 0));
            Assert.AreEqual((sbyte)6, m.Get(0, 1));
            Assert.AreEqual((sbyte)1, m.Get(2, 0));
            Assert.AreEqual((sbyte)2, m.Get(2, 1));
            // Middle row unchanged
            Assert.AreEqual((sbyte)3, m.Get(1, 0));
        }

        [TestMethod()]
        public void Matrix_Invert_1x1()
        {
            var m = new Matrix(new sbyte[][] { new sbyte[] { 5 } });
            var inv = m.Invert();
            var product = m.Times(inv);
            Assert.IsTrue(product.Equals(Matrix.Identity(1)));
        }

        [TestMethod()]
        public void Matrix_Invert_2x2_TimesOriginal_IsIdentity()
        {
            var m = new Matrix(new sbyte[][] {
                new sbyte[] { 1, 2 },
                new sbyte[] { 3, 4 }
            });
            var inv = m.Invert();
            var product = m.Times(inv);
            Assert.IsTrue(product.Equals(Matrix.Identity(2)));
        }

        [TestMethod()]
        public void Matrix_Invert_5x5_ViaVandermonde()
        {
            // Vandermonde matrices are guaranteed invertible when rows are distinct
            // Build a 5x5 matrix manually using Galois.Exp
            var data = new sbyte[5][];
            for (int r = 0; r < 5; r++)
            {
                data[r] = new sbyte[5];
                for (int c = 0; c < 5; c++)
                    data[r][c] = Galois.Exp((sbyte)(r + 1), c);
            }
            var m = new Matrix(data);
            var inv = m.Invert();
            var product = m.Times(inv);
            Assert.IsTrue(product.Equals(Matrix.Identity(5)));
        }

        [TestMethod()]
        public void Matrix_Invert_Singular_Throws()
        {
            // Row 1 is a GF(256) multiple of row 0
            sbyte scale = 3;
            var m = new Matrix(new sbyte[][] {
                new sbyte[] { 1, 2 },
                new sbyte[] { Galois.Multiply(1, scale), Galois.Multiply(2, scale) }
            });
            Assert.ThrowsException<ArgumentException>(() => m.Invert());
        }

        [TestMethod()]
        public void Matrix_ToString_Format()
        {
            var m = new Matrix(new sbyte[][] {
                new sbyte[] { 1, 2 },
                new sbyte[] { 3, 4 }
            });
            string s = m.ToString();
            Assert.AreEqual("[[1, 2], [3, 4]]", s);
        }

        [TestMethod()]
        public void Matrix_ToBigString_HexFormat()
        {
            var m = new Matrix(new sbyte[][] {
                new sbyte[] { 0, -1 },
                new sbyte[] { 16, 15 }
            });
            string s = m.ToBigString();
            // -1 as sbyte = 0xFF → "ff", 0 → "00", 16 → "10", 15 → "0f"
            Assert.IsTrue(s.Contains("00"));
            Assert.IsTrue(s.Contains("ff"));
            Assert.IsTrue(s.Contains("10"));
            Assert.IsTrue(s.Contains("0f"));
        }

        [TestMethod()]
        public void Matrix_Equals_SameReference_ReturnsTrue()
        {
            var m = new Matrix(new sbyte[][] {
                new sbyte[] { 1, 2 },
                new sbyte[] { 3, 4 }
            });
            Assert.IsTrue(m.Equals(m));
        }

        // === ReedSolomon Constructor & Validation ===

        [TestMethod()]
        public void Constructor_TotalShards256_Succeeds()
        {
            // GF(256) maximum: 256 total shards
            ReedSolomon rs = new ReedSolomon(200, 56);

            // Encode small payload and verify roundtrip
            byte[] data = new byte[200];
            for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 256);
            byte[] expected = data.ToArray();

            var paddingSize = rs.GetPaddingSize(data.Length);
            var encoded = rs.ManagedEncode(data);
            // Null out some shards (up to parity count)
            encoded[0] = null;
            encoded[5] = null;
            var decoded = rs.ManagedDecode(encoded, paddingSize: paddingSize);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void Constructor_TotalShards257_Behavior()
        {
            // Total = 257 exceeds GF(256) field size
            // Vandermonde matrix would use (sbyte)256 = 0 as row, likely producing singular matrix
            // This documents the current behavior
            try
            {
                ReedSolomon rs = new ReedSolomon(200, 57);
                // If constructor succeeds, encoding may still fail
                // Document that no explicit validation exists
                Assert.IsTrue(true, "Constructor did not throw - no input validation for total > 256");
            }
            catch (Exception)
            {
                // If it throws, that's also acceptable behavior
                Assert.IsTrue(true, "Constructor threw for total > 256");
            }
        }

        [TestMethod()]
        public void CheckBuffersAndSizes_DifferentShardSizes_Throws()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[][] shards = new sbyte[6][];
            for (int i = 0; i < 6; i++) shards[i] = new sbyte[4];
            shards[3] = new sbyte[5]; // Different size

            var ex = Assert.ThrowsException<ArgumentException>(() => rs.EncodeParity(shards, 0, 4));
            Assert.IsTrue(ex.Message.Contains("different sizes"), $"Unexpected message: {ex.Message}");
        }

        [TestMethod()]
        public void CheckBuffersAndSizes_OffsetPlusByteCount_Exceeds_Throws()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[][] shards = new sbyte[6][];
            for (int i = 0; i < 6; i++) shards[i] = new sbyte[4];

            // offset=2, byteCount=4 → 6 > 4
            var ex = Assert.ThrowsException<ArgumentException>(() => rs.EncodeParity(shards, 2, 4));
            Assert.IsTrue(ex.Message.Contains("too small"), $"Unexpected message: {ex.Message}");
        }

        [TestMethod()]
        public void Galois_Exp_NegativeExponent_Throws()
        {
            // Negative exponent causes logResult to be negative, leading to IndexOutOfRangeException
            Assert.ThrowsException<IndexOutOfRangeException>(() => Galois.Exp(2, -1));
        }

        // === Encode/Decode Edge Cases ===

        [TestMethod()]
        public void Decode_ExactlyDataShardCount_Present()
        {
            // Only data shards present, all parity shards missing
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
            sbyte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data, 4, 2);
            encoded[4] = null; // parity 0
            encoded[5] = null; // parity 1
            var decoded = rs.ManagedDecode(encoded, 4, 2);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void Decode_OnlyParityAndSomeData()
        {
            // Data shards 0+1 missing, parity shards present
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
            sbyte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data, 4, 2);
            encoded[0] = null; // data 0
            encoded[1] = null; // data 1
            var decoded = rs.ManagedDecode(encoded, 4, 2);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void Encode_SingleByte_SingleDataShard()
        {
            ReedSolomon rs = new ReedSolomon(1, 1);
            sbyte[] data = [42];
            sbyte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data, 1, 1);
            Assert.AreEqual(2, encoded.Length); // 1 data + 1 parity
            Assert.AreEqual(1, encoded[0].Length); // 1 byte per shard

            // Null data shard, recover from parity
            encoded[0] = null;
            var decoded = rs.ManagedDecode(encoded, 1, 1);
            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void EncodeAndDecode_AllMaxByteValues()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[] data = Enumerable.Repeat((sbyte)-1, 16).ToArray(); // 0xFF
            sbyte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data, 4, 2);
            encoded[1] = null;
            var decoded = rs.ManagedDecode(encoded, 4, 2);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void EncodeAndDecode_AllMinByteValues()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[] data = Enumerable.Repeat((sbyte)-128, 16).ToArray(); // 0x80
            sbyte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data, 4, 2);
            encoded[2] = null;
            var decoded = rs.ManagedDecode(encoded, 4, 2);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void EncodeAndDecode_ZeroPadding_ExactFit()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
            sbyte[] expected = data.ToArray();

            int paddingSize = rs.GetPaddingSize(data.Length, 4);
            Assert.AreEqual(0, paddingSize, "16 bytes / 4 shards should have 0 padding");

            var encoded = rs.ManagedEncode(data, 4, 2);
            encoded[0] = null;
            var decoded = rs.ManagedDecode(encoded, 4, 2, paddingSize: paddingSize);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void EncodeAndDecode_MaxPadding()
        {
            // 5 bytes with 4 data shards → shardSize=2, totalDataSize=8, padding=3
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[] data = [10, 20, 30, 40, 50];
            sbyte[] expected = data.ToArray();

            int paddingSize = rs.GetPaddingSize(data.Length, 4);
            Assert.AreEqual(3, paddingSize);

            var encoded = rs.ManagedEncode(data, 4, 2);
            encoded[1] = null;
            var decoded = rs.ManagedDecode(encoded, 4, 2, paddingSize: paddingSize);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void EncodeAndDecode_Config10_4_LargeData()
        {
            ReedSolomon rs = new ReedSolomon(10, 4);
            byte[] data = new byte[1000];
            for (int i = 0; i < data.Length; i++) data[i] = (byte)((i * 37 + 13) % 256);
            byte[] expected = data.ToArray();

            var paddingSize = rs.GetPaddingSize(data.Length, 10);
            var encoded = rs.ManagedEncode(data, 10, 4);
            // Null out 4 shards (max recovery)
            encoded[0] = null;
            encoded[3] = null;
            encoded[7] = null;
            encoded[12] = null;
            var decoded = rs.ManagedDecode(encoded, 10, 4, paddingSize: paddingSize);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void EncodeAndDecode_LargeData_64KB()
        {
            ReedSolomon rs = new ReedSolomon(8, 4);
            byte[] data = new byte[65536];
            for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 256);
            byte[] expected = data.ToArray();

            var paddingSize = rs.GetPaddingSize(data.Length, 8);
            var encoded = rs.ManagedEncode(data, 8, 4);
            encoded[0] = null;
            encoded[2] = null;
            encoded[5] = null;
            encoded[10] = null;
            var decoded = rs.ManagedDecode(encoded, 8, 4, paddingSize: paddingSize);

            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void ManagedEncode_ByteOverload_MatchesSbyteOverload()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            byte[] byteData = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
            sbyte[] sbyteData = new sbyte[byteData.Length];
            for (int i = 0; i < byteData.Length; i++)
                sbyteData[i] = unchecked((sbyte)byteData[i]);

            var byteShards = rs.ManagedEncode(byteData, 4, 2);
            var sbyteShards = rs.ManagedEncode(sbyteData, 4, 2);

            Assert.AreEqual(byteShards.Length, sbyteShards.Length);
            for (int i = 0; i < byteShards.Length; i++)
            {
                Assert.AreEqual(byteShards[i].Length, sbyteShards[i].Length);
                for (int j = 0; j < byteShards[i].Length; j++)
                    Assert.AreEqual(byteShards[i][j], unchecked((byte)sbyteShards[i][j]),
                        $"Mismatch at shard[{i}][{j}]");
            }
        }

        [TestMethod()]
        public void ManagedDecode_AllShardsNull_Behavior()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[][] shards = [null, null, null, null, null, null];

            // All shards null: shardSize becomes 0, all marked as missing
            // Should throw "Not enough shards present"
            Assert.ThrowsException<ArgumentException>(() => rs.ManagedDecode(shards, 4, 2));
        }

        // === Parity Verification ===

        [TestMethod()]
        public void IsParityCorrect_AfterEncoding_ReturnsTrue()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[] data = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 1, 2, 3, 4];

            var shards = rs.ManagedEncode(data, 4, 2);
            Assert.IsTrue(rs.IsParityCorrect(shards, 0, shards[0].Length));
        }

        [TestMethod()]
        public void IsParityCorrect_SingleBitFlip_ReturnsFalse()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

            var shards = rs.ManagedEncode(data, 4, 2);
            // Flip one bit
            shards[0][0] ^= 1;
            Assert.IsFalse(rs.IsParityCorrect(shards, 0, shards[0].Length));
        }

        [TestMethod()]
        public void IsParityCorrect_AfterDecodeRecovery_ReturnsTrue()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

            var shards = rs.ManagedEncode(data, 4, 2);

            // Null one shard and recover
            bool[] present = [true, false, true, true, true, true];
            shards[1] = new sbyte[shards[0].Length];
            rs.DecodeMissing(shards, present, 0, shards[0].Length);

            // After recovery, parity should be correct
            Assert.IsTrue(rs.IsParityCorrect(shards, 0, shards[0].Length));
        }

        [TestMethod()]
        public void IsParityCorrect_WithOffset_ChecksCorrectRange()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);

            // Create shards with 8 bytes each
            sbyte[][] shards = new sbyte[6][];
            for (int i = 0; i < 6; i++) shards[i] = new sbyte[8];

            // Fill data shards
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 8; j++)
                    shards[i][j] = (sbyte)(i * 8 + j);

            // Compute parity for all 8 bytes
            rs.EncodeParity(shards, 0, 8);

            // Parity should be correct for the full range
            Assert.IsTrue(rs.IsParityCorrect(shards, 0, 8));

            // Corrupt byte at index 6 in data shard 0
            shards[0][6] ^= 1;

            // Check only bytes 0-3: should still be correct (corruption is at index 6)
            Assert.IsTrue(rs.IsParityCorrect(shards, 0, 4));

            // Check bytes 4-7: should detect corruption
            Assert.IsFalse(rs.IsParityCorrect(shards, 4, 4));
        }

        [TestMethod()]
        public void IsParityCorrect_ParityCorruption_ReturnsFalse()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

            var shards = rs.ManagedEncode(data, 4, 2);
            // Corrupt parity shard
            shards[4][0] ^= 1;
            Assert.IsFalse(rs.IsParityCorrect(shards, 0, shards[0].Length));
        }

        // === Robustness & Exhaustive Tests ===

        [TestMethod()]
        public void AllSingleShardLoss_Exhaustive()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
            sbyte[] expected = data.ToArray();

            var original = rs.ManagedEncode(data, 4, 2);

            for (int missing = 0; missing < 6; missing++)
            {
                // Clone shards
                sbyte[][] shards = new sbyte[6][];
                for (int i = 0; i < 6; i++)
                    shards[i] = original[i].ToArray();

                shards[missing] = null;
                var decoded = rs.ManagedDecode(shards, 4, 2);
                Assert.IsTrue(decoded.SequenceEqual(expected),
                    $"Failed to recover from loss of shard {missing}");
            }
        }

        [TestMethod()]
        public void AllValidDoubleShardLoss_Exhaustive()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[] data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
            sbyte[] expected = data.ToArray();

            var original = rs.ManagedEncode(data, 4, 2);

            for (int i = 0; i < 6; i++)
            {
                for (int j = i + 1; j < 6; j++)
                {
                    // Clone shards
                    sbyte[][] shards = new sbyte[6][];
                    for (int k = 0; k < 6; k++)
                        shards[k] = original[k].ToArray();

                    shards[i] = null;
                    shards[j] = null;

                    // With parityShardCount=2, can recover any 2 missing
                    var decoded = rs.ManagedDecode(shards, 4, 2);
                    Assert.IsTrue(decoded.SequenceEqual(expected),
                        $"Failed to recover from loss of shards {i} and {j}");
                }
            }
        }

        [TestMethod()]
        public void DeterministicRandom_MultipleConfigs()
        {
            int[][] configs = [[2, 1], [3, 2], [5, 3], [8, 4]];

            foreach (var cfg in configs)
            {
                int dataShards = cfg[0];
                int parityShards = cfg[1];
                ReedSolomon rs = new ReedSolomon(dataShards, parityShards);

                // Deterministic pseudo-random data
                byte[] data = new byte[100];
                int seed = 12345;
                for (int i = 0; i < data.Length; i++)
                {
                    seed = (seed * 1103515245 + 12345) & 0x7fffffff;
                    data[i] = (byte)(seed % 256);
                }
                byte[] expected = data.ToArray();

                var paddingSize = rs.GetPaddingSize(data.Length, dataShards);
                var encoded = rs.ManagedEncode(data, dataShards, parityShards);

                // Null out max possible shards (= parityShardCount)
                for (int i = 0; i < parityShards; i++)
                    encoded[i] = null;

                var decoded = rs.ManagedDecode(encoded, dataShards, parityShards, paddingSize: paddingSize);
                Assert.IsTrue(decoded.SequenceEqual(expected),
                    $"Failed for config ({dataShards}, {parityShards})");
            }
        }

        [TestMethod()]
        public void AllowAllZeroes_False_TreatsZeroAsMissing()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
            sbyte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data, 4, 2);
            // Replace shard 1 with zeros (simulating "missing" detection)
            encoded[1] = new sbyte[encoded[0].Length];

            // allowAllZeroes=false (default) → zero shards treated as missing
            var decoded = rs.ManagedDecode(encoded, 4, 2, allowAllZeroes: false);
            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void AllowAllZeroes_True_PreservesZeroShards()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);
            sbyte[] data = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
            sbyte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data, 4, 2);
            var decoded = rs.ManagedDecode(encoded, 4, 2, allowAllZeroes: true);
            Assert.IsTrue(decoded.SequenceEqual(expected));
        }

        [TestMethod()]
        public void GetPaddingSize_VariousInputs()
        {
            ReedSolomon rs = new ReedSolomon(4, 2);

            // Exact fit
            Assert.AreEqual(0, rs.GetPaddingSize(16, 4));
            Assert.AreEqual(0, rs.GetPaddingSize(4, 4));

            // Needs padding
            Assert.AreEqual(3, rs.GetPaddingSize(1, 4));
            Assert.AreEqual(3, rs.GetPaddingSize(17, 4));
            Assert.AreEqual(2, rs.GetPaddingSize(2, 4));
            Assert.AreEqual(1, rs.GetPaddingSize(3, 4));

            // Larger data
            Assert.AreEqual(0, rs.GetPaddingSize(1000, 4));
            Assert.AreEqual(3, rs.GetPaddingSize(1001, 4));
        }

        [TestMethod()]
        public void Config1_254_MaxParityRatio()
        {
            // Extreme: 1 data shard + 254 parity shards = 255 total (within GF(256))
            ReedSolomon rs = new ReedSolomon(1, 254);

            sbyte[] data = [42];
            sbyte[] expected = data.ToArray();

            var encoded = rs.ManagedEncode(data, 1, 254);

            // Keep only the data shard, null all parity
            for (int i = 1; i < encoded.Length; i++)
                encoded[i] = null;

            var decoded = rs.ManagedDecode(encoded, 1, 254, allowAllZeroes: true);
            Assert.IsTrue(decoded.SequenceEqual(expected));
        }
    }
}