using NUnit.Framework;
using System.Threading.Tasks;
using System.Numerics;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.SolanaApi;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Test.Orca.Utils;

namespace Solana.Unity.Dex.Test.Orca.Integration
{
    [TestFixture]
    public class OpenPositionWithMetadataTests
    {
        private const int TickLowerIndex = 0; 
        private const int TickUpperIndex = 128; 
        
        private static TestWhirlpoolContext _context;
        private static PoolInitResult _initPoolResult;
        private static OpenPositionParams _defaultParams;
        private static Pda _whirlpoolPda; 
        private static Account _funderKeypair;
        private static Commitment _defaultCommitment;


        [SetUp]
        public static async Task Setup()
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
            _defaultCommitment = _context.WhirlpoolClient.DefaultCommitment;

            _initPoolResult = await PoolTestUtils.BuildPool(_context);
            Assert.IsTrue(_initPoolResult.WasSuccessful); 
            
            _whirlpoolPda = _initPoolResult.InitPoolParams.WhirlpoolPda;
            _defaultParams = PositionTestUtils.GenerateOpenParams(_context, _initPoolResult.InitPoolParams.WhirlpoolPda);
            _funderKeypair = new Account();
            
            await SolUtils.FundTestAccountAsync(_context, _funderKeypair, SolConstants.HALF_SOL);
        }
        
        public static async Task CheckMetadata(Pda metadataPda, PublicKey positionMintKey, Commitment commitment)
        {
            Assert.IsNotNull(metadataPda); 
            
            var metadataResult = await _context.RpcClient.GetAccountInfoAsync(metadataPda.PublicKey, commitment); 
            Assert.IsTrue(metadataResult.WasSuccessful);
            MetadataParser metadata = new(metadataResult.Result.Value.Data);

            Assert.That(metadata.UpdateAuthority, Is.EqualTo(AddressConstants.METADATA_UPDATE_AUTH_ID));
            Assert.That(metadata.Uri, Is.EqualTo("https://arweave.net/KZlsubXZyzeSYi2wJhyL7SY-DAot_OXhfWSYQGLmmOc"));
            Assert.That(metadata.Mint, Is.EqualTo(positionMintKey.ToString()));
        }

        [Test]  
        [Description("successfully opens position and verify position address contents")]
        public static async Task SuccessfullyOpenPosition()
        {
            //generate params to open position 
            OpenPositionParams openPositionParams =
                PositionTestUtils.GenerateOpenParams(
                    _context,
                    _whirlpoolPda,
                    TickLowerIndex,
                    TickUpperIndex,
                    withMetadata: true
                ); 
                
            //open position 
            var positionResult = await PositionTestUtils.OpenPositionWithMetadataAsync(
                _context, openPositionParams
            );
            
            Assert.IsTrue(positionResult.WasSuccessful); 
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(positionResult.Result, _defaultCommitment)); 
            
            //get position 
            Position position = (
                await _context.WhirlpoolClient.GetPositionAsync(
                    openPositionParams.PositionPda.PublicKey,
                    _defaultCommitment)
            ).ParsedResult;

            Assert.That(position.TickLowerIndex, Is.EqualTo(TickLowerIndex));
            Assert.That(position.TickUpperIndex, Is.EqualTo(TickUpperIndex));
            Assert.That(position.Whirlpool, Is.EqualTo(_initPoolResult.InitPoolParams.WhirlpoolPda.PublicKey));
            Assert.That(position.PositionMint, Is.EqualTo(openPositionParams.Accounts.PositionMint));
            Assert.That(position.Liquidity, Is.EqualTo(BigInteger.Zero));
            Assert.That(position.FeeGrowthCheckpointA, Is.EqualTo(BigInteger.Zero));
            Assert.That(position.FeeGrowthCheckpointB, Is.EqualTo(BigInteger.Zero));
            Assert.That(position.FeeOwedA, Is.EqualTo(0));
            Assert.That(position.FeeOwedB, Is.EqualTo(0));

            await CheckMetadata(openPositionParams.MetadataPda, position.PositionMint, _defaultCommitment);
        }

        [Test]  
        [Description("succeeds when funder is different than account paying for transaction fee")]
        public static async Task SuccessFunderDifferentFromPayer()
        {
            //generate params to open position 
            OpenPositionParams openPositionParams =
                PositionTestUtils.GenerateOpenParams(
                    _context,
                    _whirlpoolPda,
                    TickLowerIndex,
                    TickUpperIndex,
                    withMetadata: true,
                    funder: _funderKeypair
                );

            //open position 
            var positionResult = await PositionTestUtils.OpenPositionWithMetadataAsync(
                _context, openPositionParams
            );
            
            Assert.IsTrue(positionResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(positionResult.Result, _defaultCommitment));
            await CheckMetadata(openPositionParams.MetadataPda, openPositionParams.Accounts.PositionMint, _defaultCommitment);
        }

        [Test] 
        [Description("open position & verify position mint behavior")]
        public static async Task OpenPositionVerify()
        {
            Account newOwner = new();

            //generate params to open position 
            OpenPositionParams openPositionParams =
                PositionTestUtils.GenerateOpenParams(
                    _context,
                    _whirlpoolPda,
                    TickLowerIndex,
                    TickUpperIndex,
                    withMetadata: true, 
                    owner:  newOwner
                );
            PublicKey positionTokenMint = openPositionParams.Accounts.PositionMint;
            PublicKey positionTokenAccount = openPositionParams.Accounts.PositionTokenAccount;

            //open position 
            var positionResult = await PositionTestUtils.OpenPositionWithMetadataAsync(
                _context, openPositionParams
            );
            
            Assert.IsTrue(positionResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(positionResult.Result, _defaultCommitment));

            //verify that the metadata is what's expected
            await CheckMetadata(openPositionParams.MetadataPda, positionTokenMint, _defaultCommitment); 
            
            //get token account info 
            TokenAccountInfo userTokenAccount = (await _context.RpcClient.GetTokenAccountInfoAsync(
                openPositionParams.Accounts.PositionTokenAccount.ToString(),
                _defaultCommitment
            )).Result.Value;

            TokenAccountInfoDetails userTokenAccountData = userTokenAccount.Data.Parsed.Info; 

            Assert.IsNotNull(userTokenAccount);
            Assert.IsNotNull(userTokenAccount.Data);
            Assert.That(userTokenAccountData.TokenAmount.Amount, Is.EqualTo("1"));
            Assert.That(new PublicKey(userTokenAccountData.Owner), Is.EqualTo(newOwner.PublicKey));

            //throws /0x5/ the total supply of this token is fixed
            var mintResult = await TokenUtils.MintToByAuthorityAsync(
                _context,
                positionTokenMint, 
                positionTokenAccount,
                BigInteger.One,
                feePayer: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            
            AssertUtils.AssertFailedWithError(mintResult, 0x5);
        }

        [Test]  
        [Description("user must pass the valid token ATA account")]
        public static async Task MustPassValidTokenATA()
        {
            PublicKey anotherMintKey = await TokenUtils.CreateMintAsync(
                _context,
                authority: _context.WalletAccount, 
                commitment: Commitment.Confirmed
            ); 
            PublicKey positionTokenKey = TokenUtils.GetAssociatedTokenAddress(
                AddressConstants.ASSOCIATED_TOKEN_PROGRAM_PUBKEY,
                AddressConstants.TOKEN_PROGRAM_PUBKEY,
                anotherMintKey, 
                _context.WalletPubKey
            );

            //generate params to open position 
            OpenPositionParams openPositionParams =
                PositionTestUtils.GenerateOpenParams(
                    _context,
                    _whirlpoolPda,
                    TickLowerIndex,
                    TickUpperIndex,
                    withMetadata: true,
                    funder: _funderKeypair
                );
            openPositionParams.Accounts.PositionTokenAccount = positionTokenKey;

            //throws An account required by the instruction is missing/
            var positionResult = await PositionTestUtils.OpenPositionWithMetadataAsync(
                _context, openPositionParams
            );
            
            Assert.IsFalse(positionResult.WasSuccessful);
            Assert.IsTrue(positionResult.Reason.EndsWith("An account required by the instruction is missing")); 
        }

        [Test] 
        [Description("fail when position mint already exists")]
        public static async Task PositionMintAlreadyExists()
        {
            var initPoolResult = await PoolTestUtils.BuildPool(_context);
            Assert.IsTrue(_initPoolResult.WasSuccessful);

            var whirlpoolPda = initPoolResult.InitPoolParams.WhirlpoolPda;
            
            Account positionMintAccount = new Account();
            Pda positionPda = PdaUtils.GetPosition(_context.ProgramId, positionMintAccount.PublicKey);
            Pda metadataPda = PdaUtils.GetPositionMetadata(positionMintAccount); 

            PublicKey positionTokenAcctAddress = TokenUtils.GetAssociatedTokenAddress(
                positionMintAccount.PublicKey,
                _context.WalletPubKey
            );

            var defaultParams = PositionTestUtils.GenerateOpenParams(_context, initPoolResult.InitPoolParams.WhirlpoolPda);
            var mintAccount = defaultParams.Accounts.PositionMint;

            //create mint account 
            PublicKey mintAddress = await TokenUtils.CreateMintAsync( 
                _context,
                _context.WalletAccount,
                positionMintAccount,
                commitment: Commitment.Confirmed
            );
            
            //generate params to open position 
            OpenPositionParams openPositionParams =
                PositionTestUtils.GenerateOpenParams(
                    _context,
                    whirlpoolPda,
                    TickLowerIndex,
                    TickUpperIndex,
                    withMetadata: true
                );
            openPositionParams.MetadataPda = metadataPda;
            openPositionParams.PositionPda = positionPda; 
            openPositionParams.Accounts.Position = positionPda.PublicKey;
            openPositionParams.Accounts.PositionMint = positionMintAccount;
            openPositionParams.Accounts.PositionTokenAccount = positionTokenAcctAddress;

            //0x0: program failed to complete  
            var positionResult = await PositionTestUtils.OpenPositionWithMetadataAsync(
                _context, openPositionParams
            );
            
            Assert.IsFalse(positionResult.WasSuccessful); 
        }

        //all of these pass 
        [TestFixture]
        [Description("invalid ticks")]
        public static class InvalidTicks
        {
            private static async Task AssertTicksFail(int lower, int upper)
            {
                await AssertUtils.AssertTicksFail(
                    _context, 
                    whirlpoolKey: _whirlpoolPda.PublicKey,
                    lowerTick: lower, 
                    upperTick: upper,
                    withMetadata: true
                );
            }
            
            [Test] 
            [Description("fail when user pass in an out of bound tick index for upper-index")]
            public static async Task UpperOutOfBounds()
            {
                await AssertTicksFail(0, TickConstants.MAX_TICK_INDEX + 1);
            }
            
            [Test] 
            [Description("fail when user pass in a lower tick index that is higher than the upper-index")]
            public static async Task LowerHigherThanUpper()
            {
                await AssertTicksFail(-22534, -22534 - 1);
            }

            [Test] 
            [Description("fail when user pass in a lower tick index that equals the upper-index")]
            public static async Task LowerEqualtoUpper()
            {
                await AssertTicksFail(22365, 22365);
            }

            [Test] 
            [Description("fail when user pass in an out of bound tick index for lower-index")]
            public static async Task LowerOutofBounds()
            {
                await AssertTicksFail(TickConstants.MIN_TICK_INDEX - 1, 0);
            }

            [Test] 
            [Description("fail when user pass in a non-initializable tick index for upper-index")]
            public static async Task UpperNonInitializable()
            {
                await AssertTicksFail(0, 1);
            }

            [Test] 
            [Description("fail when user pass in a non-initializable tick index for lower-index")]
            public static async Task LowerNonInitializable()
            {
                await AssertTicksFail(1, 2);
            }
        }


        [TestFixture]
        [Description("invalid account constraints")]
        public static class InvalidAccountConstraints
        {
            [Test] 
            [Description("fails with non-mint metadataPda")]
            public static async Task FailsNonMintMetadata()
            {
                Account notMintKeypair = new Account();

                //generate params to open position 
                OpenPositionParams openPositionParams =
                    PositionTestUtils.GenerateOpenParams(
                        _context,
                        _whirlpoolPda,
                        TickLowerIndex,
                        TickUpperIndex,
                        withMetadata: true
                    );
                
                openPositionParams.MetadataPda = PdaUtils.GetPositionMetadata(notMintKeypair.PublicKey);

                //throws Invalid Metadata Key
                var positionResult = await PositionTestUtils.OpenPositionWithMetadataAsync(
                    _context, openPositionParams
                );

                AssertUtils.AssertFailedWithError(positionResult, 0x5);
            }
            
            [Test]  
            [Description("fails with non-program metadata program")]
            public static async Task FailsNonProgramMetadata()
            {
                //pass the wrong program id for metadata program 
                var positionResult = await PositionTestUtils.OpenPositionWithMetadataAsync(
                    _context,
                    openPositionParams: PositionTestUtils.GenerateOpenParams(
                        _context,
                        _whirlpoolPda,
                        TickLowerIndex,
                        TickUpperIndex,
                        withMetadata: true
                    ),
                    overrides: new PositionTestUtils.OpenWithMetadataOverrides
                    {
                        MetadataProgram = new Account()
                    }
                );

                AssertUtils.AssertFailedWithStandardError(positionResult, StandardErrorType.AddressConstraint);
            }

            [Test]  
            [Description("fails with non-metadata program")]
            public static async Task FailsNonMetadataProgram()
            {
                //pass the wrong program id for metadata program 
                var positionResult = await PositionTestUtils.OpenPositionWithMetadataAsync(
                    _context,
                    openPositionParams: PositionTestUtils.GenerateOpenParams(
                        _context,
                        _whirlpoolPda,
                        TickLowerIndex,
                        TickUpperIndex,
                        withMetadata: true
                    ),
                    overrides: new PositionTestUtils.OpenWithMetadataOverrides
                    {
                        MetadataProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    }
                );

                AssertUtils.AssertFailedWithStandardError(positionResult, StandardErrorType.AddressConstraint);
            }
            
            [Test] 
            [Description("fails with non-valid update_authority program")]
            public static async Task FailsInvalidUpdateAuthority()
            {
                //pass invalid address for metadata update authority 
                var positionResult = await PositionTestUtils.OpenPositionWithMetadataAsync(
                    _context,
                    openPositionParams: PositionTestUtils.GenerateOpenParams(
                        _context,
                        _whirlpoolPda,
                        TickLowerIndex,
                        TickUpperIndex,
                        withMetadata: true
                    ),
                    overrides: new PositionTestUtils.OpenWithMetadataOverrides
                    {
                        MetadataUpdateAuth = new Account()
                    }
                );

                AssertUtils.AssertFailedWithStandardError(positionResult, StandardErrorType.AddressConstraint);
            }
        }
    }
}