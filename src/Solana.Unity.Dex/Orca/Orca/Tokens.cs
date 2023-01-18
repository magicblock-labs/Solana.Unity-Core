using System.Net.Http;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Solana.Unity.Dex.Models;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Core.Http;
using System;

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
    public static class Tokens
    {
        private const string MainnetUrl = "https://api.mainnet.orca.so/v1/token/list"; 
        private const string DevnetUrl = "https://api.devnet.orca.so/v1/token/list"; 
        
        private static IList<TokenData> _tokens;

        /// <summary>
        /// Retrieves and returns the list of tokens. 
        /// </summary>
        /// <param name="cluster">The cluster</param>
        /// <param name="forceRefresh">If true, forces the list to refresh from the source (rather than returning
        /// a cached copy)</param>
        /// <returns>List of token data.</returns>
        public static async Task<IList<TokenData>> GetTokens(Cluster cluster = Cluster.MainNet, bool forceRefresh = false)
        {
            string url = cluster == Cluster.DevNet ? DevnetUrl : MainnetUrl;
            if (_tokens == null || forceRefresh)
            {
                using var client = new HttpClient();
                using var httpReq = new HttpRequestMessage(HttpMethod.Get, url);
                string response = await CrossHttpClient.SendAsyncRequest(client, httpReq).Result.Content.ReadAsStringAsync();
                TokensDocument tokensDocument = new JsonSerializer().Deserialize<TokensDocument>(
                    new JsonTextReader(
                        new StringReader(response)
                    )
                );
                if(tokensDocument != null)
                {
                    _tokens = tokensDocument.tokens.ToList();
                }
            }
            
            return _tokens;
        }
        
        /// <summary>
        /// Retrieves and returns the token data for the given symbol.
        /// </summary>
        /// <param name="symbol"></param>
        /// <exception cref="T:System.InvalidOperationException"/>No token found with given <paramref name="symbol" />.
        /// <returns></returns>
        public static async Task<TokenData> GetTokenBySymbol(string symbol)
        {
            IList<TokenData> tokens = await GetTokens();
            
            return tokens.First(t => 
                string.Equals(t.Symbol, symbol, StringComparison.CurrentCultureIgnoreCase) || 
                string.Equals(t.Symbol, $"${symbol}", StringComparison.CurrentCultureIgnoreCase));
        }
        
        /// <summary>
        /// Serialized from JSON; represents the entire JSON output. 
        /// </summary>
        private class TokensDocument
        {
            public IList<TokenData> tokens; 
        }
    }
    
    
}