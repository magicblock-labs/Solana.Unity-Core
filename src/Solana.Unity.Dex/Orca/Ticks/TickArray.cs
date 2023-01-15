using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Wallet;


namespace Solana.Unity.Dex.Orca.Ticks
{
    /// <summary>
    /// Encapsulates a tick array with its address. 
    /// </summary>
    public class TickArrayContainer
    {
        public PublicKey Address { get; set; }
        public TickArray Data { get; set; }
    }
}
