using System.Numerics;

using Solana.Unity.Programs.Utilities;
using Solana.Unity.Wallet;

namespace Solana.Unity.Dex.Orca.Core.Types
{
    public partial class WhirlpoolRewardInfo
    {
        public PublicKey Mint { get; set; }

        public PublicKey Vault { get; set; }

        public PublicKey Authority { get; set; }

        public BigInteger EmissionsPerSecondX64 { get; set; }

        public BigInteger GrowthGlobalX64 { get; set; }

        public int Serialize(byte[] data, int initialOffset)
        {
            int offset = initialOffset;
            data.WritePubKey(Mint, offset);
            offset += 32;
            data.WritePubKey(Vault, offset);
            offset += 32;
            data.WritePubKey(Authority, offset);
            offset += 32;
            data.WriteBigInt(EmissionsPerSecondX64, offset, 16, true);
            offset += 16;
            data.WriteBigInt(GrowthGlobalX64, offset, 16, true);
            offset += 16;
            return offset - initialOffset;
        }

        public static int Deserialize(System.ReadOnlySpan<byte> data, int initialOffset, out WhirlpoolRewardInfo result)
        {
            int offset = initialOffset;
            result = new WhirlpoolRewardInfo();
            result.Mint = data.GetPubKey(offset);
            offset += 32;
            result.Vault = data.GetPubKey(offset);
            offset += 32;
            result.Authority = data.GetPubKey(offset);
            offset += 32;
            result.EmissionsPerSecondX64 = data.GetBigInt(offset, 16, false);
            offset += 16;
            result.GrowthGlobalX64 = data.GetBigInt(offset, 16, false);
            offset += 16;
            return offset - initialOffset;
        }
    }
}