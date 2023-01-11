using System.Numerics;

namespace Solana.Unity.Dex.Quotes;

/// <summary>
/// A collection of estimated values from quoting an increase liquidity.
/// </summary>
public class IncreaseLiquidityQuote
{
    public BigInteger LiquidityAmount { get; set; }
    public BigInteger TokenMaxA { get; set; }
    public BigInteger TokenMaxB { get; set; }
    public BigInteger TokenEstA { get; set; }
    public BigInteger TokenEstB { get; set; }
}