using System;
using System.Numerics;

using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Swap;
using Solana.Unity.Dex.Orca.Core.Types;
using Solana.Unity.Dex.Orca.Core.Accounts;

namespace Solana.Unity.Dex.Orca.Quotes
{
    public class CollectRewardsQuote
    {
        public BigInteger? RewardOwedA { get; set; }
        public BigInteger? RewardOwedB { get; set; }
        public BigInteger? RewardOwedC { get; set; }
        
        public CollectRewardsQuote(
            Whirlpool whirlpool,
            Position position,
            Tick tickLower,
            Tick tickUpper)
        {
            const int numRewards = 3; 
            
            BigInteger[] rewardGrowthsBelowX64 = new BigInteger[numRewards];
            BigInteger[] rewardGrowthsAboveX64 = new BigInteger[numRewards];
            for (int i=0; i<numRewards; i++) 
            {
                WhirlpoolRewardInfo rewardInfo = whirlpool.RewardInfos[i];
                if (rewardInfo == null)
                    throw new System.NullReferenceException("Whirlpool RewardInfo cannot be null");

                BigInteger growthGlobalX64 = rewardInfo.GrowthGlobalX64;

                BigInteger lowerRewardGrowthsOutside = tickLower.RewardGrowthsOutside[i];
                BigInteger upperRewardGrowthsOutside = tickUpper.RewardGrowthsOutside[i];

                rewardGrowthsBelowX64[i] = (whirlpool.TickCurrentIndex < position.TickLowerIndex) ?
                    ArithmeticUtils.SubUnderflowU128(
                        growthGlobalX64,
                        lowerRewardGrowthsOutside
                    ) :
                    lowerRewardGrowthsOutside;

                rewardGrowthsAboveX64[i] = (whirlpool.TickCurrentIndex < position.TickUpperIndex) ?
                    upperRewardGrowthsOutside : 
                    ArithmeticUtils.SubUnderflowU128(
                        growthGlobalX64,
                        upperRewardGrowthsOutside
                    );
            }

            Tuple<BigInteger, bool>[] rewardGrowthsInsideX64 = new Tuple<BigInteger, bool>[numRewards];

            for (int i = 0; i < numRewards; i++)
            {
                WhirlpoolRewardInfo rewardInfo = whirlpool.RewardInfos[i];
                if (rewardInfo == null)
                    throw new System.NullReferenceException("Whirlpool RewardInfo cannot be null");
                    
                bool isRewardInitialized = PoolUtils.IsRewardInitialized(rewardInfo);
                
                if (isRewardInitialized)
                {
                    BigInteger growthBelowX64 = rewardGrowthsBelowX64[i];
                    BigInteger growthAboveX64 = rewardGrowthsAboveX64[i];

                    BigInteger growthInsde = ArithmeticUtils.SubUnderflowU128(
                        ArithmeticUtils.SubUnderflowU128(rewardInfo.GrowthGlobalX64, growthBelowX64),
                        growthAboveX64
                    );
                    rewardGrowthsInsideX64[i] = Tuple.Create<BigInteger, bool>(growthInsde, true);
                }
            }
            
            BigInteger[] updatedRewardInfosX64 = new BigInteger[numRewards];

            // Calculate the updated rewards owed
            for (int i = 0; i < numRewards; i++)
            {
                var (rewardGrowthInsideX64, isRewardInitialized) = rewardGrowthsInsideX64[i];

                if (isRewardInitialized)
                {
                    PositionRewardInfo rewardInfo = position.RewardInfos[i];
                    if (rewardInfo == null)
                        throw new System.NullReferenceException("Whirlpool RewardInfo cannot be null");

                    BigInteger amountOwedX64 = rewardInfo.AmountOwed << 64;
                    BigInteger growthInsideCheckpointX64 = rewardInfo.GrowthInsideCheckpoint;
                    
                    updatedRewardInfosX64[i] = amountOwedX64 + (
                        ArithmeticUtils.SubUnderflowU128(rewardGrowthInsideX64, growthInsideCheckpointX64) * position.Liquidity
                    );
                }
            }
            
            if (rewardGrowthsInsideX64.Length < 3) 
                throw new ArgumentOutOfRangeException("rewards length cannot be less than 3");


            bool rewardExistsA = rewardGrowthsInsideX64[0].Item2;
            bool rewardExistsB = rewardGrowthsInsideX64[1].Item2;
            bool rewardExistsC = rewardGrowthsInsideX64[2].Item2;

            this.RewardOwedA = rewardExistsA ? updatedRewardInfosX64[0] >> 64 : null;
            this.RewardOwedB = rewardExistsB ? updatedRewardInfosX64[1] >> 64 : null;
            this.RewardOwedC = rewardExistsC ? updatedRewardInfosX64[2] >> 64 : null;
        }
    }
}