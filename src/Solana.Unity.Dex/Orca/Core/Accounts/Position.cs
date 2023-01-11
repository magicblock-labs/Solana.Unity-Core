using System.Numerics;
using Solana.Unity.Programs.Utilities;
using Solana.Unity.Wallet;

using Solana.Unity.Dex.Orca.Core.Types;

namespace Solana.Unity.Dex.Orca.Core.Accounts
{
    public partial class Position
    {
        public static ulong ACCOUNT_DISCRIMINATOR => 15057574775701355690UL;

        public static System.ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES =>
            new byte[] { 170, 188, 143, 228, 122, 64, 247, 208 };

        public static string ACCOUNT_DISCRIMINATOR_B58 => "VZMoMoKgZQb";
        public PublicKey Whirlpool { get; set; }

        public PublicKey PositionMint { get; set; }

        public BigInteger Liquidity { get; set; }

        public int TickLowerIndex { get; set; }

        public int TickUpperIndex { get; set; }

        public BigInteger FeeGrowthCheckpointA { get; set; }

        public ulong FeeOwedA { get; set; }

        public BigInteger FeeGrowthCheckpointB { get; set; }

        public ulong FeeOwedB { get; set; }

        public PositionRewardInfo[] RewardInfos { get; set; }

        public static Position Deserialize(System.ReadOnlySpan<byte> _data)
        {
            int offset = 0;
            ulong accountHashValue = _data.GetU64(offset);
            offset += 8;
            if (accountHashValue != ACCOUNT_DISCRIMINATOR)
            {
                return null;
            }

            Position result = new Position();
            result.Whirlpool = _data.GetPubKey(offset);
            offset += 32;
            result.PositionMint = _data.GetPubKey(offset);
            offset += 32;
            result.Liquidity = _data.GetBigInt(offset, 16, false);
            offset += 16;
            result.TickLowerIndex = _data.GetS32(offset);
            offset += 4;
            result.TickUpperIndex = _data.GetS32(offset);
            offset += 4;
            result.FeeGrowthCheckpointA = _data.GetBigInt(offset, 16, false);
            offset += 16;
            result.FeeOwedA = _data.GetU64(offset);
            offset += 8;
            result.FeeGrowthCheckpointB = _data.GetBigInt(offset, 16, false);
            offset += 16;
            result.FeeOwedB = _data.GetU64(offset);
            offset += 8;
            result.RewardInfos = new PositionRewardInfo[3];
            for (uint resultRewardInfosIdx = 0; resultRewardInfosIdx < 3; resultRewardInfosIdx++)
            {
                offset += PositionRewardInfo.Deserialize(_data, offset, out var resultRewardInfosresultRewardInfosIdx);
                result.RewardInfos[resultRewardInfosIdx] = resultRewardInfosresultRewardInfosIdx;
            }

            return result;
        }
    }
}