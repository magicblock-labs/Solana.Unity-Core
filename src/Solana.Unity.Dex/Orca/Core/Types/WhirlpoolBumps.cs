using Solana.Unity.Programs.Utilities;

namespace Solana.Unity.Dex.Orca.Core.Types
{
    public partial class WhirlpoolBumps
    {
        public byte WhirlpoolBump { get; set; }

        public int Serialize(byte[] data, int initialOffset)
        {
            int offset = initialOffset;
            data.WriteU8(WhirlpoolBump, offset);
            offset += 1;
            return offset - initialOffset;
        }

        public static int Deserialize(System.ReadOnlySpan<byte> data, int initialOffset, out WhirlpoolBumps result)
        {
            int offset = initialOffset;
            result = new WhirlpoolBumps();
            result.WhirlpoolBump = data.GetU8(offset);
            offset += 1;
            return offset - initialOffset;
        }
    }
}