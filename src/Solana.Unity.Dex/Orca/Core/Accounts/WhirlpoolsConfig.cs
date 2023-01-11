using Solana.Unity.Programs.Utilities;
using Solana.Unity.Wallet;

namespace Solana.Unity.Dex.Orca.Core.Accounts
{
    public partial class WhirlpoolsConfig
    {
        public static ulong ACCOUNT_DISCRIMINATOR => 18357050149419685021UL;

        public static System.ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES =>
            new byte[] { 157, 20, 49, 224, 217, 87, 193, 254 };

        public static string ACCOUNT_DISCRIMINATOR_B58 => "TGrzdk13ciR";
        public PublicKey FeeAuthority { get; set; }

        public PublicKey CollectProtocolFeesAuthority { get; set; }

        public PublicKey RewardEmissionsSuperAuthority { get; set; }

        public ushort DefaultProtocolFeeRate { get; set; }

        public static WhirlpoolsConfig Deserialize(System.ReadOnlySpan<byte> _data)
        {
            int offset = 0;
            ulong accountHashValue = _data.GetU64(offset);
            offset += 8;
            if (accountHashValue != ACCOUNT_DISCRIMINATOR)
            {
                return null;
            }

            WhirlpoolsConfig result = new WhirlpoolsConfig();
            result.FeeAuthority = _data.GetPubKey(offset);
            offset += 32;
            result.CollectProtocolFeesAuthority = _data.GetPubKey(offset);
            offset += 32;
            result.RewardEmissionsSuperAuthority = _data.GetPubKey(offset);
            offset += 32;
            result.DefaultProtocolFeeRate = _data.GetU16(offset);
            offset += 2;
            return result;
        }
    }
}