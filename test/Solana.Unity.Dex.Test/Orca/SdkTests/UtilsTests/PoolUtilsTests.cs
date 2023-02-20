using NUnit.Framework;

using System;
using System.Collections.Generic; 

using Solana.Unity.Wallet;

using Solana.Unity.Dex.Orca.Swap;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Swap;

namespace Solana.Unity.Dex.Test.Orca.SdkTests.UtilsTests
{
    //ALL 6 PASSING 
    public static class PoolUtilsTests
    {
        [TestFixture]
        public static class GetTokenType
        {
            //TODO: (LOW) can these be combined into one test using TestCase attribute 
            [Test]
            [Description("getTokenType: Token is tokenA")]
            public static void TokenA() 
            {
                Whirlpool pool = TestDataUtils.TestWhirlpool;
                TokenType result = PoolUtils.GetTokenType(pool, pool.TokenMintA);
                
                Assert.That(result, Is.EqualTo(TokenType.TokenA));
            }
            
            [Test]
            [Description("getTokenType: Token is tokenB")]
            public static void TokenB() 
            {
                Whirlpool pool = TestDataUtils.TestWhirlpool;
                TokenType result = PoolUtils.GetTokenType(pool, pool.TokenMintB);
                
                Assert.That(result, Is.EqualTo(TokenType.TokenB));
            }
            
            [Test]
            [Description("getTokenType: Token is some other token")]
            public static void NeitherTokenANorTokenB() 
            {
                Whirlpool pool = TestDataUtils.TestWhirlpool;
                TokenType result = PoolUtils.GetTokenType(pool, new Account().PublicKey);
                
                Assert.That(result, Is.EqualTo(TokenType.None));
            }
        }

        [TestFixture]
        [Description("determine base quote token ordering")]
        public static class BaseQuoteTokenOrdering
        {
            public static readonly Dictionary<string, PublicKey> Mints = new Dictionary<string, PublicKey>()
        {
            { "FTM", new PublicKey("EsPKhGTMf3bGoy4Qm7pCv3UCcWqAmbC1UGHBTDxRjjD4")},
            { "SOL", AddressConstants.NATIVE_MINT_PUBKEY},
            { "mSOL", new PublicKey("mSoLzYCxHdYgdzU16g5QSh3i5K3z3KZK7ytfqcJm7So")},
            { "USDH", new PublicKey("USDH1SM1ojwWUga67PGrgFWUHibbjqMvuMaDkRJTgkX")},
            { "stSOL", new PublicKey("7dHbWXmci3dT8UFYWYZweBLXgycu7Y3iL6trKn1Y7ARj")},
            { "BTC", new PublicKey("9n4nbM75f5Ui33ZbPYXn59EwSgE8CGsHtAeTH5YFeJ9E")},
            { "whETH", new PublicKey("7vfCXTUXx5WJV5JADk17DUJ4ksgau7utNKj4b963voxs")},
            { "USDC", new PublicKey("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v")},
            { "USDT", new PublicKey("Es9vMFrzaCERmJfrF4H2FYD4KCoNkY11McCe8BenwNYB")},
            { "ORCA", new PublicKey("orcaEKTdK7LKz57vaAYr9QeNsVEPfiu6QeMU1kektZE")},
        };

            [Test]
            [Description("USD stables")]
            public static void UsdStables()
            {
                Tuple<PublicKey, PublicKey> pair = null;

                // USDC/FTM => FTM/USDC
                pair = PoolUtils.ToBaseQuoteOrder(Mints["USDC"], Mints["FTM"]);

                Assert.That(Mints["FTM"], Is.EqualTo(pair.Item1));
                Assert.That(Mints["USDC"], Is.EqualTo(pair.Item2));

                // USDT/USDC => USDC/USDT
                pair = PoolUtils.ToBaseQuoteOrder(Mints["USDT"], Mints["USDC"]);

                Assert.That(Mints["USDC"], Is.EqualTo(pair.Item1));
                Assert.That(Mints["USDT"], Is.EqualTo(pair.Item2));

                // USDH/stSOL => stSOL/USDH
                pair = PoolUtils.ToBaseQuoteOrder(Mints["USDH"], Mints["stSOL"]);

                Assert.That(Mints["stSOL"], Is.EqualTo(pair.Item1));
                Assert.That(Mints["USDH"], Is.EqualTo(pair.Item2));
            }

            [Test]
            [Description("SOL variants")]
            public static void SolVariants()
            {
                Tuple<PublicKey, PublicKey> pair = null;

                // SOL/mSOL => mSOL/SOL
                pair = PoolUtils.ToBaseQuoteOrder(Mints["SOL"], Mints["mSOL"]);

                Assert.That(Mints["mSOL"], Is.EqualTo(pair.Item1));
                Assert.That(Mints["SOL"], Is.EqualTo(pair.Item2));

                // mSOL/BTC => BTC/mSOL
                pair = PoolUtils.ToBaseQuoteOrder(Mints["mSOL"], Mints["BTC"]);

                Assert.That(Mints["BTC"], Is.EqualTo(pair.Item1));
                Assert.That(Mints["mSOL"], Is.EqualTo(pair.Item2));

                // mSOL/whETH => whETH/mSOL
                pair = PoolUtils.ToBaseQuoteOrder(Mints["mSOL"], Mints["whETH"]);

                Assert.That(Mints["whETH"], Is.EqualTo(pair.Item1));
                Assert.That(Mints["mSOL"], Is.EqualTo(pair.Item2));
            }

            [Test]
            [Description("Order remains unchanged for exotic pairs")]
            public static void OrderUnchangedForExoticPairs()
            {
                //FTM/ORCA => FTM/ORCA (unchanged)
                Tuple<PublicKey, PublicKey> pair = PoolUtils.ToBaseQuoteOrder(Mints["FTM"], Mints["ORCA"]);

                Assert.That(Mints["FTM"], Is.EqualTo(pair.Item1));
                Assert.That(Mints["ORCA"], Is.EqualTo(pair.Item2));
            }
        }
    }
}