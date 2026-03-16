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

    /// <summary>
    /// To specify a level or amount of additional fees to prioritize the transaction
    /// It can be used for both priority fee and jito tip
    /// </summary>
    public PrioritizationFeeLamportsContainer PrioritizationFeeLamports { get; set; }
}

/// <summary>
/// Represents prioritization fee settings
/// </summary>
public class PrioritizationFeeLamportsContainer
{
    /// <summary>
    /// Represents the max Lamports and priority level
    /// </summary>
    public PriorityLevelWithMaxLamports PriorityLevelWithMaxLamports { get; set; }
}

/// <summary>
/// Represents the max Lamports and priority level
/// </summary>
public class PriorityLevelWithMaxLamports
{
    /// <summary>
    /// Maximum lamports to cap the priority fee estimation, to prevent overpaying
    /// </summary>
    public ulong MaxLamports { get; set; }

    /// <summary>
    /// Either medium, high or veryHigh
    /// </summary>
    public string PriorityLevel { get; set; }  // Example: "veryHigh"
}