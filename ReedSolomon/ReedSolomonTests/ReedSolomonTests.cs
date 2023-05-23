using Witteborn.ReedSolomon;

namespace ReedSolomonTests
{
    [TestClass()]
    public class ReedSolomonTests
    {
        //[TestMethod()]
        //public void ReedSolomonTest()
        //{
        //    Assert.Fail();
        //}

        [TestMethod()]
        public void EncodeParityTest()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[] data = new sbyte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

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
            sbyte[][] expectedShards = new sbyte[][]
{
                new sbyte[] {0,1,2,3 },
                new sbyte[] {4,5,6,7},
                new sbyte[] {8,9,10,11 },
                new sbyte[] {12,13,14,15 },
                new sbyte[] {16,17,18,19 },
                new sbyte[] {20,21,22,23 }
};

            for (int i = 0; i < shards.Length; i++)
            {
                Assert.IsTrue(shards[i].SequenceEqual(expectedShards[i]));
            }
        }

        [TestMethod()]
        public void DecodeMissing_OneShard_Success_Test()
        {
            //Arrange
            ReedSolomon rs = new ReedSolomon(dataShardCount: 4, parityShardCount: 2);

            sbyte[][] shards = new sbyte[][]
            {
                new sbyte[] {0,1,2,3 },
                //new sbyte[] {4,5,6,7},
                new sbyte[] {0,0,0,0},
                new sbyte[] {8,9,10,11 },
                new sbyte[] {12,13,14,15 },
                new sbyte[] {16,17,18,19 },
                new sbyte[] {20,21,22,23 }
            };


            bool[] shardPresent = new bool[] { true, false, true, true, true, true };
            //Act
            rs.DecodeMissing(shards, shardPresent, 0, shards[0].Length);


            //Assert

            sbyte[][] expectedShards = new sbyte[][]
            {
                new sbyte[] {0,1,2,3 },
                new sbyte[] {4,5,6,7},
                new sbyte[] {8,9,10,11 },
                new sbyte[] {12,13,14,15 },
                new sbyte[] {16,17,18,19 },
                new sbyte[] {20,21,22,23 }
            };

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

            sbyte[][] shards = new sbyte[][]
            {
                new sbyte[] {0,1,2,3 },
                //new sbyte[] {4,5,6,7},
                new sbyte[] {0,0,0,0},
                new sbyte[] {8,9,10,11 },
                new sbyte[] {0,0,0,0},
                //new sbyte[] {12,13,14,15 },
                new sbyte[] {16,17,18,19 },
                new sbyte[] {20,21,22,23 }
            };


            bool[] shardPresent = new bool[] { true, false, true, false, true, true };
            //Act
            rs.DecodeMissing(shards, shardPresent, 0, shards[0].Length);


            //Assert

            sbyte[][] expectedShards = new sbyte[][]
            {
                new sbyte[] {0,1,2,3 },
                new sbyte[] {4,5,6,7},
                new sbyte[] {8,9,10,11 },
                new sbyte[] {12,13,14,15 },
                new sbyte[] {16,17,18,19 },
                new sbyte[] {20,21,22,23 }
            };

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

            sbyte[][] shards = new sbyte[][]
            {
                new sbyte[] {0,1,2,3 },
                new sbyte[] {4,5,6,7},
                new sbyte[] {8,9,10,11 },
                new sbyte[] {12,13,14,15 },
                //new sbyte[] {16,17,18,19 },
                new sbyte[] {0,0,0,0},
                new sbyte[] {20,21,22,23 }
            };


            bool[] shardPresent = new bool[] { true, true, true, true, false, true };
            //Act
            rs.DecodeMissing(shards, shardPresent, 0, shards[0].Length);


            //Assert

            sbyte[][] expectedShards = new sbyte[][]
            {
                new sbyte[] {0,1,2,3 },
                new sbyte[] {4,5,6,7},
                new sbyte[] {8,9,10,11 },
                new sbyte[] {12,13,14,15 },
                new sbyte[] {16,17,18,19 },
                new sbyte[] {20,21,22,23 }
            };

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

            sbyte[][] shards = new sbyte[][]
            {
                new sbyte[] {0,1,2,3 },
                new sbyte[] {4,5,6,7},
                new sbyte[] {8,9,10,11 },
                new sbyte[] {12,13,14,15 },
                //new sbyte[] {16,17,18,19 },
                new sbyte[] {0,0,0,0},
                //new sbyte[] {20,21,22,23 }
                new sbyte[] {0,0,0,0}
            };


            bool[] shardPresent = new bool[] { true, true, true, true, false, false };
            //Act
            rs.DecodeMissing(shards, shardPresent, 0, shards[0].Length);


            //Assert

            sbyte[][] expectedShards = new sbyte[][]
            {
                new sbyte[] {0,1,2,3 },
                new sbyte[] {4,5,6,7},
                new sbyte[] {8,9,10,11 },
                new sbyte[] {12,13,14,15 },
                new sbyte[] {16,17,18,19 },
                new sbyte[] {20,21,22,23 }
            };

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

            sbyte[][] shards = new sbyte[][]
            {
                new sbyte[] {0,0,0,0},
                new sbyte[] {0,0,0,0},
                new sbyte[] {0,0,0,0},
                new sbyte[] {0,0,0,0},

                new sbyte[] {0,0,0,0},
                new sbyte[] {0,0,0,0}
            };


            bool[] shardPresent = new bool[] { false, false, false, false, false, false };
            //Act
            var ex = Assert.ThrowsException<ArgumentException>(() => rs.DecodeMissing(shards, shardPresent, 0, shards[0].Length));


            //Assert
            Assert.AreEqual("Not enough shards present", ex.Message);
        }
    }
}