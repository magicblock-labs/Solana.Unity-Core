using NUnit.Framework;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.Swap;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Wallet;
using System.Collections.Generic;

namespace Solana.Unity.Dex.Test.Orca.SdkTests.UtilsTests
{
    [TestFixture]
    public static class SwapUtilsTests
    {
        private static TestWhirlpoolContext _context;

        [SetUp]
        public static void Setup()
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
        }
        
        [Test]
        [Description("SwapToken is tokenA and is an input")]
        public static void SwapTokenTokenAInput() 
        {
            Whirlpool pool = TestDataUtils.TestWhirlpool;
            SwapDirection result = SwapUtils.GetSwapDirection(pool, pool.TokenMintA, true);
            
            Assert.That(result, Is.EqualTo(SwapDirection.AtoB));
        }

        [Test]
        [Description("SwapToken is tokenB and is an input")]
        public static void SwapTokenTokenBInput() 
        {
            Whirlpool pool = TestDataUtils.TestWhirlpool;
            SwapDirection result = SwapUtils.GetSwapDirection(pool, pool.TokenMintB, true);
            
            Assert.That(result, Is.EqualTo(SwapDirection.BtoA));
        }

        [Test]
        [Description("SwapToken is tokenA and is not an input")]
        public static void SwapTokenTokenANotInput() 
        {
            Whirlpool pool = TestDataUtils.TestWhirlpool;
            SwapDirection result = SwapUtils.GetSwapDirection(pool, pool.TokenMintA, false);
            
            Assert.That(result, Is.EqualTo(SwapDirection.BtoA));
        }

        [Test]
        [Description("SwapToken is tokenB and is not an input")]
        public static void SwapTokenTokenBNotInput() 
        {
            Whirlpool pool = TestDataUtils.TestWhirlpool;
            SwapDirection result = SwapUtils.GetSwapDirection(pool, pool.TokenMintB, false);
            
            Assert.That(result, Is.EqualTo(SwapDirection.AtoB));
        }

        [Test]
        [Description("SwapToken is a random mint and is an input")]
        public static void SwapTokenRandomInput() 
        {
            Whirlpool pool = TestDataUtils.TestWhirlpool;
            SwapDirection result = SwapUtils.GetSwapDirection(pool, new Account().PublicKey, true);
            
            Assert.That(result, Is.EqualTo(SwapDirection.None));
        }

        [Test]
        [Description("SwapToken is a random mint and is not an input")]
        public static void SwapTokenRandomNotInput() 
        {
            Whirlpool pool = TestDataUtils.TestWhirlpool;
            SwapDirection result = SwapUtils.GetSwapDirection(pool, new Account().PublicKey, false);
            
            Assert.That(result, Is.EqualTo(SwapDirection.None));
        }
        
        [Test]
        [Description("getTickArrayPublicKeys, a->b, ts = 64, tickCurrentIndex = 0")]
        public static void GetTickArrayPublicKeysAToB64Ticks() 
        {
            PublicKey programId = _context.ProgramId;
            PublicKey whirlpoolPubkey = new Account().PublicKey;
            ushort tickSpacing = 64;
            int ticksInArray = tickSpacing * TickConstants.TICK_ARRAY_SIZE;
            bool aToB = true;
            int tickCurrentIndex = 0;

            IList<PublicKey> result = SwapUtils.GetTickArrayPublicKeys(
                tickCurrentIndex,
                tickSpacing,
                aToB,
                programId,
                whirlpoolPubkey
            );
            var expected = new List<PublicKey>
            {
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * 0).PublicKey,
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * -1).PublicKey,
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * -2).PublicKey,
            };
            for (int i = 0; i < result.Count; i++)
            {
                Assert.IsTrue(result[i].Equals(expected[i]));
            }
        }
        
        [Test]
        [Description("getTickArrayPublicKeys, a->b, ts = 64, tickCurrentIndex = 64*TICK_ARRAY_SIZE - 64")]
        public static void GetTickArrayPublicKeysAToB64TicksCurrentIndex() 
        {
            PublicKey programId = _context.ProgramId;
            PublicKey whirlpoolPubkey = new Account().PublicKey;
            ushort tickSpacing = 64;
            int ticksInArray = tickSpacing * TickConstants.TICK_ARRAY_SIZE;
            bool aToB = true;
            var tickCurrentIndex = tickSpacing * TickConstants.TICK_ARRAY_SIZE - tickSpacing;

            IList<PublicKey> result = SwapUtils.GetTickArrayPublicKeys(
                tickCurrentIndex,
                tickSpacing,
                aToB,
                programId,
                whirlpoolPubkey
            );
            var expected = new List<PublicKey>
            {
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * 0).PublicKey,
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * -1).PublicKey,
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * -2).PublicKey,
            };
            for (int i = 0; i < result.Count; i++)
            {
                Assert.IsTrue(result[i].Equals(expected[i]));
            }
        }
        
        [Test]
        [Description("getTickArrayPublicKeys, a->b, ts = 64, tickCurrentIndex = 64*TICK_ARRAY_SIZE - 1")]
        public static void GetTickArrayPublicKeysAToB64TicksCurrentIndexMin1() 
        {
            PublicKey programId = _context.ProgramId;
            PublicKey whirlpoolPubkey = new Account().PublicKey;
            ushort tickSpacing = 64;
            int ticksInArray = tickSpacing * TickConstants.TICK_ARRAY_SIZE;
            bool aToB = true;
            var tickCurrentIndex = tickSpacing * TickConstants.TICK_ARRAY_SIZE - 1;

            IList<PublicKey> result = SwapUtils.GetTickArrayPublicKeys(
                tickCurrentIndex,
                tickSpacing,
                aToB,
                programId,
                whirlpoolPubkey
            );
            var expected = new List<PublicKey>
            {
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * 0).PublicKey,
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * -1).PublicKey,
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * -2).PublicKey,
            };
            for (int i = 0; i < result.Count; i++)
            {
                Assert.IsTrue(result[i].Equals(expected[i]));
            }
        }
        
        [Test]
        [Description("getTickArrayPublicKeys, b->a, shifted, ts = 64, tickCurrentIndex = 0")]
        public static void GetTickArrayPublicKeysBToA64() 
        {
            PublicKey programId = _context.ProgramId;
            PublicKey whirlpoolPubkey = new Account().PublicKey;
            ushort tickSpacing = 64;
            int ticksInArray = tickSpacing * TickConstants.TICK_ARRAY_SIZE;
            bool aToB = false;
            var tickCurrentIndex = 0;

            IList<PublicKey> result = SwapUtils.GetTickArrayPublicKeys(
                tickCurrentIndex,
                tickSpacing,
                aToB,
                programId,
                whirlpoolPubkey
            );
            var expected = new List<PublicKey>
            {
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * 0).PublicKey,
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * 1).PublicKey,
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * 2).PublicKey,
            };
            for (int i = 0; i < result.Count; i++)
            {
                Assert.IsTrue(result[i].Equals(expected[i]));
            }
        }
        
        [Test]
        [Description("getTickArrayPublicKeys, b->a, shifted, ts = 64, tickCurrentIndex = 64*TICK_ARRAY_SIZE - 64 - 1")]
        public static void GetTickArrayPublicKeysBToA64Min1() 
        {
            PublicKey programId = _context.ProgramId;
            PublicKey whirlpoolPubkey = new Account().PublicKey;
            ushort tickSpacing = 64;
            int ticksInArray = tickSpacing * TickConstants.TICK_ARRAY_SIZE;
            bool aToB = false;
            var tickCurrentIndex = tickSpacing * TickConstants.TICK_ARRAY_SIZE - tickSpacing - 1;

            IList<PublicKey> result = SwapUtils.GetTickArrayPublicKeys(
                tickCurrentIndex,
                tickSpacing,
                aToB,
                programId,
                whirlpoolPubkey
            );
            var expected = new List<PublicKey>
            {
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * 0).PublicKey,
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * 1).PublicKey,
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * 2).PublicKey,
            };
            for (int i = 0; i < result.Count; i++)
            {
                Assert.IsTrue(result[i].Equals(expected[i]));
            }
        }
        
        [Test]
        [Description("getTickArrayPublicKeys, b->a, shifted, ts = 64, tickCurrentIndex = 64*TICK_ARRAY_SIZE - 64")]
        public static void GetTickArrayPublicKeysBToA64Min64() 
        {
            PublicKey programId = _context.ProgramId;
            PublicKey whirlpoolPubkey = new Account().PublicKey;
            ushort tickSpacing = 64;
            int ticksInArray = tickSpacing * TickConstants.TICK_ARRAY_SIZE;
            bool aToB = false;
            var tickCurrentIndex = tickSpacing * TickConstants.TICK_ARRAY_SIZE - tickSpacing;

            IList<PublicKey> result = SwapUtils.GetTickArrayPublicKeys(
                tickCurrentIndex,
                tickSpacing,
                aToB,
                programId,
                whirlpoolPubkey
            );
            var expected = new List<PublicKey>
            {
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * 1).PublicKey,
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * 2).PublicKey,
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * 3).PublicKey,
            };
            for (int i = 0; i < result.Count; i++)
            {
                Assert.IsTrue(result[i].Equals(expected[i]));
            }
        }
        
        [Test]
        [Description("getTickArrayPublicKeys, b->a, shifted, ts = 64, tickCurrentIndex = 64*TICK_ARRAY_SIZE - 1")]
        public static void GetTickArrayPublicKeysBToA64Min64S() 
        {
            PublicKey programId = _context.ProgramId;
            PublicKey whirlpoolPubkey = new Account().PublicKey;
            ushort tickSpacing = 64;
            int ticksInArray = tickSpacing * TickConstants.TICK_ARRAY_SIZE;
            bool aToB = false;
            var tickCurrentIndex = tickSpacing * TickConstants.TICK_ARRAY_SIZE - 1;

            IList<PublicKey> result = SwapUtils.GetTickArrayPublicKeys(
                tickCurrentIndex,
                tickSpacing,
                aToB,
                programId,
                whirlpoolPubkey
            );
            var expected = new List<PublicKey>
            {
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * 1).PublicKey,
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * 2).PublicKey,
                PdaUtils.GetTickArray(programId, whirlpoolPubkey, ticksInArray * 3).PublicKey,
            };
            for (int i = 0; i < result.Count; i++)
            {
                Assert.IsTrue(result[i].Equals(expected[i]));
            }
        }
        
    }
}