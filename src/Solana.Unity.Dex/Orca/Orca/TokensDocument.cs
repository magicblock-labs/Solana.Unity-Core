using Solana.Unity.Dex.Models;
using System.Collections.Generic;

namespace Solana.Unity.Dex.Orca.Orca;

/// <summary>
/// Serialized from JSON; represents the entire JSON output. 
/// </summary>
public class TokensDocument
{
    public List<TokenData> tokens; 
}