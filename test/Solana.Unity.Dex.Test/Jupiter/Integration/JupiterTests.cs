using NUnit.Framework;
using Solana.Unity.Dex.Jupiter;
using Solana.Unity.Dex.Models;
using System.Threading.Tasks;
using Solana.Unity.Dex.Test.Orca;
using Solana.Unity.Wallet;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Solana.Unity.Dex.Test.Jupiter.Integration
{
     
    [TestFixture]
    public class JupiterTests
    {
        

        [Test] 
        [Description("get a swap quote")]
        public static async Task GetSwapQuote()
        {
            IDexAggregator dexAg = new JupiterDexAg(TestConfiguration.TestWallet.Account);

            PublicKey inputMint = new("So11111111111111111111111111111111111111112"); // SOL
            PublicKey outputMint = new("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v"); // USDC
            BigInteger amount = 100000000;
            
            var quote = await dexAg.GetSwapQuote(inputMint, outputMint, amount);
            
            Assert.IsNotNull(quote);
            Assert.IsTrue(quote.OutputAmount > 0);
            Assert.IsTrue(quote.InputAmount > 0);
            Assert.IsTrue(quote.RoutePlan.Count > 0);
        }
        
        [Test] 
        [Description("get a swap transaction")]
        public static async Task GetSwap()
        {
            IDexAggregator dexAg = new JupiterDexAg(TestConfiguration.TestWallet.Account);

            PublicKey inputMint = new("So11111111111111111111111111111111111111112"); // SOL
            PublicKey outputMint = new("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v"); // USDC
            BigInteger amount = 100000000;
            
            var quote = await dexAg.GetSwapQuote(inputMint, outputMint, amount);
            
            Assert.IsNotNull(quote);
            Assert.IsTrue(quote.OutputAmount > 0);
            Assert.IsTrue(quote.InputAmount > 0);
            Assert.IsTrue(quote.RoutePlan.Count > 0);
            
            var tx = await dexAg.Swap(quote);
            Assert.IsNotNull(tx);
        }
        
        [Test]
        public static async Task GetTokensList()
        {
            IDexAggregator dexAg = new JupiterDexAg();
            IList<TokenData> tokens = await dexAg.GetTokens(); 
            
            Assert.That(tokens.Count, Is.GreaterThan(0));
            Assert.That(tokens.Count(t => t.Symbol == "USDC"), Is.GreaterThan(0));
        }
        
        [Test]
        public static async Task GetTokenBySymbol()
        {  
            IDexAggregator dexAg = new JupiterDexAg();
            TokenData token = await dexAg.GetTokenBySymbol("ORCA"); 
            
            Assert.IsNotNull(token);
            Assert.IsNotNull(token.Mint);
        }
        
        [Test]
        public static async Task GetTokenByMint()
        {
            string orcaMint = "orcaEKTdK7LKz57vaAYr9QeNsVEPfiu6QeMU1kektZE";
            IDexAggregator dexAg = new JupiterDexAg();
            TokenData token = await dexAg.GetTokenByMint(orcaMint); 
            
            Assert.IsNotNull(token);
            Assert.IsTrue(token.Mint.Equals(orcaMint));
            Assert.IsTrue(token.Symbol.Equals("ORCA"));
        }
    }
}