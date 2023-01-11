using NUnit.Framework;
using Orca;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Solana.Unity.Dex.Test.Orca.Integration.TxApi
{
    [TestFixture]
    public class TokensListTests
    {
        [Test]
        public static async Task GetTokensList()
        {
            IList<TokenData> tokens = await OrcaTokensList.GetTokens(); 
            
            Assert.That(tokens.Count, Is.GreaterThan(0));
            Assert.That(tokens.Count(t => t.Symbol == "ORCA"), Is.GreaterThan(0));
        }
    }
}