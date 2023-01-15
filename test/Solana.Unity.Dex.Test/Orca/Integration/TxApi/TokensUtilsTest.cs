using NUnit.Framework;
using Orca;
using Solana.Unity.Dex.Models;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Wallet;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Solana.Unity.Dex.Test.Orca.Integration.TxApi
{
    [TestFixture]
    public class TokensUtilsTest
    {
        private static TestWhirlpoolContext _context;

        [SetUp]
        public static void Setup()
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
        }
        
        [Test]
        public static async Task GetTokensList()
        {
            IList<TokenData> tokens = await Tokens.GetTokens(); 
            
            Assert.That(tokens.Count, Is.GreaterThan(0));
            Assert.That(tokens.Count(t => t.Symbol == "ORCA"), Is.GreaterThan(0));
        }
        
        [Test]
        public static async Task GetTokenBySymbol()
        {  
            TokenData token = await Tokens.GetTokenBySymbol("ORCA"); 
            
            Assert.IsNotNull(token);
            Assert.IsNotNull(token.Mint);
        }
        
        [Test]
        public static async Task GetTokensWithInterface()
        {
            IDex dex = new OrcaDex(new Account(), _context.RpcClient);
            IList<TokenData> tokens = await dex.GetTokens();
            
            Assert.That(tokens.Count, Is.GreaterThan(0));
            Assert.That(tokens.Count(t => t.Symbol == "ORCA"), Is.GreaterThan(0));
        }
        
        [Test]
        public static async Task GetTokenBySymbolWithInterface()
        {  
            IDex dex = new OrcaDex(new Account(), _context.RpcClient);
            TokenData token = await dex.GetTokenBySymbol("ORCA"); 
            
            Assert.IsNotNull(token);
            Assert.IsNotNull(token.Mint);
        }
    }
}