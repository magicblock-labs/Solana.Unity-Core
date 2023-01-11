using System;

namespace Solana.Unity.Dex.Orca.Ticks
{
    /// <summary>
    /// Encapsulates a tick array. 
    /// </summary>
    public class TickArrayIndex
    {
        private ushort _tickSpacing;

        public int ArrayIndex { get; private set; }
        public int OffsetIndex { get; private set; }

        public TickArrayIndex(int arrayIndex, int offsetIndex, ushort tickSpacing)
        {
            if (offsetIndex >= TickConstants.TICK_ARRAY_SIZE)
            {
                throw new Exception("Invalid offsetIndex - value has to be smaller than TICK_ARRAY_SIZE");
            }
            if (offsetIndex < 0)
            {
                throw new Exception("Invalid offsetIndex - value is smaller than 0");
            }

            this.ArrayIndex = arrayIndex;
            this.OffsetIndex = offsetIndex;
            _tickSpacing = tickSpacing;
        }

        public static TickArrayIndex FromTickIndex(int index, ushort tickSpacing)
        {
            int arrayIndex = (int)System.Math.Floor(System.Math.Floor((double)index / (double)tickSpacing) / TickConstants.TICK_ARRAY_SIZE);
            int offsetIndex = (int)System.Math.Floor((index % (tickSpacing * TickConstants.TICK_ARRAY_SIZE)) / (double)tickSpacing);
            if (offsetIndex < 0)
            {
                offsetIndex = TickConstants.TICK_ARRAY_SIZE + offsetIndex;
            }

            return new TickArrayIndex(arrayIndex, offsetIndex, tickSpacing);
        }

        public int ToTickIndex() => this.ArrayIndex * TickConstants.TICK_ARRAY_SIZE * this._tickSpacing +
                                     this.OffsetIndex * this._tickSpacing;

        public TickArrayIndex ToNextInitializableTickIndex() => TickArrayIndex.FromTickIndex(
            this.ToTickIndex() + this._tickSpacing, this._tickSpacing);

        public TickArrayIndex ToPrevInitializableTickIndex() => TickArrayIndex.FromTickIndex(
            this.ToTickIndex() - this._tickSpacing, this._tickSpacing);
    }
}
