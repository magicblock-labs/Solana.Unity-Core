using System;
using System.Numerics;

using Solana.Unity.Wallet;

using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Ticks;

namespace Solana.Unity.Dex.Orca.Address
{
    /// <summary>
    /// Utilities for generating program-derived addresses for various uses. 
    /// </summary>
    public static class PdaUtils
    {
        private const string PdaWhirlpoolSeed = "whirlpool";
        private const string PdaPositionSeed = "position";
        private const string PdaMetadataSeed = "metadata";
        private const string PdaTickArraySeed = "tick_array";
        private const string PdaFeeTierSeed = "fee_tier";
        private const string PdaOracleSeed = "oracle";

        /// <summary>
        /// Generates a PDA for a whirlpool. 
        /// </summary>
        /// <param name="programId">Address of the whirlpool program.</param>
        /// <param name="whirlpoolsConfigKey">The public key of the config account for the specific whirlpool</param>
        /// <param name="tokenMintAKey">The address of the pool's token A mint.</param>
        /// <param name="tokenMintBKey">The address of the pool's token B mint.</param>
        /// <param name="tickSpacing">The tickspacing associated with the pool.</param>
        /// <returns>A PDA and the bump byte used to generate it.</returns>
        public static Pda GetWhirlpool(
            PublicKey programId,
            PublicKey whirlpoolsConfigKey,
            PublicKey tokenMintAKey,
            PublicKey tokenMintBKey,
            ushort tickSpacing
        )
        {
            return AddressUtils.FindProgramAddress(
                new byte[][]
                {
                    StringToBytes(PdaWhirlpoolSeed),
                    whirlpoolsConfigKey.KeyBytes,
                    tokenMintAKey.KeyBytes,
                    tokenMintBKey.KeyBytes,
                    ArithmeticUtils.BigIntToArray(new BigInteger(tickSpacing), Endianness.LittleEndian, 2)
                },
                programId
            );
        }

        /// <summary>
        /// Generates a PDA for a liquidity position for a whirlpool. 
        /// </summary>
        /// <param name="programId">Address of the whirlpool program.</param>
        /// <param name="positionMintKey">Address key of the position token mint.</param>
        /// <returns>A PDA and the bump byte used to generate it.</returns>
        public static Pda GetPosition(PublicKey programId, PublicKey positionMintKey)
        {
            return AddressUtils.FindProgramAddress(
                new byte[][]
                {
                    StringToBytes(PdaPositionSeed),
                    positionMintKey.KeyBytes
                },
                programId
            );
        }

        /// <summary>
        /// Generates a PDA for a position's metadata.
        /// </summary>
        /// <param name="positionMintKey">Address key of the position token mint.</param>
        /// <returns>A PDA and the bump byte used to generate it.</returns>
        public static Pda GetPositionMetadata(PublicKey positionMintKey)
        {
            return AddressUtils.FindProgramAddress(
                new byte[][]
                {
                    StringToBytes(PdaMetadataSeed),
                    AddressConstants.METADATA_PROGRAM_PUBKEY.KeyBytes,
                    positionMintKey.KeyBytes
                },
                AddressConstants.METADATA_PROGRAM_PUBKEY
            );
        }

        /// <summary>
        /// Generates a PDA for a tick array associated with a specific whirlpool.
        /// </summary>
        /// <param name="programId">Address of the whirlpool program.</param>
        /// <param name="whirlpoolsConfigKey">The public key of the config account for the specific whirlpool</param>
        /// <param name="startTick">The adjusted start tick of the tick array. </param>
        /// <returns>A PDA and the bump byte used to generate it.</returns>
        public static Pda GetTickArray(PublicKey programId, PublicKey whirlpoolAddress, int startTick)
        {
            return AddressUtils.FindProgramAddress(
                new byte[][]
                {
                    StringToBytes(PdaTickArraySeed),
                    whirlpoolAddress.KeyBytes,
                    StringToBytes(startTick.ToString())
                },
                programId
            );
        }

        /// <summary>
        /// Generates a PDA for a tick array associated with a specific whirlpool, given a tick index 
        /// and offset.
        /// </summary>
        /// <param name="tickIndex"></param>
        /// <param name="tickSpacing"></param>
        /// <param name="whirlpool"></param>
        /// <param name="programId"></param>
        /// <param name="tickArrayOffset"></param>
        /// <returns>A PDA and the bump byte used to generate it.</returns>
        public static Pda GetTickArrayFromTickIndex(
            int tickIndex,
            ushort tickSpacing,
            PublicKey whirlpool,
            PublicKey programId,
            int tickArrayOffset = 0
        )
        {
            int startIndex = TickUtils.GetStartTickIndex(tickIndex, tickSpacing, tickArrayOffset);
            return PdaUtils.GetTickArray(
                programId,
                whirlpool,
                startIndex
            );
        }

        /// <summary>
        /// Generates a PDA for a tick array associated with a specific whirlpool, given the square root 
        /// price limit and tick array offset.
        /// </summary>
        /// <param name="sqrtPriceX64"></param>
        /// <param name="tickSpacing"></param>
        /// <param name="whirlpool"></param>
        /// <param name="programId"></param>
        /// <param name="tickArrayOffset"></param>
        /// <returns>A PDA and the bump byte used to generate it.</returns>
        public static Pda GetTickArrayFromSqrtPrice(
            BigInteger sqrtPriceX64,
            ushort tickSpacing,
            PublicKey whirlpool,
            PublicKey programId,
            int tickArrayOffset = 0
        )
        {
            int tickIndex = PriceMath.SqrtPriceX64ToTickIndex(sqrtPriceX64);
            return PdaUtils.GetTickArrayFromTickIndex(
                tickIndex,
                tickSpacing,
                whirlpool,
                programId,
                tickArrayOffset
            );
        }

        /// <summary>
        /// Generates a PDA for a specific whirlpool's fee tier information.
        /// </summary>
        /// <param name="programId">Address of the whirlpool program.</param>
        /// <param name="whirlpoolsConfigAddress"></param>
        /// <param name="tickSpacing">The tickspacing associated with the whirlpool.</param>
        /// <returns>A PDA and the bump byte used to generate it.</returns>
        public static Pda GetFeeTier(PublicKey programId, PublicKey whirlpoolsConfigAddress, ushort tickSpacing)
        {
            return AddressUtils.FindProgramAddress(
                new byte[][]
                {
                    StringToBytes(PdaFeeTierSeed),
                    whirlpoolsConfigAddress.KeyBytes,
                    ArithmeticUtils.BigIntToArray(new BigInteger(tickSpacing), Endianness.LittleEndian, 2)
                },
                programId
            );
        }

        /// <summary>
        /// Generates a PDA for an oracle associated with a specific whirlpool. 
        /// </summary>
        /// <param name="programId">Address of the whirlpool program.</param>
        /// <param name="whirlpoolAddress">Address of the specific liquidity pool.</param>
        /// <returns>A PDA and the bump byte used to generate it.</returns>
        public static Pda GetOracle(PublicKey programId, PublicKey whirlpoolAddress)
        {
            return AddressUtils.FindProgramAddress(
                new byte[][]
                {
                    StringToBytes(PdaOracleSeed),
                    whirlpoolAddress.KeyBytes
                },
                programId
            );
        }

        private static byte[] StringToBytes(string s) => System.Text.Encoding.ASCII.GetBytes(s);
    }
}
