using Microsoft.VisualStudio.TestTools.UnitTesting;
using Witteborn.ReedSolomon;

namespace ReedSolomonTests
{
    [TestClass()]
    public class ReedSolomonTests
    {
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
    }
}