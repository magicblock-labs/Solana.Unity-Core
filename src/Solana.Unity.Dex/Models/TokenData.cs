using Solana.Unity.Wallet;

namespace Solana.Unity.Dex.Models;

/// <summary>
/// Encapsulates information about a token, retrieved from Orca's API. 
/// </summary>
public class TokenData
{
    /// <summary>
    /// The token's name
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// The token's mint
    /// </summary>
    public string Mint { get; set; }
        
    /// <summary>
    /// The token's mint address
    /// </summary>
    public PublicKey MintAddress => new PublicKey(Mint);
    
    /// <summary>
    /// The token's symbol
    /// </summary>
    public string Symbol { get; set; } 
        
    /// <summary>
    /// The token's decimals
    /// </summary>
    public int Decimals { get; set; }
            
    /// <summary>
    /// The token's logo URI
    /// </summary>
    public string LogoURI { get; set; }
            
    /// <summary>
    /// The token's Coingecko ID
    /// </summary>
    public string CoingeckoId { get; set; }
            
    /// <summary>
    /// If token is whitelisted
    /// </summary>
    public bool Whitelisted { get; set; }
    
    /// <summary>
    /// If token is a pool token
    /// </summary>
    public bool PoolToken { get; set; }

}