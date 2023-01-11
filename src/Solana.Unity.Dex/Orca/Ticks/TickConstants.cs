namespace Solana.Unity.Dex.Orca.Ticks
{
    public static class TickConstants
    {
        /// <summary>
        /// The number of initialized ticks that a tick-array account can hold.
        /// </summary>
        public static readonly int TICK_ARRAY_SIZE = 88;

        /// <summary>
        /// The maximum number of tick-arrays that can traversed across in a swap.
        /// </summary>
        public static readonly int MAX_SWAP_TICK_ARRAYS = 3;

        /// <summary>
        /// The maximum tick index supported by the Whirlpool program.
        /// </summary>
        public static readonly int MAX_TICK_INDEX = 443636;

        /// <summary>
        /// The minimum tick index supported by the Whirlpool program.
        /// </summary>
        public static readonly int MIN_TICK_INDEX = -443636;
    }
}
