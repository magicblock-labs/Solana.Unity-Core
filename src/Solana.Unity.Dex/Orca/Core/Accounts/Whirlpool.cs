using System.Numerics;
using Solana.Unity.Programs.Utilities;
using Solana.Unity.Wallet;

using Solana.Unity.Dex.Orca.Core.Types;

namespace Solana.Unity.Dex.Orca.Core.Accounts
{
    public partial class Whirlpool
    {
        public static ulong ACCOUNT_DISCRIMINATOR => 676526073106765119UL;

        public static System.ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES =>
            new byte[] { 63, 149, 209, 12, 225, 128, 99, 9 };

        public static string ACCOUNT_DISCRIMINATOR_B58 => "BdrfaPg3xM6";
        public PublicKey WhirlpoolsConfig { get; set; }

        public byte[] WhirlpoolBump { get; set; }

        public ushort TickSpacing { get; set; }

        public byte[] TickSpacingSeed { get; set; }

        public ushort FeeRate { get; set; }

        public ushort ProtocolFeeRate { get; set; }

        public BigInteger Liquidity { get; set; }

        public BigInteger SqrtPrice { get; set; }

        public int TickCurrentIndex { get; set; }

        public ulong ProtocolFeeOwedA { get; set; }

        public ulong ProtocolFeeOwedB { get; set; }

        public PublicKey TokenMintA { get; set; }

        public PublicKey TokenVaultA { get; set; }

        public BigInteger FeeGrowthGlobalA { get; set; }

        public PublicKey TokenMintB { get; set; }

        public PublicKey TokenVaultB { get; set; }

        public BigInteger FeeGrowthGlobalB { get; set; }

        public ulong RewardLastUpdatedTimestamp { get; set; }

        public WhirlpoolRewardInfo[] RewardInfos { get; set; }
        public PublicKey Address { get; set; }

        public static Whirlpool Deserialize(System.ReadOnlySpan<byte> _data)
        {
            int offset = 0;
            ulong accountHashValue = _data.GetU64(offset);
            offset += 8;
            if (accountHashValue != ACCOUNT_DISCRIMINATOR)
            {
                return null;
            }

            Whirlpool result = new Whirlpool();
            result.WhirlpoolsConfig = _data.GetPubKey(offset);
            offset += 32;
            result.WhirlpoolBump = _data.GetBytes(offset, 1);
            offset += 1;
            result.TickSpacing = _data.GetU16(offset);
            offset += 2;
            result.TickSpacingSeed = _data.GetBytes(offset, 2);
            offset += 2;
            result.FeeRate = _data.GetU16(offset);
            offset += 2;
            result.ProtocolFeeRate = _data.GetU16(offset);
            offset += 2;
            result.Liquidity = _data.GetBigInt(offset, 16, false);
            offset += 16;
            result.SqrtPrice = _data.GetBigInt(offset, 16, false);
            offset += 16;
            result.TickCurrentIndex = _data.GetS32(offset);
            offset += 4;
            result.ProtocolFeeOwedA = _data.GetU64(offset);
            offset += 8;
            result.ProtocolFeeOwedB = _data.GetU64(offset);
            offset += 8;
            result.TokenMintA = _data.GetPubKey(offset);
            offset += 32;
            result.TokenVaultA = _data.GetPubKey(offset);
            offset += 32;
            result.FeeGrowthGlobalA = _data.GetBigInt(offset, 16, false);
            offset += 16;
            result.TokenMintB = _data.GetPubKey(offset);
            offset += 32;
            result.TokenVaultB = _data.GetPubKey(offset);
            offset += 32;
            result.FeeGrowthGlobalB = _data.GetBigInt(offset, 16, false);
            offset += 16;
            result.RewardLastUpdatedTimestamp = _data.GetU64(offset);
            offset += 8;
            result.RewardInfos = new WhirlpoolRewardInfo[3];
            for (uint resultRewardInfosIdx = 0; resultRewardInfosIdx < 3; resultRewardInfosIdx++)
            {
                offset += WhirlpoolRewardInfo.Deserialize(_data, offset, out var resultRewardInfosresultRewardInfosIdx);
                result.RewardInfos[resultRewardInfosIdx] = resultRewardInfosresultRewardInfosIdx;
            }

            return result;
        }
    }
}