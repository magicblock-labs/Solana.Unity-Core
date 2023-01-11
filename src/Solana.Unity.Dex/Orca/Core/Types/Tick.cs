using System.Numerics;

using Solana.Unity.Programs.Utilities;

namespace Solana.Unity.Dex.Orca.Core.Types
{
    public partial class Tick
    {
        public bool Initialized { get; set; }

        public BigInteger LiquidityNet { get; set; }

        public BigInteger LiquidityGross { get; set; }

        public BigInteger FeeGrowthOutsideA { get; set; }

        public BigInteger FeeGrowthOutsideB { get; set; }

        public BigInteger[] RewardGrowthsOutside { get; set; }

        public int Serialize(byte[] data, int initialOffset)
        {
            int offset = initialOffset;
            
            data.WriteBool(Initialized, offset);
            offset += 1;
            data.WriteBigInt(LiquidityNet, offset, 16, false);
            offset += 16;
            data.WriteBigInt(LiquidityGross, offset, 16, true);
            offset += 16;
            data.WriteBigInt(FeeGrowthOutsideA, offset, 16, true);
            offset += 16;
            data.WriteBigInt(FeeGrowthOutsideB, offset, 16, true);
            offset += 16;
            foreach (var rewardGrowthsOutsideElement in RewardGrowthsOutside)
            {
                data.WriteBigInt(rewardGrowthsOutsideElement, offset, 16, true);
                offset += 16;
            }

            return offset - initialOffset;
        }

        public static int Deserialize(System.ReadOnlySpan<byte> data, int initialOffset, out Tick result)
        {
            int offset = initialOffset;
            result = new Tick();
            result.Initialized = data.GetBool(offset);
            offset += 1;
            result.LiquidityNet = data.GetBigInt(offset, 16, true);
            offset += 16;
            result.LiquidityGross = data.GetBigInt(offset, 16, false);
            offset += 16;
            result.FeeGrowthOutsideA = data.GetBigInt(offset, 16, false);
            offset += 16;
            result.FeeGrowthOutsideB = data.GetBigInt(offset, 16, false);
            offset += 16;
            result.RewardGrowthsOutside = new BigInteger[3];
            for (uint resultRewardGrowthsOutsideIdx = 0;
                 resultRewardGrowthsOutsideIdx < 3;
                 resultRewardGrowthsOutsideIdx++)
            {
                result.RewardGrowthsOutside[resultRewardGrowthsOutsideIdx] = data.GetBigInt(offset, 16, false);
                offset += 16;
            }

            return offset - initialOffset;
        }
    }
}