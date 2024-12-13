using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Solana.Unity.Dex.Jupiter.Core.Types.Http;
using Solana.Unity.Dex.Models;
using Solana.Unity.Dex.Orca.Orca;
using Solana.Unity.Dex.Quotes;
using Solana.Unity.Rpc.Converters;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Solana.Unity.Dex.Jupiter;

/// <summary>
/// Concrete implementation of IDexAggregator for Jupiter Aggregator. 
/// </summary>
public class JupiterDexAg: IDexAggregator
{
    private readonly PublicKey _account;
    private readonly string _endpoint;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerSettings _serializerOptions;
    private List<TokenData> _tokens;
    
    /// <summary>
    /// Public constructor; Create the JupiterDexAg instance with the account to use for the aggregator. 
    /// </summary>
    /// <param name="endpoint"></param>
    public JupiterDexAg(string endpoint = "https://api.jup.ag/")
    {
        _endpoint = endpoint;
        _httpClient = new HttpClient();
        _serializerOptions = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters =
            {
                new EncodingConverter(),
                new StringEnumConverter()
            },
            NullValueHandling = NullValueHandling.Ignore
        };
    }

    /// <summary>
    /// Public constructor; Create the JupiterDexAg instance with the account to use for the aggregator. 
    /// </summary>
    /// <param name="account"></param>
    /// <param name="endpoint"></param>
    public JupiterDexAg(PublicKey account, string endpoint = "https://api.jup.ag/")
    {
        _account = account;
        _endpoint = endpoint;
        _httpClient = new HttpClient();
        _serializerOptions = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters =
            {
                new EncodingConverter(),
                new StringEnumConverter()
            },
            NullValueHandling = NullValueHandling.Ignore
        };
    }
 
    /// <inheritdoc />
    public async Task<SwapQuoteAg> GetSwapQuote(
        PublicKey inputMint,
        PublicKey outputMint,
        BigInteger amount,
        SwapMode swapMode = SwapMode.ExactIn,
        ushort? slippageBps = null,
        List<string> excludeDexes = null,
        bool onlyDirectRoutes = false,
        ushort? platformFeeBps = null,
        ushort? maxAccounts = null)
    {
        // Construct the query parameters
        List<KeyValuePair<string, string>> queryParams = new()
        {
            new("inputMint", inputMint.ToString()),
            new("outputMint", outputMint.ToString()),
            new("amount", amount.ToString()),
            new("swapMode", swapMode.ToString()),
            new("asLegacyTransaction", "false")
        };

        if (slippageBps.HasValue) queryParams.Add(new KeyValuePair<string, string>("slippageBps", slippageBps.Value.ToString()));

        if (excludeDexes is { Count: > 0 }) queryParams.AddRange(excludeDexes.Select(dex => new KeyValuePair<string, string>("excludeDexes", dex)));

        queryParams.Add(new KeyValuePair<string, string>("onlyDirectRoutes", onlyDirectRoutes.ToString().ToLower()));

        if (platformFeeBps.HasValue) queryParams.Add(new KeyValuePair<string, string>("platformFeeBps", platformFeeBps.Value.ToString()));

        if (maxAccounts.HasValue) queryParams.Add(new KeyValuePair<string, string>("maxAccounts", maxAccounts.Value.ToString()));

        var queryString = string.Join("&", queryParams.Select(kv => $"{kv.Key}={kv.Value}"));

        // Construct the request URL
        var apiUrl = _endpoint + "/quote/v1?" + queryString;

        using var httpReq = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        
        // execute the REST request
        using var response = await CrossHttpClient.SendAsyncRequest(_httpClient, httpReq);
        
        if (response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            // Deserialize the response JSON into SwapQuoteAg object
            var swapQuote = JsonConvert.DeserializeObject<SwapQuoteAg>(responseBody);
            return swapQuote;
        }

        // Handle error scenarios
        throw new HttpRequestException($"Request failed with status code: {response.StatusCode}");
    }

    /// <inheritdoc />
    public async Task<Transaction> Swap(
        SwapQuoteAg quoteResponse,
        PublicKey userPublicKey = null, 
        PublicKey destinationTokenAccount = null,
        bool wrapAndUnwrapSol = true, 
        bool useSharedAccounts = true, 
        PublicKey feeAccount = null,
        BigInteger? computeUnitPriceMicroLamports = null,
        bool useTokenLedger = false)
    {
        userPublicKey ??= _account;
        
        // Construct the request URL
        var apiUrl = _endpoint + "/swap/v1";

        var req = new SwapRequest()
        {
            QuoteResponse = quoteResponse,
            UserPublicKey = userPublicKey,
            DestinationTokenAccount = destinationTokenAccount,
            WrapAndUnwrapSol = wrapAndUnwrapSol,
            UseSharedAccounts = useSharedAccounts,
            FeeAccount = feeAccount,
            ComputeUnitPriceMicroLamports = computeUnitPriceMicroLamports,
            UseTokenLedger = useTokenLedger,
            AsLegacyTransaction = false
        };
        
        var requestJson = JsonConvert.SerializeObject(req, _serializerOptions);
        var buffer = Encoding.UTF8.GetBytes(requestJson);
        
        using var httpReq = new HttpRequestMessage(HttpMethod.Post, apiUrl)
        {
            Content = new ByteArrayContent(buffer)
            {
                Headers = {
                    { "Content-Type", "application/json"}
                }
            }
        };
        
        // execute POST
        using var response = await CrossHttpClient.SendAsyncRequest(_httpClient, httpReq);

        if (response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var res = JsonConvert.DeserializeObject<SwapResponse>(responseBody, _serializerOptions);
            return Transaction.Deserialize(res.SwapTransaction);
        }
        
        // Handle error scenarios
        throw new HttpRequestException($"Request failed with status code: {response.StatusCode}");
    }

    /// <inheritdoc />
    public async Task<IList<TokenData>> GetTokens(TokenListType tokenListType = TokenListType.Strict)
    {
        string url = $"https://token.jup.ag/{tokenListType.ToString().ToLower()}";
        if (_tokens == null)
        {
            using var client = new HttpClient();
            using var httpReq = new HttpRequestMessage(HttpMethod.Get, url);
            HttpResponseMessage result = await CrossHttpClient.SendAsyncRequest(client, httpReq);
            string response = await result.Content.ReadAsStringAsync();
            TokensDocument tokensDocument = new JsonSerializer().Deserialize<TokensDocument>(
                new JsonTextReader(
                    new StringReader($"{{\"tokens\": {response} }}")
                )
            );
            if(tokensDocument != null)
            {
                _tokens = tokensDocument.tokens.ToList();
            }
        }
            
        return _tokens;
    }

    /// <inheritdoc />
    public async Task<TokenData> GetTokenBySymbol(string symbol)
    {
        IList<TokenData> tokens = await GetTokens(TokenListType.All);
        return tokens.First(t => 
            string.Equals(t.Symbol, symbol, StringComparison.CurrentCultureIgnoreCase) || 
            string.Equals(t.Symbol, $"${symbol}", StringComparison.CurrentCultureIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<TokenData> GetTokenByMint(string mint)
    {
        IList<TokenData> tokens = await GetTokens(TokenListType.All);
        return tokens.First(t => string.Equals(t.Mint, mint, StringComparison.CurrentCultureIgnoreCase));
    }
}