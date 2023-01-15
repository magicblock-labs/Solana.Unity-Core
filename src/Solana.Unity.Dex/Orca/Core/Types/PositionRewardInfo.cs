using System.Numerics;

using Solana.Unity.Programs.Utilities;

namespace Solana.Unity.Dex.Orca.Core.Types
{
    public partial class PositionRewardInfo
    {
        public BigInteger GrowthInsideCheckpoint { get; set; }

        public ulong AmountOwed { get; set; }

        public int Serialize(byte[] data, int initialOffset)
        {
            int offset = initialOffset;
            data.WriteBigInt(GrowthInsideCheckpoint, offset, 16, true);
            offset += 16;
            data.WriteU64(AmountOwed, offset);
            offset += 8;
            return offset - initialOffset;
        }

        public static int Deserialize(System.ReadOnlySpan<byte> data, int initialOffset, out PositionRewardInfo result)
        {
            int offset = initialOffset;
            result = new PositionRewardInfo();
            result.GrowthInsideCheckpoint = data.GetBigInt(offset, 16, false);
            offset += 16;
            result.AmountOwed = data.GetU64(offset);
            offset += 8;
            return offset - initialOffset;
        }
    }
}