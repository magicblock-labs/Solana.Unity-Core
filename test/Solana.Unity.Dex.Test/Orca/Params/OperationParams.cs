using System.Numerics;

using Solana.Unity.Wallet;

using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.Core;
using Solana.Unity.Dex.Orca.Core.Types;
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Dex.Orca.Address;

//TODO: (HIGH) EVERY ACCOUNTS PARAM THAT HAS A FUNDER PROPERTY, SHOULD HAVE HERE A FUNDERKEYPAIR PROPERTY 
    // (and the funder keypair should be a signer, in case it differs from feePayer)
namespace Solana.Unity.Dex.Test.Orca.Params
{
    public class WhirlpoolsConfigKeyPairs
    {
        public Account FeeAuthorityKeyPair { get; set; }
        public Account CollectProtocolFeesAuthorityKeyPair { get; set; }
        public Account RewardEmissionsSuperAuthorityKeyPair { get; set; }
    }

    public class InitializeConfigParams 
    {
        public InitializeConfigAccounts Accounts { get; set; }
        public Account FeeAuthority { get; set; }
        public PublicKey CollectProtocolFeesAuthority { get; set; }
        public PublicKey RewardEmissionsSuperAuthority { get; set; }
        public ushort DefaultProtocolFeeRate { get; set; }
        public WhirlpoolsConfigKeyPairs ConfigKeyPairs { get; set; }
        public Account FeeAuthorityKeypair { get; set; }
        public Account ConfigKeypair { get; set; }
    }

    public class InitializeFeeTierParams  
    {
        public const int DefaultDefaultFeeRate = 3000;

        public InitializeFeeTierAccounts Accounts { get; set; }
        public Pda FeeTierPda { get; set; }  
        public ushort TickSpacing { get; set; }
        public ushort DefaultFeeRate { get; set; }
        public Account FeeAuthorityKeypair { get; set; }
    }

    /// <summary>
    /// Parameters to initialize a Whirlpool account.
    /// </summary>
    public class InitializePoolParams
    {
        public InitializePoolAccounts Accounts { get; set; }
        /// <summary>
        /// Gets/sets the desired initial sqrt-price for this pool.
        /// </summary>
        public BigInteger InitSqrtPrice { get; set; }
        /// <summary>
        /// Gets/sets the PDA for the whirlpool account that would be initialized.
        /// </summary>
        public Pda WhirlpoolPda { get; set; } 
        /// <summary>
        /// Gets/sets keypair of the token A vault for this pool.
        /// </summary>
        public Account TokenVaultAKeyPair { get; set; } 
        /// <summary>
        /// Gets/sets keypair of the token B vault for this pool.
        /// </summary>
        public Account TokenVaultBKeyPair { get; set; } 
        /// <summary>
        /// Gets/sets the desired tick spacing for this pool.
        /// </summary>
        public ushort TickSpacing { get; set; }
        
        public WhirlpoolBumps Bumps {get; set;}
    }
    
    public class OpenPositionParams 
    {
        public OpenPositionAccounts Accounts { get; set; }
        public OpenPositionBumps Bumps { get; set; }
        public int TickUpperIndex { get; set; }
        public int TickLowerIndex { get; set; }
        
        public Pda PositionPda { get; set; }
        public Pda MetadataPda { get; set; }
        public bool WithMetadata { get; set; }
        public Account PositionMintKeypair { get; set; }
        public Account FunderKeypair { get; set; }
    }
    
    public class ClosePositionParams 
    {
        public ClosePositionAccounts Accounts { get; set; }
    }
    
    public class FundedPositionParams
    {
        public int TickLowerIndex { get; set; }
        public int TickUpperIndex { get; set; }
        public BigInteger LiquidityAmount { get; set; }
    }

    public class InitializeRewardParams
    {
        public InitializeRewardAccounts Accounts { get; set; }
        public byte RewardIndex { get; set; }
    }
    
    public class RewardParams
    {
        public BigInteger EmissionsPerSecondX64 { get; set; }
        public BigInteger VaultAmount { get; set; }
    }
    
    public class CollectRewardsParams
    {
        public CollectRewardAccounts Accounts { get; set;}
        public byte RewardIndex { get; set; }
    }

    public class CollectFeesParams
    {
        public CollectFeesAccounts Accounts { get; set; }
        public byte RewardIndex { get; set; }
        public Account PositionAuthorityKeypair { get; set; }
    }
    
    public class SwapParams
    {
        public SwapAccounts Accounts { get; set; }
        public BigInteger Amount { get; set; }
        public BigInteger OtherThresholdAmount { get; set; }
        public BigInteger SqrtPriceLimit { get; set; }
        public bool AmountSpecifiedIsInput { get; set; }
        public bool AtoB { get; set; }        
    }
    
    public class SwapTestPoolParams
    {
        public IWhirlpoolContext Context { get; set; }
        public WhirlpoolClient Client { get; set; }
        public ushort TickSpacing { get; set; }
        public BigInteger InitSqrtPrice { get; set; }
        public long[] InitArrayStartTicks { get; set; }
        public FundedPositionParams[] FundedPositions { get; set; }
        public BigInteger? TokenMintAmount { get; set; } 
    }

    public class ArrayTickIndex
    {
        public int ArrayIndex { get; set; }
        public int OffsetIndex { get; set; }
    }
    
    public class InitializeTickArrayParams
    {
        public InitializeTickArrayAccounts Accounts { get; set; }
        public int StartTick { get; set; }
        public Pda TickArrayPda { get; set; }
    }

    public class IncreaseLiquidityParams
    {
        public IncreaseLiquidityAccounts Accounts { get; set; }
        public BigInteger LiquidityAmount { get; set; }
        public ulong TokenMaxA { get; set; }
        public ulong TokenMaxB { get; set; }
        public Account PositionAuthorityKeypair { get; set; }
    }

    public class DecreaseLiquidityParams
    {
        public DecreaseLiquidityAccounts Accounts { get; set; }
        public BigInteger LiquidityAmount { get; set; }
        public ulong TokenMinA { get; set; }
        public ulong TokenMinB { get; set; }
        public Account PositionAuthorityKeypair { get; set; }
    }
}