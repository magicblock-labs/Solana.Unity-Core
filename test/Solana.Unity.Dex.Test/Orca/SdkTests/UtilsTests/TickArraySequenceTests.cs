using NUnit.Framework; 

using System;

using Solana.Unity.Dex.Orca.Exceptions;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Test.Orca.Utils;

namespace Solana.Unity.Dex.Test.Orca.SdkTests.UtilsTests
{
    public static class TickArraySequenceTests
    {
        [TestFixture]
        public static class CheckArrayContainsTickIndex
        {
            private static readonly TickArrayContainer _tickArray0 = TestDataUtils.BuildTickArrayData(0, new int[]{0, 32, 63});
            private static readonly TickArrayContainer _tickArray1 = TestDataUtils.BuildTickArrayData(64 * TickConstants.TICK_ARRAY_SIZE * -1, new int[]{0, 50});
            private static readonly TickArrayContainer _tickArray2 = TestDataUtils.BuildTickArrayData(64 * TickConstants.TICK_ARRAY_SIZE * -2, new int[]{25, 50}); 
            
            //TODO: (LOW) can these be combined into one test using TestCase attribute 
            [Test]
            [Description("a->b, arrayIndex 0 contains index")]
            public static void AToBCheckArrayContainsIndex() 
            {
                var seq = new TickArraySequence(
                    new TickArrayContainer[] { _tickArray0, _tickArray1, _tickArray2 },
                    64,
                    true
                );
                
                Assert.IsTrue(seq.CheckArrayContainsTickIndex(0, 250));
                Assert.IsFalse(seq.CheckArrayContainsTickIndex(0, -64));
            }
            
            [Test]
            [Description("b->a, arrayIndex 0 contains index")]
            public static void BToACheckArrayContainsIndex() //b->a, arrayIndex 0 contains index
            {
                var seq = new TickArraySequence(
                    new TickArrayContainer[] { _tickArray2, _tickArray1, _tickArray0 },
                    64,
                    false
                );

                Assert.IsTrue(seq.CheckArrayContainsTickIndex(0, -10000));
                Assert.IsFalse(seq.CheckArrayContainsTickIndex(0, -64));
            }
            
            [Test]
            [Description("check that null does not contain index")]
            public static void NullDoesNotContainIndex() //
            {
                var seq = new TickArraySequence(
                    new TickArrayContainer[] { _tickArray2, TestDataUtils.TestEmptyTickArray, _tickArray0 },
                    64,
                    false
                );

                Assert.IsFalse(seq.CheckArrayContainsTickIndex(1, -64));
            }
        }

        [TestFixture]
        public static class FindNextInitializedTickIndex
        {
            [Test]
            [Description("a->b, search reaches left bounds")]
            public static void AToBSearchReachesLeftBounds() 
            {
                //TODO: (HIGH) these should be in setup 
                TickArrayContainer ta0 = TestDataUtils.BuildTickArrayData(0, new int[]{0, 32, 63});
                TickArrayContainer ta1 = TestDataUtils.BuildTickArrayData(64 * TickConstants.TICK_ARRAY_SIZE * -1, new int[]{0, 50});
                TickArrayContainer ta2 = TestDataUtils.BuildTickArrayData(64 * TickConstants.TICK_ARRAY_SIZE * -2, new int[]{25, 50});
                TickArraySequence seq = new TickArraySequence(new TickArrayContainer[] { ta0, ta1, ta2 }, 64, true);
                int searchIndex = new TickArrayIndex(-2, 12, 64).ToTickIndex(); 

                // First traversal brings swap to the left most edge
                var nextIndex = seq.FindNextInitializedTickIndex(searchIndex).Item1;
                Assert.That(nextIndex, Is.EqualTo(ta2.Data.StartTickIndex));

                // The next one will throw an error
                System.Exception error = new System.Exception();
                try{
                    seq.FindNextInitializedTickIndex(nextIndex - 1);
                }
                catch(Exception e) 
                {
                    error = e;
                }
                
                Assert.IsTrue(WhirlpoolsException.IsWhirlpoolsErrorCode<SwapErrorCode>(error, SwapErrorCode.TickArraySequenceInvalid));
            }
            
            [Test]
            [Description("b->a, search reaches right bounds")]
            public static void BToASearchReachesRightBounds() 
            {
                TickArrayContainer ta0 = TestDataUtils.BuildTickArrayData(0, new int[]{0, 32});
                TickArrayContainer ta1 = TestDataUtils.BuildTickArrayData(64 * TickConstants.TICK_ARRAY_SIZE * -1, new int[]{0, 50});
                TickArrayContainer ta2 = TestDataUtils.BuildTickArrayData(64 * TickConstants.TICK_ARRAY_SIZE * -2, new int[]{25, 50});
                TickArraySequence seq = new TickArraySequence(new TickArrayContainer[] { ta2, ta1, ta0 }, 64, false);
                int searchIndex = new TickArrayIndex(0, 33, 64).ToTickIndex(); 

                // First traversal brings swap to the right most edge
                int nextIndex = seq.FindNextInitializedTickIndex(searchIndex).Item1;
                Assert.That(nextIndex, Is.EqualTo(ta0.Data.StartTickIndex + TickConstants.TICK_ARRAY_SIZE * 64 - 1));

                // The next one will throw an error
                System.Exception error = new System.Exception();
                try
                {
                    seq.FindNextInitializedTickIndex(nextIndex);
                }
                catch (Exception e)
                {
                    error = e;
                }

                Assert.IsTrue(WhirlpoolsException.IsWhirlpoolsErrorCode<SwapErrorCode>(error, SwapErrorCode.TickArraySequenceInvalid));
            }
            
            [Test]
            [Description("a->b, on initializable index, ts = 64")]
            public static void AToBOnInitializable() 
            {
                TickArrayContainer ta0 = TestDataUtils.BuildTickArrayData(0, new int[]{0, 32, 63});
                TickArrayContainer ta1 = TestDataUtils.BuildTickArrayData(64 * TickConstants.TICK_ARRAY_SIZE * -1, new int[]{0, 50});
                TickArrayContainer ta2 = TestDataUtils.BuildTickArrayData(64 * TickConstants.TICK_ARRAY_SIZE * -2, new int[]{25, 50});
                TickArraySequence seq = new TickArraySequence(new TickArrayContainer[] { ta0, ta1, ta2 }, 64, true);
                int searchIndex = new TickArrayIndex(0, 32, 64).ToTickIndex(); 

                int[] expectedIndices = new[]
                {
                    new TickArrayIndex(0, 32, 64).ToTickIndex(),
                    new TickArrayIndex(0, 0, 64).ToTickIndex(),
                    new TickArrayIndex(-1, 50, 64).ToTickIndex(),
                    new TickArrayIndex(-1, 0, 64).ToTickIndex(),
                    new TickArrayIndex(-2, 50, 64).ToTickIndex(),
                    new TickArrayIndex(-2, 25, 64).ToTickIndex(),
                    ta2.Data.StartTickIndex
                };
            }
            
            [Test]
            [Description("b->a, not on initializable index, ts = 128")]
            public static void BToANotOnInitializable() 
            {
                TickArrayContainer ta0 = TestDataUtils.BuildTickArrayData(0, new int[]{0, 32, 63});
                TickArrayContainer ta1 = TestDataUtils.BuildTickArrayData(128 * TickConstants.TICK_ARRAY_SIZE, new int[]{0, 50});
                TickArrayContainer ta2 = TestDataUtils.BuildTickArrayData(128 * TickConstants.TICK_ARRAY_SIZE * 2, new int[]{25, 50});
                TickArraySequence seq = new TickArraySequence(new TickArrayContainer[] { ta0, ta1, ta2 }, 128, false);
                int searchIndex = new TickArrayIndex(0, 25, 128).ToTickIndex(); 

                int[] expectedIndices = new[]
                {
                    new TickArrayIndex(0, 32, 128).ToTickIndex(),
                    new TickArrayIndex(0, 63, 128).ToTickIndex(),
                    new TickArrayIndex(1, 0, 128).ToTickIndex(),
                    new TickArrayIndex(1, 50, 128).ToTickIndex(),
                    new TickArrayIndex(2, 25, 128).ToTickIndex(),
                    new TickArrayIndex(2, 50, 128).ToTickIndex(),
                    ta2.Data.StartTickIndex + TickConstants.TICK_ARRAY_SIZE * 128 -1
                };
            }
            
            [Test]
            [Description("b->a, on initializable index, ts = 64")]
            public static void BToAOnInitializable() 
            {
                TickArrayContainer ta0 = TestDataUtils.BuildTickArrayData(0, new int[]{0, 32, 63});
                TickArrayContainer ta1 = TestDataUtils.BuildTickArrayData(64 * TickConstants.TICK_ARRAY_SIZE, new int[]{0, 50});
                TickArrayContainer ta2 = TestDataUtils.BuildTickArrayData(64 * TickConstants.TICK_ARRAY_SIZE * 2, new int[]{25, 50});
                TickArraySequence seq = new TickArraySequence(new TickArrayContainer[] { ta0, ta1, ta2 }, 64, false);
                int searchIndex = new TickArrayIndex(0, 25, 64).ToTickIndex();

                int[] expectedIndices = new[]
                {
                    new TickArrayIndex(0, 32, 64).ToTickIndex(),
                    new TickArrayIndex(0, 63, 64).ToTickIndex(),
                    new TickArrayIndex(1, 0, 64).ToTickIndex(),
                    new TickArrayIndex(1, 50, 64).ToTickIndex(),
                    new TickArrayIndex(2, 25, 64).ToTickIndex(),
                    new TickArrayIndex(2, 50, 64).ToTickIndex(),
                    ta2.Data.StartTickIndex + TickConstants.TICK_ARRAY_SIZE * 64 -1
                };

                for (int n=0; n<expectedIndices.Length; n++)
                {
                    int expectedIndex = expectedIndices[n];
                }
            }
        }
    }
}
