using System;

using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Exceptions;

namespace Solana.Unity.Dex.Orca.Ticks
{
    public static class TickUtils
    {
        public static int GetStartTickIndex(int tickIndex, ushort tickSpacing, int offset = 0)
        {
            double realIndex = System.Math.Floor((double)tickIndex / tickSpacing / TickConstants.TICK_ARRAY_SIZE);
            double startTickIndex = (realIndex + offset) * tickSpacing * TickConstants.TICK_ARRAY_SIZE;
            
            int ticksInArray = TickConstants.TICK_ARRAY_SIZE * tickSpacing;
            int minTickIndex = TickConstants.MIN_TICK_INDEX - ((TickConstants.MIN_TICK_INDEX % ticksInArray) + ticksInArray);
            if (startTickIndex < minTickIndex)
                throw new WhirlpoolsException($"startTickIndex (${startTickIndex}) >= minTickIndex ({minTickIndex})");
            if (startTickIndex > TickConstants.MAX_TICK_INDEX)
                throw new WhirlpoolsException($"startTickIndex (${startTickIndex}) <= TickConstants.MAX_TICK_INDEX ({TickConstants.MAX_TICK_INDEX})");
            return (int)startTickIndex;
        }

        public static int GetInitializableTickIndex(int tickIndex, ushort tickSpacing)
        {
            return tickIndex - (tickIndex % tickSpacing);
        }

        public static int GetNextInitializableTickIndex(int tickIndex, ushort tickSpacing)
        {
            return TickUtils.GetInitializableTickIndex(tickIndex, tickSpacing) + tickSpacing;
        }

        public static int GetPrevInitializableTickIndex(int tickIndex, ushort tickSpacing)
        {
            return TickUtils.GetInitializableTickIndex(tickIndex, tickSpacing) - tickSpacing;
        }

        public static int? FindPreviousInitializedTickIndex(
            TickArray account,
            int currentTickIndex,
            ushort tickSpacing
        )
        {
            return TickUtils.FindInitializedTick(account, currentTickIndex, tickSpacing, TickSearchDirection.Left);
        }

        public static int? FindNextInitializedTickIndex(
            TickArray account,
            int currentTickIndex,
            ushort tickSpacing
        )
        {
            return TickUtils.FindInitializedTick(account, currentTickIndex, tickSpacing, TickSearchDirection.Right);
        }

        public static int? FindInitializedTick(
            TickArray account,
            int currentTickIndex,
            ushort tickSpacing,
            TickSearchDirection searchDirection
        )
        {
            int currentTickArrayIndex = TickIndexToInnerIndex(account.StartTickIndex, currentTickIndex, tickSpacing);
            
            int increment = (searchDirection == TickSearchDirection.Right) ? 1 : -1;
            
            int stepInitializedTickArrayIndex = (searchDirection == TickSearchDirection.Right) ?
                currentTickArrayIndex + increment :
                currentTickArrayIndex; 
                
            while (
                stepInitializedTickArrayIndex >= 0 &&
                stepInitializedTickArrayIndex < account.Ticks.Length
            ) {
                if ((account.Ticks[stepInitializedTickArrayIndex]?.Initialized).Value)
                {
                    return InnerIndexToTickIndex(
                        account.StartTickIndex, stepInitializedTickArrayIndex, tickSpacing
                    );
                }
            }
            
            return null;
        }
        
        public static int TickIndexToInnerIndex(int startTickIndex, int tickIndex, ushort tickSpacing)
        {
            return (int)System.Math.Floor((tickIndex - startTickIndex) / (double)tickSpacing);
        }

        public static int InnerIndexToTickIndex(int startTickIndex, int tickArrayIndex, ushort tickSpacing)
        {
            return startTickIndex + tickArrayIndex * tickSpacing;
        }

        public static bool CheckTickInBounds(int tick)
        {
            return tick <= TickConstants.MAX_TICK_INDEX && tick >= TickConstants.MIN_TICK_INDEX;
        }

        public static bool IsTickInitializable(int tick, ushort tickSpacing)
        {
            return tick % tickSpacing == 0;
        }
    }
    
    public enum TickSearchDirection { Left, Right }
}
