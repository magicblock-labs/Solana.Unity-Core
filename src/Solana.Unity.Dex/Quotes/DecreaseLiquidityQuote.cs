using System.Numerics;

namespace Solana.Unity.Dex.Quotes;

/// <summary>
/// A collection of estimated values from quoting a decrease liquidity.
/// </summary>
public class DecreaseLiquidityQuote
{
    public BigInteger LiquidityAmount { get; set; }
    public BigInteger TokenMinA { get; set; }
    public BigInteger TokenMinB { get; set; }
    public BigInteger TokenEstA { get; set; }
    public BigInteger TokenEstB { get; set; }
}