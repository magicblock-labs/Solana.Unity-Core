using Newtonsoft.Json;
using Solana.Unity.Wallet;
using System.Collections.Generic;
using System.Numerics;

namespace Solana.Unity.Dex.Quotes;

/// <summary> 
/// A swap quote from an aggregator.
/// </summary>
public class SwapQuoteAg{
    
    /// <summary>
    /// The input mint
    /// </summary>
    [JsonProperty("inputMint")]
    public string InputMint { get; set; }
    
    /// <summary>
    /// The input amount
    /// </summary>
    [JsonProperty("inAmount")]
    private string InAmount { get; set; }
    
    /// <summary>
    /// The input amount
    /// </summary>
    [JsonIgnore]
    public BigInteger InputAmount => BigInteger.Parse(InAmount);
    
    /// <summary>
    /// The output mint
    /// </summary>
    [JsonProperty("outputMint")]
    public string OutputMint { get; set; }
    
    /// <summary>
    /// The output amount
    /// </summary>
    [JsonProperty("outAmount")]
    private string OutAmount { get; set; }
    
    /// <summary>
    /// The output amount
    /// </summary>
    [JsonIgnore]
    public BigInteger OutputAmount => BigInteger.Parse(OutAmount);
    
    /// <summary>
    /// The otherAmountThreshold
    /// </summary>
    [JsonProperty("otherAmountThreshold")]
    public string OtherAmountThreshold { get; set; }

    /// <summary>
    /// The swap mode
    /// </summary>
    [JsonProperty("swapMode")]
    public SwapMode Mode { get; set; }
    
    /// <summary>
    /// The slippage
    /// </summary>
    [JsonProperty("slippageBps")]
    public ushort SlippageBps { get; set; }
    
    /// <summary>
    /// The platform fee
    /// </summary>
    [JsonProperty("platformFee")]
    public PlatformFee PlatformFee { get; set; }
    
    /// <summary>
    /// The otherAmountThreshold
    /// </summary>
    [JsonProperty("priceImpactPct")]
    public string PriceImpactPct { get; set; }
    
    /// <summary>
    /// The context slot
    /// </summary>
    [JsonProperty("contextSlot")]
    public BigInteger ContextSlot { get; set; }
    
    /// <summary>
    /// The route plan
    /// </summary>
    [JsonProperty("routePlan")]
    public List<RouteStep> RoutePlan { get; set; }
    
    /// <summary>
    /// The time taken
    /// </summary>
    [JsonProperty("timeTaken")]
    public BigInteger TimeTaken { get; set; }
}

/// <summary>
/// A route step
/// </summary>
public class RouteStep
{
    /// <summary>
    /// The swap info
    /// </summary>
    [JsonProperty("swapInfo")]
    public SwapInfo SwapInfo { get; set; }
    
    /// <summary>
    /// The percent
    /// </summary>
    [JsonProperty("Percent")]
    public ushort percent { get; set; }
}

/// <summary>
/// The swap info
/// </summary>
public class SwapInfo
{
    /// <summary>
    /// The AMM key
    /// </summary>
    [JsonProperty("ammKey")]
    public string AmmKey { get; set; }
    
    /// <summary>
    /// The AMM label
    /// </summary>
    [JsonProperty("label")]
    public string Label { get; set; }
    
    /// <summary>
    /// The input mint
    /// </summary>
    [JsonProperty("inputMint")]
    public string InputMint { get; set; }
    
    /// <summary>
    /// The output mint
    /// </summary>
    [JsonProperty("outputMint")]
    public string OutputMint { get; set; }
    
    /// <summary>
    /// The input amount
    /// </summary>
    [JsonProperty("inAmount")]
    public string InAmount { get; set; }
    
    /// <summary>
    /// The output amount
    /// </summary>
    [JsonProperty("outAmount")]
    public string OutAmount { get; set; }
    
    /// <summary>
    /// The fee amount
    /// </summary>
    [JsonProperty("feeAmount")]
    public string FeeAmount { get; set; }
    
    /// <summary>
    /// The fee mint
    /// </summary>
    [JsonProperty("feeMint")]
    public string FeeMint { get; set; }
}

/// <summary>
/// The platform fee
/// </summary>
public class PlatformFee
{
    /// <summary>
    /// The fee amount
    /// </summary>
    [JsonProperty("amount")]
    public string Amount { get; set; }
    
    /// <summary>
    /// The fee amount
    /// </summary>
    [JsonProperty("feeBps")]
    public ushort FeeBps { get; set; }
}

/// <summary>
/// The swap mode
/// </summary>
public enum SwapMode
{
    ExactIn,
    ExactOut
}