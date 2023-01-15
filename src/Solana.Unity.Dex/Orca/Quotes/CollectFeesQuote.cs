using System.Numerics; 

using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Core.Types;
using Solana.Unity.Dex.Orca.Core.Accounts;
    
namespace Solana.Unity.Dex.Orca.Quotes
{
    public class CollectFeesQuote
    {
        public BigInteger FeeOwedA { get; private set; }
        public BigInteger FeeOwedB { get; private set; }
        
        public CollectFeesQuote(
            Whirlpool whirlpool,
            Position position,
            Tick tickLower, 
            Tick tickUpper)
        {
            BigInteger feeGrowthBelowA = 0;
            BigInteger feeGrowthBelowB = 0;

            BigInteger feeGrowthAboveA = 0;
            BigInteger feeGrowthAboveB = 0;

            BigInteger feeGrowthInsideA = 0;
            BigInteger feeGrowthInsideB = 0;

            //fee growth below 
            if (whirlpool.TickCurrentIndex < position.TickLowerIndex)
            {
                feeGrowthBelowA = ArithmeticUtils.SubUnderflowU128(
                    whirlpool.FeeGrowthGlobalA, tickLower.FeeGrowthOutsideA
                );
                feeGrowthBelowB = ArithmeticUtils.SubUnderflowU128(
                    whirlpool.FeeGrowthGlobalB, tickLower.FeeGrowthOutsideB
                );
            }
            else
            {
                feeGrowthBelowA = tickLower.FeeGrowthOutsideA;
                feeGrowthBelowB = tickLower.FeeGrowthOutsideB;
            }

            //fee growth above 
            if (whirlpool.TickCurrentIndex < position.TickUpperIndex)
            {
                feeGrowthAboveA = tickUpper.FeeGrowthOutsideA;
                feeGrowthAboveB = tickUpper.FeeGrowthOutsideB;
            }
            else
            {
                feeGrowthAboveA = ArithmeticUtils.SubUnderflowU128(
                    whirlpool.FeeGrowthGlobalA, tickUpper.FeeGrowthOutsideA
                );
                feeGrowthAboveB = ArithmeticUtils.SubUnderflowU128(
                    whirlpool.FeeGrowthGlobalB, tickUpper.FeeGrowthOutsideB
                );
            }

            //fee growth inside 
            feeGrowthInsideA = ArithmeticUtils.SubUnderflowU128(
                ArithmeticUtils.SubUnderflowU128(whirlpool.FeeGrowthGlobalA, feeGrowthBelowA),
                feeGrowthAboveA
            );
            feeGrowthInsideB = ArithmeticUtils.SubUnderflowU128(
                ArithmeticUtils.SubUnderflowU128(whirlpool.FeeGrowthGlobalB, feeGrowthBelowB),
                feeGrowthAboveB
            );
            
            // Calculate the updated fees owed
            BigInteger feeOwedDeltaA = ArithmeticUtils.SubUnderflowU128(
                feeGrowthInsideA, position.FeeGrowthCheckpointA
            ) * position.Liquidity >> 64;
            BigInteger feeOwedDeltaB = ArithmeticUtils.SubUnderflowU128(
                feeGrowthInsideB, position.FeeGrowthCheckpointB
            ) * position.Liquidity >> 64;

            this.FeeOwedA = position.FeeOwedA + (feeOwedDeltaA);
            this.FeeOwedB = position.FeeOwedB + (feeOwedDeltaB);
        }
    }
}