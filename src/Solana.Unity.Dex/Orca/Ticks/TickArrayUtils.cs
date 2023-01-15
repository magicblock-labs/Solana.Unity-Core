using System;
using System.Linq;

using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Core.Types;

namespace Solana.Unity.Dex.Orca.Ticks
{
    public static class TickArrayUtils
    {
        /// <summary>
        /// Get the tick from tickArray with a global tickIndex.
        /// </summary>
        /// <param name="tickArray"></param>
        /// <param name="tickIndex"></param>
        /// <param name="tickSpacing"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Tick GetTickFromArray(TickArray tickArray, int tickIndex, ushort tickSpacing)
        {
            long realIndex = TickUtils.TickIndexToInnerIndex(tickArray.StartTickIndex, tickIndex, tickSpacing);
            Tick tick = tickArray.Ticks[realIndex];
            if (tick == null)
            {
                throw new IndexOutOfRangeException(
                    $"tick realIndex out of range - start - ${tickArray.StartTickIndex} index - ${tickIndex}, realIndex - ${realIndex}");
            }

            return tick;
        }

        /// <summary>
        /// Evaluate a list of tick-array data and return the array of indices which the tick-arrays are not initialized.
        /// </summary>
        /// <param name="tickArrays">a list of TickArrayData or null objects from AccountFetcher.listTickArrays</param>
        /// <returns>an array of array-index for the input tickArrays that requires initialization.</returns>
        public static int[] GetUninitializedArrays(TickArray[] tickArrays)
        {
            return tickArrays.Select(
                (tickArray, i) => (tickArray == null) ? i : -1).Where(
                i => (i >= 0)).ToArray();
        }
    }
}