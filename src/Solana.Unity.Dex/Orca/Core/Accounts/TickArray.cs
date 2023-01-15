using Solana.Unity.Programs.Utilities;
using Solana.Unity.Wallet;

using Solana.Unity.Dex.Orca.Core.Types;

namespace Solana.Unity.Dex.Orca.Core.Accounts
{
    public partial class TickArray
    {
        public static ulong ACCOUNT_DISCRIMINATOR => 13493355605783306565UL;

        public static System.ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES =>
            new byte[] { 69, 97, 189, 190, 110, 7, 66, 187 };

        public static string ACCOUNT_DISCRIMINATOR_B58 => "Cc6F4MyvbgN";
        public int StartTickIndex { get; set; }

        public Tick[] Ticks { get; set; }

        public PublicKey Whirlpool { get; set; }

        public static TickArray Deserialize(System.ReadOnlySpan<byte> _data)
        {
            int offset = 0;
            ulong accountHashValue = _data.GetU64(offset);
            offset += 8;
            if (accountHashValue != ACCOUNT_DISCRIMINATOR)
            {
                return null;
            }

            TickArray result = new TickArray();
            result.StartTickIndex = _data.GetS32(offset);
            offset += 4;
            result.Ticks = new Tick[88];
            for (uint resultTicksIdx = 0; resultTicksIdx < 88; resultTicksIdx++)
            {
                offset += Tick.Deserialize(_data, offset, out var resultTicksresultTicksIdx);
                result.Ticks[resultTicksIdx] = resultTicksresultTicksIdx;
            }

            result.Whirlpool = _data.GetPubKey(offset);
            offset += 32;
            return result;
        }
    }
}