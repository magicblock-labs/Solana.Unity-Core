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
            IList<TokenData> tokens = await OrcaTokens.GetTokens(); 
            
            Assert.That(tokens.Count, Is.GreaterThan(0));
            Assert.That(tokens.Count(t => t.Symbol == "ORCA"), Is.GreaterThan(0));
        }
        
        [Test]
        public static async Task GetTokenBySymbol()
        {  
            TokenData token = await OrcaTokens.GetTokenBySymbol("ORCA"); 
            
            Assert.IsNotNull(token);
            Assert.IsNotNull(token.Mint);
        }
        
        [Test]
        public static async Task GetTokenByMint()
        {
            string orcaMint = "orcaEKTdK7LKz57vaAYr9QeNsVEPfiu6QeMU1kektZE";
            IDex dex = new OrcaDex(new Account(), _context.RpcClient);
            TokenData token = await dex.GetTokenByMint(orcaMint); 
            
            Assert.IsNotNull(token);
            Assert.IsTrue(token.Mint.Equals(orcaMint));
            Assert.IsTrue(token.Symbol.Equals("ORCA"));
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
        
        [Test]
        public static async Task GetPositions()
        {
            // Open a position to ensure we have one
            OpenPositionTxApiTests.Setup();
            await OpenPositionTxApiTests.OpenPositionSingleTransaction();
            
            // Get positions
            IDex dex = new OrcaDex(_context.WalletPubKey, _context.RpcClient);
            IList<PublicKey> positions = await dex.GetPositions(commitment: TestConfiguration.DefaultCommitment);
            
            Assert.IsNotNull(positions);
            Assert.IsTrue(positions.Count > 0);
        }
        
        [Test]
        public static async Task GetEmptyPositionsForOwner()
        {
            Account newAccount = new();
            
            // Get positions
            IDex dex = new OrcaDex(_context.WalletPubKey, _context.RpcClient);
            IList<PublicKey> positions = await dex.GetPositions(owner: newAccount, commitment: TestConfiguration.DefaultCommitment);
            
            Assert.IsNotNull(positions);
            Assert.IsTrue(positions.Count == 0);
        }
        
        [Test]
        public static async Task GetPositionsForOwner()
        {
            // Open a position on a new account
            OpenPositionTxApiTests.Setup();
            Account newAccount = new();
            OpenPositionTxApiTests.WalletAccount = newAccount;
            await OpenPositionTxApiTests.OpenPositionSingleTransaction();
            
            // Get positions
            IDex dex = new OrcaDex(_context.WalletPubKey, _context.RpcClient);
            IList<PublicKey> positions = await dex.GetPositions(owner: newAccount, commitment: TestConfiguration.DefaultCommitment);
            
            Assert.IsNotNull(positions);
            Assert.IsTrue(positions.Count == 1);
        }
    }
}