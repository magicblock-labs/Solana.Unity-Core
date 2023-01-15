using NUnit.Framework;

using Solana.Unity.Dex.Orca.Swap;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Wallet;

namespace Solana.Unity.Dex.Test.Orca.SdkTests.UtilsTests
{
    [TestFixture]
    public static class SwapUtilsTests
    {
        //TODO: (LOW) can these be combined into one test using TestCase attribute 
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
    }
}