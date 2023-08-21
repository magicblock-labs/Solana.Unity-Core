using Solana.Unity.Dex.Quotes;
using Solana.Unity.Wallet;
using BigInteger = System.Numerics.BigInteger;

namespace Solana.Unity.Dex.Jupiter.Core.Types.Http;

/// <summary>
/// The request to get a swap quote from the Jupiter aggregator.
/// </summary>
public class SwapRequest
{
    /// <summary>
    /// The swap quote
    /// </summary>
    public SwapQuoteAg QuoteResponse { get; set; }
    
    /// <summary>
    /// The user's public key
    /// </summary>
    public string UserPublicKey { get; set; }
    
    /// <summary>
    /// The user's token account
    /// </summary>
    public PublicKey DestinationTokenAccount { get; set; }
    
    /// <summary>
    ///  The user's token account
    /// </summary>
    public bool WrapAndUnwrapSol { get; set; }
    
    /// <summary>
    /// The user's token account
    /// </summary>
    public bool UseSharedAccounts { get; set; }
    
    /// <summary>
    /// The user's token account
    /// </summary>
    public PublicKey FeeAccount { get; set; }
    
    /// <summary>
    /// The user's token account
    /// </summary>
    public BigInteger? ComputeUnitPriceMicroLamports { get; set; }
    
    /// <summary>
    /// The user's token account
    /// </summary>
    public bool UseTokenLedger { get; set; }

    /// <summary>
    /// Is this a legacy transaction
    /// </summary>
    public bool AsLegacyTransaction { get; set; }
}