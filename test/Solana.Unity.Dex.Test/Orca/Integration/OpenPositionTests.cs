using NUnit.Framework;
using System.Numerics;
using System.Threading.Tasks;
using Solana.Unity.Programs.Models;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Test.Orca.Utils;

namespace Solana.Unity.Dex.Test.Orca.Integration
{

    [TestFixture]
    public class OpenPositionTests
    {
        private static TestWhirlpoolContext _context;
        private static Commitment _defaultCommitment;

        [SetUp]
        public static void Setup()
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
            _defaultCommitment = _context.WhirlpoolClient.DefaultCommitment;
            //_funderKeyPair = AddressUtils.Generate();
        }

        [Test] 
        [Description("successfully opens position and verify position address contents")]
        public static async Task SuccessfulOpenPosition()
        {
            PoolInitResult initPoolResult = await PoolTestUtils.BuildPool(_context);
            Assert.IsTrue(initPoolResult.WasSuccessful);


            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context, initPoolResult.InitPoolParams.WhirlpoolPda);            
            
            //open the position 
            var openPositionResult = await PositionTestUtils.OpenPositionAsync(
                _context, 
                openPositionParams
            );
            Assert.IsTrue(openPositionResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openPositionResult.Result, _defaultCommitment));

            //retrieve the position
            AccountResultWrapper<Position> positionResult =
                await _context.WhirlpoolClient.GetPositionAsync(openPositionParams.PositionPda.PublicKey);
            
            //asserts 
            Assert.IsTrue(positionResult.WasSuccessful);
            Position position = positionResult.ParsedResult;
            
            Assert.That(position.TickLowerIndex, Is.EqualTo(openPositionParams.TickLowerIndex));
            Assert.That(position.TickUpperIndex, Is.EqualTo(openPositionParams.TickUpperIndex));
            Assert.That(position.Whirlpool, Is.EqualTo(initPoolResult.InitPoolParams.WhirlpoolPda.PublicKey));
            Assert.That(position.PositionMint, Is.EqualTo(openPositionParams.Accounts.PositionMint));
            Assert.That(position.Liquidity, Is.EqualTo(BigInteger.Zero));
            Assert.That(position.FeeGrowthCheckpointA, Is.EqualTo(BigInteger.Zero));
            Assert.That(position.FeeGrowthCheckpointB, Is.EqualTo(BigInteger.Zero));
            Assert.That(position.FeeOwedA, Is.EqualTo(0));
            Assert.That(position.FeeOwedB, Is.EqualTo(0));
        }

        [Test] 
        [Description("succeeds when funder is different than account paying for transaction fee")]
        public static async Task SucceedsWhenFunderDifferentFromPayer()
        {
            PoolInitResult initPoolResult = await PoolTestUtils.BuildPool(_context);
            Assert.IsTrue(initPoolResult.WasSuccessful);
            
            //generate and fund funder account 
            Account funderKeyPair = new();
            await SolUtils.FundTestAccountAsync(_context, funderKeyPair); 
                
            //generate params 
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context,
                initPoolResult.InitPoolParams.WhirlpoolPda,
                funder: funderKeyPair
            );

            //open the position 
            var openPositionResult = await PositionTestUtils.OpenPositionAsync(
                _context,
                openPositionParams, 
                feePayer: _context.WalletAccount
            );
            
            Assert.IsTrue(openPositionResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openPositionResult.Result, _defaultCommitment));
        }
        
        [Test] 
        [Description("open position & verify position mint behavior")]
        public static async Task OpenPositionVerifyMint()
        {
            PoolInitResult initPoolResult = await PoolTestUtils.BuildPool(_context);
            Assert.IsTrue(initPoolResult.WasSuccessful);
                
            Account newOwner = new();
            
            //generate params 
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context,
                initPoolResult.InitPoolParams.WhirlpoolPda,
                owner: newOwner.PublicKey
            );

            //open the position 
            var openPositionResult = await PositionTestUtils.OpenPositionAsync(
                _context,
                openPositionParams
            );
            
            Assert.IsTrue(openPositionResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openPositionResult.Result, _defaultCommitment));
            
            //get token account info 
            PublicKey positionTokenAccount = openPositionParams.Accounts.PositionTokenAccount;
            PublicKey positionMintAddress = openPositionParams.Accounts.PositionMint;

            TokenAccountInfo userTokenAccount = (await _context.RpcClient.GetTokenAccountInfoAsync(
                positionTokenAccount.ToString(),
                _defaultCommitment
            )).Result.Value;

            Assert.IsNotNull(userTokenAccount);
            Assert.IsNotNull(userTokenAccount.Data);
            Assert.That(userTokenAccount.Data.Parsed.Info.TokenAmount.Amount, Is.EqualTo("1"));
            Assert.That(new PublicKey(userTokenAccount.Data.Parsed.Info.Owner), Is.EqualTo(newOwner.PublicKey));
            
            // the total supply of this token is fixed /0x5/
            var mintResult = await TokenUtils.MintToByAuthorityAsync(
                _context, positionMintAddress, positionTokenAccount, 1,
                feePayer: _context.WalletAccount,
                commitment: _defaultCommitment
            );
            
            AssertUtils.AssertFailedWithError(mintResult, 0x5);
        }
        
        [Test] 
        [Description("user must pass the valid token ATA account")]
        public static async Task UserMustPassValidTokenAta()
        {
            PoolInitResult initPoolResult = await PoolTestUtils.BuildPool(_context);
            Assert.IsTrue(initPoolResult.WasSuccessful);
            
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context, initPoolResult.InitPoolParams.WhirlpoolPda);
            
            PublicKey anotherMintKey = await TokenUtils.CreateMintAsync(
                _context,
                authority: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            PublicKey positionTokenAccount = TokenUtils.GetAssociatedTokenAddress(
                anotherMintKey, 
                _context.WalletPubKey
            );
            
            //get default params with the PositionTokenAccount 
            var defaultParams = initPoolResult.InitPoolParams; 
            
            openPositionParams.Accounts.PositionTokenAccount = positionTokenAccount;

            var result = await PositionTestUtils.OpenPositionAsync(
                _context,
                openPositionParams
            );
            
            Assert.IsFalse(result.WasSuccessful);
            Assert.IsTrue(result.Reason.EndsWith("An account required by the instruction is missing"));
        }

        [Test] 
        [Description("fail when position mint already exists")]
        public static async Task PositionMintAlreadyExists()
        {
            PoolInitResult initPoolResult = await PoolTestUtils.BuildPool(_context);
            Assert.IsTrue(initPoolResult.WasSuccessful);
                
            Account positionMintAccount = new Account();
            Pda positionPda = PdaUtils.GetPosition(_context.ProgramId, positionMintAccount.PublicKey);

            PublicKey positionTokenAcctAddress = TokenUtils.GetAssociatedTokenAddress(
                positionMintAccount.PublicKey,
                _context.WalletPubKey
            ); 
            
            PublicKey positionMint = await TokenUtils.CreateMintAsync( 
                _context, 
                _context.WalletAccount, 
                positionMintAccount,
                commitment: Commitment.Confirmed
            );
            
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context, 
                whirlpoolAddr: initPoolResult.InitPoolParams.WhirlpoolPda, 
                tickLowerIndex: 0, 
                tickUpperIndex: 128
            );
            openPositionParams.PositionPda = positionPda;
            openPositionParams.Bumps.PositionBump = positionPda.Bump;
            openPositionParams.Accounts.Position = positionPda.PublicKey;
            openPositionParams.Accounts.PositionMint = positionMintAccount;
            openPositionParams.Accounts.PositionTokenAccount = positionTokenAcctAddress;
            openPositionParams.PositionMintKeypair = positionMintAccount;
            
            //should fail with 0x0
            var result = await PositionTestUtils.OpenPositionAsync(
                _context, openPositionParams
            );

            AssertUtils.AssertFailedWithError(result, 0);
        }
        
        [TestFixture]
        [Description("invalid ticks")]
        public class InvalidTicks
        {
            private static PoolInitResult _initPoolResult;

            [SetUp]
            public static async Task Setup()
            {
                _initPoolResult = await PoolTestUtils.BuildPool(_context);
                Assert.IsTrue(_initPoolResult.WasSuccessful);
            }
            
            private static async Task AssertTicksFail(int lower, int upper)
            {
                await AssertUtils.AssertTicksFail(
                    _context,
                    _initPoolResult.InitPoolParams.WhirlpoolPda,
                    lower, upper
                );
            }
            
            [Test]  
            [Description("fail when user pass in an out of bound tick index for upper-index")]
            public static async Task UpperIndexOutOfBounds()
            {
                await AssertTicksFail(0, TickConstants.MAX_TICK_INDEX + 1);
            }

            [Test]  
            [Description("fail when user pass in a lower tick index that is higher than the upper-index")]
            public static async Task LowerIndexHigherThanUpper()
            {
                await AssertTicksFail(-22534, -22534 - 1);
            }

            [Test]  
            [Description("fail when user pass in a lower tick index that equals the upper-index")]
            public static async Task LowerIndexEqualUpper()
            {
                await AssertTicksFail(22365, 22365);
            }

            [Test]  
            [Description("fail when user pass in an out of bound tick index for lower-index")]
            public static async Task LowerIndexOutOfBounds()
            {
                await AssertTicksFail(TickConstants.MIN_TICK_INDEX - 1, 0);
            }

            [Test]  
            [Description("fail when user pass in a non-initializable tick index for upper-index")]
            public static async Task UpperIndexNonInitializable()
            {
                await AssertTicksFail(0, 1);
            }

            [Test]  
            [Description("fail when user pass in a non-initializable tick index for lower-index")]
            public static async Task LowerIndexNonInitializable()
            {
                await AssertTicksFail(1, 2);
            }
        }
    }
}