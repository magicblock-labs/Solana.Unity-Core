using Solana.Unity.Wallet;

using Solana.Unity.Dex.Test.Orca.Params;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public class FundedPositionInfo
    {
        public OpenPositionParams OpenPositionParams { get; set; }
        public PublicKey PublicKey { get; set; }
        public PublicKey TokenAccount { get; set; }
        public Account MintKeyPair { get; set; }
        public PublicKey TickArrayLower { get; set; }
        public PublicKey TickArrayUpper { get; set; }
    }
}