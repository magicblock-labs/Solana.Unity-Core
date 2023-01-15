using Solana.Unity.Programs.Utilities;

namespace Solana.Unity.Dex.Orca.Core.Types
{
    public partial class OpenPositionBumps
    {
        public byte PositionBump { get; set; }

        public int Serialize(byte[] _data, int initialOffset)
        {
            int offset = initialOffset;
            _data.WriteU8(PositionBump, offset);
            offset += 1;
            return offset - initialOffset;
        }

        public static int Deserialize(System.ReadOnlySpan<byte> data, int initialOffset, out OpenPositionBumps result)
        {
            int offset = initialOffset;
            result = new OpenPositionBumps();
            result.PositionBump = data.GetU8(offset);
            offset += 1;
            return offset - initialOffset;
        }
    }
}