using System;
using System.Numerics;
using System.Collections.Generic;

using Solana.Unity.Programs.Utilities;
using Solana.Unity.Wallet;

using Solana.Unity.Dex.Orca.Core.Types;

namespace Solana.Unity.Dex.Orca.Core.Program
{
    public static class WhirlpoolProgram
    {
        public static Solana.Unity.Rpc.Models.TransactionInstruction InitializeConfig(
            InitializeConfigAccounts accounts, PublicKey feeAuthority, PublicKey collectProtocolFeesAuthority,
            PublicKey rewardEmissionsSuperAuthority, ushort defaultProtocolFeeRate, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Config, true),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Funder, true),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(5099410418541363152UL, offset);
            offset += 8;
            data.WritePubKey(feeAuthority, offset);
            offset += 32;
            data.WritePubKey(collectProtocolFeesAuthority, offset);
            offset += 32;
            data.WritePubKey(rewardEmissionsSuperAuthority, offset);
            offset += 32;
            data.WriteU16(defaultProtocolFeeRate, offset);
            offset += 2;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction InitializePool(InitializePoolAccounts accounts,
            WhirlpoolBumps bumps, ushort tickSpacing, BigInteger initialSqrtPrice, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.WhirlpoolsConfig, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenMintA, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenMintB, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Funder, true),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenVaultA, true),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenVaultB, true),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.FeeTier, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Rent, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(2947797634800858207UL, offset);
            offset += 8;
            offset += bumps.Serialize(data, offset);
            data.WriteU16(tickSpacing, offset);
            offset += 2;
            data.WriteBigInt(initialSqrtPrice, offset, 16, true);
            offset += 16;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction InitializeTickArray(
            InitializeTickArrayAccounts accounts, int startTickIndex, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Funder, true),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TickArray, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(13300637739260165131UL, offset);
            offset += 8;
            data.WriteS32(startTickIndex, offset);
            offset += 4;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction InitializeFeeTier(
            InitializeFeeTierAccounts accounts, ushort tickSpacing, ushort defaultFeeRate, PublicKey programId)
        {
            List<Solana.Unity.Rpc.Models.AccountMeta> keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Config, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.FeeTier, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Funder, true),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.FeeAuthority, true),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(2173552452913875639UL, offset);
            offset += 8;
            data.WriteU16(tickSpacing, offset);
            offset += 2;
            data.WriteU16(defaultFeeRate, offset);
            offset += 2;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction InitializeReward(
            InitializeRewardAccounts accounts, byte rewardIndex, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.RewardAuthority, true),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Funder, true),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.RewardMint, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.RewardVault, true),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Rent, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(4964798518905571167UL, offset);
            offset += 8;
            data.WriteU8(rewardIndex, offset);
            offset += 1;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction SetRewardEmissions(
            SetRewardEmissionsAccounts accounts, byte rewardIndex, BigInteger emissionsPerSecondX64,
            PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.RewardAuthority, true),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.RewardVault, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(17589846754647786765UL, offset);
            offset += 8;
            data.WriteU8(rewardIndex, offset);
            offset += 1;
            data.WriteBigInt(emissionsPerSecondX64, offset, 16, true);
            offset += 16;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction OpenPosition(OpenPositionAccounts accounts,
            OpenPositionBumps bumps, int tickLowerIndex, int tickUpperIndex, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Funder, true),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Owner, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Position, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PositionMint, true),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PositionTokenAccount, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Rent, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.AssociatedTokenProgram, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(3598543293755916423UL, offset);
            offset += 8;
            offset += bumps.Serialize(data, offset);
            data.WriteS32(tickLowerIndex, offset);
            offset += 4;
            data.WriteS32(tickUpperIndex, offset);
            offset += 4;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction OpenPositionWithMetadata(
            OpenPositionWithMetadataAccounts accounts, OpenPositionWithMetadataBumps bumps, int tickLowerIndex,
            int tickUpperIndex, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Funder, true),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Owner, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Position, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PositionMint, true),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PositionMetadataAccount, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PositionTokenAccount, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Rent, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.AssociatedTokenProgram, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.MetadataProgram, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.MetadataUpdateAuth, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(4327517488150879730UL, offset);
            offset += 8;
            offset += bumps.Serialize(data, offset);
            data.WriteS32(tickLowerIndex, offset);
            offset += 4;
            data.WriteS32(tickUpperIndex, offset);
            offset += 4;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction IncreaseLiquidity(
            IncreaseLiquidityAccounts accounts, BigInteger liquidityAmount, ulong tokenMaxA, ulong tokenMaxB,
            PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.PositionAuthority, true),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Position, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.PositionTokenAccount, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenOwnerAccountA, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenOwnerAccountB, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenVaultA, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenVaultB, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TickArrayLower, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TickArrayUpper, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(12897127415619492910UL, offset);
            offset += 8;
            data.WriteBigInt(liquidityAmount, offset, 16, true);
            offset += 16;
            data.WriteU64(tokenMaxA, offset);
            offset += 8;
            data.WriteU64(tokenMaxB, offset);
            offset += 8;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction DecreaseLiquidity(
            DecreaseLiquidityAccounts accounts, BigInteger liquidityAmount, ulong tokenMinA, ulong tokenMinB,
            PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.PositionAuthority, true),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Position, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.PositionTokenAccount, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenOwnerAccountA, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenOwnerAccountB, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenVaultA, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenVaultB, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TickArrayLower, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TickArrayUpper, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(84542997123835552UL, offset);
            offset += 8;
            data.WriteBigInt(liquidityAmount, offset, 16, true);
            offset += 16;
            data.WriteU64(tokenMinA, offset);
            offset += 8;
            data.WriteU64(tokenMinB, offset);
            offset += 8;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction UpdateFeesAndRewards(
            UpdateFeesAndRewardsAccounts accounts, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Position, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TickArrayLower, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TickArrayUpper, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(16090184905488262810UL, offset);
            offset += 8;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction CollectFees(CollectFeesAccounts accounts,
            PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.PositionAuthority, true),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Position, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.PositionTokenAccount, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenOwnerAccountA, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenVaultA, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenOwnerAccountB, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenVaultB, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(13120034779146721444UL, offset);
            offset += 8;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction CollectReward(CollectRewardAccounts accounts,
            byte rewardIndex, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.PositionAuthority, true),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Position, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.PositionTokenAccount, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.RewardOwnerAccount, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.RewardVault, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(2500038024235320646UL, offset);
            offset += 8;
            data.WriteU8(rewardIndex, offset);
            offset += 1;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction CollectProtocolFees(
            CollectProtocolFeesAccounts accounts, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.WhirlpoolsConfig, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.CollectProtocolFeesAuthority, true),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenVaultA, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenVaultB, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenDestinationA, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenDestinationB, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(15872570295674422038UL, offset);
            offset += 8;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction Swap(SwapAccounts accounts, ulong amount,
            ulong otherAmountThreshold, BigInteger sqrtPriceLimit, bool amountSpecifiedIsInput, bool aToB,
            PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenAuthority, true),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenOwnerAccountA, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenVaultA, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenOwnerAccountB, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TokenVaultB, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TickArray0, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TickArray1, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.TickArray2, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Oracle, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(14449647541112719096UL, offset);
            offset += 8;
            data.WriteU64(amount, offset);
            offset += 8;
            data.WriteU64(otherAmountThreshold, offset);
            offset += 8;
            data.WriteBigInt(sqrtPriceLimit, offset, 16, true);
            offset += 16;
            data.WriteBool(amountSpecifiedIsInput, offset);
            offset += 1;
            data.WriteBool(aToB, offset);
            offset += 1;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction ClosePosition(ClosePositionAccounts accounts,
            PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.PositionAuthority, true),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Receiver, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Position, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PositionMint, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PositionTokenAccount, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.TokenProgram, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(7089303740684011131UL, offset);
            offset += 8;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction SetDefaultFeeRate(
            SetDefaultFeeRateAccounts accounts, ushort defaultFeeRate, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.WhirlpoolsConfig, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.FeeTier, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.FeeAuthority, true)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(16487930808298297206UL, offset);
            offset += 8;
            data.WriteU16(defaultFeeRate, offset);
            offset += 2;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction SetDefaultProtocolFeeRate(
            SetDefaultProtocolFeeRateAccounts accounts, ushort defaultProtocolFeeRate, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.WhirlpoolsConfig, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.FeeAuthority, true)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(24245983252172139UL, offset);
            offset += 8;
            data.WriteU16(defaultProtocolFeeRate, offset);
            offset += 2;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction SetFeeRate(SetFeeRateAccounts accounts,
            ushort feeRate, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.WhirlpoolsConfig, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.FeeAuthority, true)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(476972577635038005UL, offset);
            offset += 8;
            data.WriteU16(feeRate, offset);
            offset += 2;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction SetProtocolFeeRate(
            SetProtocolFeeRateAccounts accounts, ushort protocolFeeRate, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.WhirlpoolsConfig, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.FeeAuthority, true)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(9483542439018104671UL, offset);
            offset += 8;
            data.WriteU16(protocolFeeRate, offset);
            offset += 2;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction SetFeeAuthority(
            SetFeeAuthorityAccounts accounts, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.WhirlpoolsConfig, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.FeeAuthority, true),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.NewFeeAuthority, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(9539017555791970591UL, offset);
            offset += 8;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction SetCollectProtocolFeesAuthority(
            SetCollectProtocolFeesAuthorityAccounts accounts, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.WhirlpoolsConfig, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.CollectProtocolFeesAuthority, true),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.NewCollectProtocolFeesAuthority, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(4893690461331232290UL, offset);
            offset += 8;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction SetRewardAuthority(
            SetRewardAuthorityAccounts accounts, byte rewardIndex, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.RewardAuthority, true),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.NewRewardAuthority, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(9175270962884978466UL, offset);
            offset += 8;
            data.WriteU8(rewardIndex, offset);
            offset += 1;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction SetRewardAuthorityBySuperAuthority(
            SetRewardAuthorityBySuperAuthorityAccounts accounts, byte rewardIndex, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.WhirlpoolsConfig, false),
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Whirlpool, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.RewardEmissionsSuperAuthority, true),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.NewRewardAuthority, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(1817305343215639280UL, offset);
            offset += 8;
            data.WriteU8(rewardIndex, offset);
            offset += 1;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }

        public static Solana.Unity.Rpc.Models.TransactionInstruction SetRewardEmissionsSuperAuthority(
            SetRewardEmissionsSuperAuthorityAccounts accounts, PublicKey programId)
        {
            var keys = new List<Solana.Unity.Rpc.Models.AccountMeta>
            {
                Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.WhirlpoolsConfig, false),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.RewardEmissionsSuperAuthority, true),
                Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.NewRewardEmissionsSuperAuthority, false)
            };
            byte[] data = new byte[1200];
            int offset = 0;
            data.WriteU64(13209682757187798479UL, offset);
            offset += 8;
            byte[] resultData = new byte[offset];
            Array.Copy(data, resultData, offset);
            return new Solana.Unity.Rpc.Models.TransactionInstruction
            { Keys = keys, ProgramId = programId.KeyBytes, Data = resultData };
        }
    }
}