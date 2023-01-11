using System;
using System.Net.Http;
using System.Linq;
using System.IO;
using System.Text;
using System.Numerics;
using System.Collections.Generic;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Solana.Unity.Wallet;

namespace Orca
{
    /// <summary>
    /// Retrieves a list of tokens that are featured in whirlpools. This list is not exhaustive, as it is derived only 
    /// from whirlpools that have been created by Orca. 
    /// 
    /// devnet url: https://api.devnet.orca.so/v1/token/list
    /// mainnet url: https://api.mainnet.orca.so/v1/token/list
    /// not supported for localnet or testnet    
    /// </summary>
    public static class OrcaTokensList
    {
        private const string MainnetUrl = "https://api.mainnet.orca.so/v1/token/list"; 
        private const string DevnetUrl = "https://api.devnet.orca.so/v1/token/list"; 
        
        private static IList<TokenData> _tokens = null;
        
        /// <summary>
        /// Retrieves and returns the list of tokens. 
        /// </summary>
        /// <param name="forceRefresh">If true, forces the list to refresh from the source (rather than returning
        /// a cached copy)</param>
        /// <returns>List of token data.</returns>
        public static async Task<IList<TokenData>> GetTokens(bool forceRefresh = false)
        {
            if (_tokens == null || forceRefresh)
            {
                HttpClient client = new HttpClient();
                string response = await client.GetStringAsync(MainnetUrl);

                JsonDocument json = new JsonSerializer().Deserialize<JsonDocument>(
                    new JsonTextReader(
                        new StringReader(response)
                    )
                );

                _tokens = json.tokens.Select(t =>
                    new TokenData
                    {
                        MintAddress = new PublicKey(t.Mint),
                        Name = t.Name,
                        Symbol = t.Symbol,
                        Decimals = t.Decimals
                    }
                ).ToList();
            }
            
            return _tokens;
        }
        
        /// <summary>
        /// Serialized from JSON; represents the entire JSON output. 
        /// </summary>
        private class JsonDocument
        {
            public IList<JsonToken> tokens; 
        }
        
        /// <summary>
        /// Serialized from JSON; represents a single token record.
        /// </summary>
        private class JsonToken
        {
            public string Name { get; set; }
            public string Mint { get; set; } 
            public string Symbol { get; set; } 
            public int Decimals { get; set; }
        }
    }
    
    /// <summary>
    /// Encapsulates information about a token, retrieved from Orca's API. 
    /// </summary>
    public class TokenData
    {
        /// <summary>
        /// Gets/sets token mint address. 
        /// </summary>
        public PublicKey MintAddress { get; set; }
        /// <summary>
        /// Gets/sets token symbol. 
        /// </summary>
        public string Symbol { get; set; }
        /// <summary>
        /// Gets/sets token name. 
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Gets/sets token's decimal count. 
        /// </summary>
        public int Decimals { get; set; }
    }
}