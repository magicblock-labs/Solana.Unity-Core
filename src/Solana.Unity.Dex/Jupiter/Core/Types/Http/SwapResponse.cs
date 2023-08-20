using Newtonsoft.Json;
using Org.BouncyCastle.Math;

namespace Solana.Unity.Dex.Jupiter.Core.Types.Http;

/// <summary>
/// The response to get a swap quote from the Jupiter aggregator.
/// </summary>
public class SwapResponse
{
    /// <summary>
    /// The swap quote
    /// </summary>
    public string SwapTransaction { get; set; }

    [JsonProperty("lastValidBlockHeight")]
    private string _lastValidBlockHeight { get; set; }
    
    /// <summary>
    /// The LastValidBlockHeight
    /// </summary>
    [JsonIgnore]
    public BigInteger LastValidBlockHeight => new(_lastValidBlockHeight);
}