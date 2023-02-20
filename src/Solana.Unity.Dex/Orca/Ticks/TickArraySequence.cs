using System;
using System.Collections.Generic;

using Solana.Unity.Wallet;

using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Exceptions;
using Solana.Unity.Dex.Orca.Core.Types;

namespace Solana.Unity.Dex.Orca.Ticks
{
    public class TickArraySequence
    {
        private bool[] _touchedArrays;
        private int _startArrayIndex;
        private ushort _tickSpacing;
        private bool _aToB;
        private IList<TickArrayContainer> _tickArrays;

        public TickArraySequence(IList<TickArrayContainer> tickArrays, ushort tickSpacing, bool aToB)
        {
            this._tickArrays = tickArrays;
            this._tickSpacing = tickSpacing;
            this._aToB = aToB;
            
            if (tickArrays.Count == 0 || tickArrays[0].Data == null)
            {
                throw new System.Exception("TickArray index 0 must be initialized");
            }

            this._touchedArrays = new bool[tickArrays.Count];
            this._startArrayIndex = TickArrayIndex.FromTickIndex(
                tickArrays[0].Data.StartTickIndex,
                this._tickSpacing
            ).ArrayIndex;
        }

        public bool CheckArrayContainsTickIndex(int sequenceIndex, int tickIndex)
        {
            TickArray data = _tickArrays[sequenceIndex]?.Data;
            if (data == null)
                return false;

            return CheckIfIndexIsInTickArrayRange(data.StartTickIndex, tickIndex);
        }
        
        public bool IsValidTickArray0(int tickIndex)
        {
            int shift = _aToB ? 0 : _tickSpacing;
            TickArray data = _tickArrays[0]?.Data;
            if (data == null)
                return false;

            return CheckIfIndexIsInTickArrayRange(data.StartTickIndex, tickIndex + shift);
        }
        
        public bool CheckIfIndexIsInTickArrayRange(int startTick, int tickIndex)
        {
            int upperBound = startTick + _tickSpacing * TickConstants.TICK_ARRAY_SIZE;
            return tickIndex >= startTick && tickIndex < upperBound;
        }
        
        /// <summary>
        /// if a->b, currIndex is included in the search
        /// if b->a, currIndex is always ignored
        /// </summary>
        /// <param name="currentIndex">The index to search</param>
        /// <returns>A tuple of 1) the located tick's index, and 2) located tick's TickArrayData</returns>
        /// <exception cref="WhirlpoolsException">Throws if the given index is out of bounds</exception>
        public Tuple<int, Tick> FindNextInitializedTickIndex(int currentIndex)
        {
            int searchIndex = _aToB ? currentIndex : currentIndex + _tickSpacing;
            TickArrayIndex currTickArrayIndex = TickArrayIndex.FromTickIndex(searchIndex, this._tickSpacing);
            
            // Throw error if the search attempted to search for an index out of bounds
            if (!IsArrayIndexInBounds(currTickArrayIndex, _aToB))
            {
                throw new WhirlpoolsException(
                    $"Swap input value traversed too many arrays. Out of bounds at attempt to traverse tick index - ${currTickArrayIndex.ToTickIndex()}.",
                    SwapErrorCode.TickArraySequenceInvalid
                );
            }

            while (IsArrayIndexInBounds(currTickArrayIndex, _aToB))
            {
                Tick currTickData = GetTick(currTickArrayIndex.ToTickIndex());
                if (currTickData.Initialized)
                {
                    return Tuple.Create(currTickArrayIndex.ToTickIndex(), currTickData);
                }

                currTickArrayIndex = _aToB
                    ? currTickArrayIndex.ToPrevInitializableTickIndex()
                    : currTickArrayIndex.ToNextInitializableTickIndex();
            }

            int lastIndexInArray = System.Math.Max(
                System.Math.Min(
                    _aToB ? currTickArrayIndex.ToTickIndex() + _tickSpacing : currTickArrayIndex.ToTickIndex() - 1, 
                    TickConstants.MAX_TICK_INDEX),
                TickConstants.MIN_TICK_INDEX); 
                
            return Tuple.Create<int, Tick>(lastIndexInArray, null);
        }

        public Tick GetTick(int index)
        {
            TickArrayIndex targetTaIndex = TickArrayIndex.FromTickIndex(index, _tickSpacing);
            if (!IsArrayIndexInBounds(targetTaIndex, _aToB))
            {
                throw new WhirlpoolsException("Provided tick index is out of bounds for this sequence.");
            }

            int localArrayIndex = GetLocalArrayIndex(targetTaIndex.ArrayIndex, _aToB);
            TickArray tickArrayData = _tickArrays[localArrayIndex].Data;
            _touchedArrays[localArrayIndex] = true;

            if (tickArrayData == null)
            {
                throw new WhirlpoolsException(
                    $"TickArray at index ${localArrayIndex} is not initialized.",
                    SwapErrorCode.TickArrayIndexNotInitialized
                );
            }

            if (!CheckIfIndexIsInTickArrayRange(tickArrayData.StartTickIndex, index))
            {
                throw new WhirlpoolsException(
                    $"TickArray at index ${localArrayIndex} is unexpected for this sequence.",
                    SwapErrorCode.TickArraySequenceInvalid
                );
            }

            return tickArrayData.Ticks[targetTaIndex.OffsetIndex];
        }
        
        public IList<PublicKey> GetTouchedArrays(int minArraySize) 
        {
            List<PublicKey> result = new List<PublicKey>();
            
            for (int n=0; n<_touchedArrays.Length; n++) 
            {
                if (_touchedArrays[n])
                    result.Add(_tickArrays[n].Address);
            }
            
            // The quote object should contain the specified amount of tick arrays to be plugged
            // directly into the swap instruction.
            // If the result does not fit minArraySize, pad the rest with the last touched array
            if (result.Count > 0) {
                while (result.Count < minArraySize) {
                    result.Add(result[result.Count - 1]);
                }
            }
            
            return result; 
        }
        
        /// <summary>
        /// Check whether the array index potentially exists in this sequence.
        /// Note: assumes the sequence of tick-arrays are sequential
        /// </summary>
        /// <param name="index"></param>
        /// <param name="aToB"></param>
        /// <returns>True if the given index is in bounds of the array.</returns>
        private bool IsArrayIndexInBounds(TickArrayIndex index, bool aToB)
        {
            long localArrayIndex = GetLocalArrayIndex(index.ArrayIndex, aToB);
            int seqLength = _tickArrays.Count;
            return localArrayIndex >= 0 && localArrayIndex < seqLength;
        }

        private int GetLocalArrayIndex(int arrayIndex, bool aToB)
        {
            return aToB 
                ? this._startArrayIndex - arrayIndex 
                : arrayIndex - this._startArrayIndex;
        }
    }
}
