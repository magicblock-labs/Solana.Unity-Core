namespace Solana.Unity.Dex.Orca.Math
{
    public static class MathConstants
    {
        /// <summary>
        /// The maximum sqrt-price supported by the Whirlpool program.
        /// </summary>
        public static readonly string MAX_SQRT_PRICE = "79226673515401279992447579055";

        /// <summary>
        /// The minimum sqrt-price supported by the Whirlpool program.
        /// </summary>
        public static readonly string MIN_SQRT_PRICE = "4295048016";

        /// <summary>
        /// The maximum number of tick-arrays that can traversed across in a swap.
        /// </summary>
        public static readonly int MAX_SWAP_TICK_ARRAYS = 3;
    }
}