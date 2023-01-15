using Solana.Unity.Programs.Utilities;
using Solana.Unity.Wallet;

namespace Solana.Unity.Dex.Orca.Core.Accounts
{
    public partial class FeeTier
    {
        public static ulong ACCOUNT_DISCRIMINATOR => 7619602997519010616UL;

        public static System.ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES =>
            new byte[] { 56, 75, 159, 76, 142, 68, 190, 105 };

        public static string ACCOUNT_DISCRIMINATOR_B58 => "AR8t9QRDQXa";
        public PublicKey WhirlpoolsConfig { get; set; }

        public ushort TickSpacing { get; set; }

        public ushort DefaultFeeRate { get; set; }

        public static FeeTier Deserialize(System.ReadOnlySpan<byte> _data)
        {
            int offset = 0;
            ulong accountHashValue = _data.GetU64(offset);
            offset += 8;
            if (accountHashValue != ACCOUNT_DISCRIMINATOR)
            {
                return null;
            }

            FeeTier result = new FeeTier();
            result.WhirlpoolsConfig = _data.GetPubKey(offset);
            offset += 32;
            result.TickSpacing = _data.GetU16(offset);
            offset += 2;
            result.DefaultFeeRate = _data.GetU16(offset);
            offset += 2;
            return result;
        }
    }
}