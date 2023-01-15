using Solana.Unity.Wallet;
using System.Numerics;

namespace Solana.Unity.Dex.Quotes;

/// <summary> 
/// A collection of estimated values from quoting a swap.
/// </summary>
public class SwapQuote{
    /// <summary>
    /// Approximate number of input token swapped in the swap
    /// </summary>
    public BigInteger EstimatedAmountIn { get; set; }

    /// <summary>
    /// Approximate number of output token swapped in the swap
    /// </summary>
    public BigInteger EstimatedAmountOut { get; set; }
        
    /// <summary> 
    /// estimatedEndTickIndex - Approximate tick-index the Whirlpool will land on after this swap
    /// </summary>
    public int EstimatedEndTickIndex { get; set; }

    /// <summary> 
    /// estimatedEndSqrtPrice - Approximate sqrtPrice the Whirlpool will land on after this swap
    /// </summary>
    public BigInteger EstimatedEndSqrtPrice { get; set; }

    /// <summary> 
    /// estimatedFeeAmount - Approximate feeAmount (all fees) charged on this swap
    /// </summary>
    public BigInteger EstimatedFeeAmount { get; set; }

    public bool AtoB { get; set; }
    public BigInteger Amount { get; set; }
    public BigInteger SqrtPriceLimit { get; set; }
    public BigInteger OtherAmountThreshold { get; set; }
    public bool AmountSpecifiedIsInput { get; set; }
    public PublicKey TickArray0 { get; set; }
    public PublicKey TickArray1 { get; set; }
    public PublicKey TickArray2 { get; set; }
}