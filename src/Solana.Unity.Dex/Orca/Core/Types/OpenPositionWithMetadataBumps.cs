using Solana.Unity.Programs.Utilities;

namespace Solana.Unity.Dex.Orca.Core.Types
{
    public partial class OpenPositionWithMetadataBumps
    {
        public byte PositionBump { get; set; }

        public byte MetadataBump { get; set; }

        public int Serialize(byte[] data, int initialOffset)
        {
            int offset = initialOffset;
            data.WriteU8(PositionBump, offset);
            offset += 1;
            data.WriteU8(MetadataBump, offset);
            offset += 1;
            return offset - initialOffset;
        }

        public static int Deserialize(System.ReadOnlySpan<byte> data, int initialOffset,
            out OpenPositionWithMetadataBumps result)
        {
            int offset = initialOffset;
            result = new OpenPositionWithMetadataBumps();
            result.PositionBump = data.GetU8(offset);
            offset += 1;
            result.MetadataBump = data.GetU8(offset);
            offset += 1;
            return offset - initialOffset;
        }
    }
}