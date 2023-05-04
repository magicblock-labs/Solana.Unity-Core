using NUnit.Framework;
using System.Threading.Tasks;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Dex.Orca.SolanaApi;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Orca.Core.Program; 
using Solana.Unity.Dex.Orca.Core.Errors;

namespace Solana.Unity.Dex.Test.Orca.Integration
{
    
    [TestFixture]
    public class ClosePositionTests
    {
        private static TestWhirlpoolContext _context;
        private static Account _funderKeyPair;
        private static PoolInitResult _initPoolResult;
        private static Commitment _defaultCommitment;

        [SetUp]
        public static async Task Setup() 
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
            _defaultCommitment = _context.WhirlpoolClient.DefaultCommitment;

            _initPoolResult = await PoolTestUtils.BuildPool(_context);
            Assert.IsTrue(_initPoolResult.WasSuccessful);

            _funderKeyPair = new Account();
        }

        [Test] 
        [Description("successfully closes an open position")]
        public static async Task SuccessfulClosePosition()
        {
            //generate params to open position 
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context,
                _initPoolResult.InitPoolParams.WhirlpoolPda
            );

            //open the position 
            var openPositionResult = await PositionTestUtils.OpenPositionAsync(
                _context,
                openPositionParams
            );
            
            Assert.IsTrue(openPositionResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openPositionResult.Result, _defaultCommitment));

            //generate params to close position 
            ClosePositionParams closePositionParams = PositionTestUtils.GenerateCloseParams(
                _context,
                openPositionParams
            );
            
            //position before closing 
            await _context.WhirlpoolClient.GetPositionAsync(
                closePositionParams.Accounts.Position.ToString()
                , _defaultCommitment
            );

            //close the position 
            var closePositionResult = await PositionTestUtils.ClosePositionAsync(
                _context,
                closePositionParams
            );

            Assert.IsTrue(closePositionResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(closePositionResult.Result, _defaultCommitment));

            //get token supply
            var tokenResult = await _context.RpcClient.GetTokenSupplyAsync(openPositionParams.Accounts.PositionMint, _defaultCommitment);

            //position after closing 
            var positionAccount = (await _context.RpcClient.GetAccountInfoAsync(openPositionParams.Accounts.Position, _defaultCommitment)).Result.Value;
            var positionTokenAccount = (await _context.RpcClient.GetAccountInfoAsync(openPositionParams.Accounts.PositionTokenAccount, _defaultCommitment)).Result.Value;
            var receiverAccount = (await _context.RpcClient.GetAccountInfoAsync(closePositionParams.Accounts.Receiver, _defaultCommitment)).Result.Value;

            Assert.That(tokenResult.Result.Value.AmountUlong, Is.EqualTo(0));
            
            Assert.IsNull(positionAccount);
            Assert.IsNull(positionTokenAccount);
            Assert.IsNotNull(receiverAccount); 
            Assert.That(receiverAccount.Lamports, Is.GreaterThan(0));

            var closedPosition = await _context.WhirlpoolClient.GetPositionAsync(
                closePositionParams.Accounts.Position.ToString()
            ); 
                    
            Assert.IsFalse(closedPosition.WasSuccessful); 
            Assert.IsNull(closedPosition.ParsedResult); 
        }

        [Test]  
        [Description("succeeds if the position is delegated")]
        public static async Task SuccessfulCloseDelegatedPosition()
        {
            PoolInitResult poolInitResult = _initPoolResult;
            
            Account ownerAccount = new Account();
            Account delegateAccount = new Account();
            
            //fund the accounts 
            await Task.WhenAll(new Task[]{
                SolUtils.FundTestAccountAsync(_context, ownerAccount),
                SolUtils.FundTestAccountAsync(_context, delegateAccount)
            });
            
            //params to open position 
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context,
                poolInitResult.InitPoolParams.Accounts.Whirlpool,
                0, 128,
                ownerAccount
            );

            //open position 
            var openPositionResult = await PositionTestUtils.OpenPositionAsync(
                _context, 
                openPositionParams
            );
            
            Assert.IsTrue(openPositionResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openPositionResult.Result, _defaultCommitment));
            
            //approve delegate 
            var approveResult = await TokenUtils.ApproveTokenAsync(
                _context,
                tokenAccount: openPositionParams.Accounts.PositionTokenAccount,
                delegateAccount: delegateAccount.PublicKey, 
                amount: 1,
                ownerAccount: ownerAccount, commitment: Commitment.Confirmed
            );
            
            Assert.IsTrue(approveResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(approveResult.Result, _defaultCommitment));
            
            //set newAuthority as authority
            var setAuthorityResult = await TokenUtils.SetAuthorityAsync(
                _context,
                tokenAccount: openPositionParams.Accounts.PositionTokenAccount,
                newAuthority: delegateAccount.PublicKey,
                authorityType: Solana.Unity.Programs.AuthorityType.CloseAccount,
                authorityAccount: ownerAccount, 
                commitment: Commitment.Confirmed
            );

            Assert.IsTrue(setAuthorityResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(setAuthorityResult.Result, _defaultCommitment));

            //close position should not throw 
            var closeResult = await PositionTestUtils.ClosePositionAsync(
                _context,
                PositionTestUtils.GenerateCloseParams(
                    _context,
                    openPositionParams,
                    positionAuthority: delegateAccount
                ), 
                feePayer: delegateAccount
            ); 
            
            Assert.IsTrue(closeResult.WasSuccessful); 
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(closeResult.Result, _defaultCommitment));
        }

        [Test]  
        [Description("succeeds with the owner's signature even if the token is delegated")]
        public static async Task SuccessfulCloseDelegatedPositionWithOwnerSig()
        {
            PoolInitResult poolInitResult = _initPoolResult;

            Account ownerAccount = new Account();
            Account delegateAccount = new Account();

            //fund the accounts 
            await Task.WhenAll(new Task[]{
                SolUtils.FundTestAccountAsync(_context, ownerAccount),
                SolUtils.FundTestAccountAsync(_context, delegateAccount)
            });

            //params to open position 
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context,
                poolInitResult.InitPoolParams.Accounts.Whirlpool,
                0, 128,
                ownerAccount
            );

            //open position 
            var openPositionResult = await PositionTestUtils.OpenPositionAsync(
                _context,
                openPositionParams
            );

            Assert.IsTrue(openPositionResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openPositionResult.Result, _defaultCommitment));

            //approve delegate 
            var approveResult = await TokenUtils.ApproveTokenAsync(
                _context,
                tokenAccount: openPositionParams.Accounts.PositionTokenAccount,
                delegateAccount: delegateAccount,
                amount: 1,
                ownerAccount: ownerAccount, 
                commitment: Commitment.Confirmed
            );

            Assert.IsTrue(approveResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(approveResult.Result, _defaultCommitment));

            var closeResult = await PositionTestUtils.ClosePositionAsync(
                _context, 
                PositionTestUtils.GenerateCloseParams(
                    _context, 
                    openPositionParams,
                    receiver: ownerAccount,
                    positionAuthority: ownerAccount
                ), 
                feePayer: ownerAccount
            );

            Assert.IsTrue(closeResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(closeResult.Result, _defaultCommitment));
        }

        [Test] 
        [Description("succeeds with position token that was transferred to new owner")]
        public static async Task SuccessfulWithPositionToken()
        {
            WhirlpoolsTestFixture testFixture = await WhirlpoolsTestFixture.CreateInstance(
                _context, 
                positions: new FundedPositionParams[]{ 
                    new()
                    {
                        TickLowerIndex = 0, TickUpperIndex = 128, LiquidityAmount = 0
                    }
                }
            ); 
            var position = testFixture.GetTestInfo().Positions[0]; 
            
            //create new owner with token 
            Account newOwner = new Account();

            //fund new owner 
            await SolUtils.FundTestAccountAsync(_context, newOwner);
            
            PublicKey newOwnerPositionToken = await TokenUtils.CreateTokenAccountAsync(
                _context, 
                position.MintKeyPair, 
                newOwner, 
                commitment: Commitment.Confirmed
            );

            //transfer token 
            await TokenUtils.TransferTokensAsync(
                _context, position.TokenAccount, newOwnerPositionToken, 1,
                feePayer: _context.WalletAccount, commitment: Commitment.Confirmed
            ); 
            
            //close position should not throw 
            var closeResult = await PositionTestUtils.ClosePositionAsync(
                _context,
                new ClosePositionParams
                {
                    Accounts = new ClosePositionAccounts
                    {
                        PositionAuthority = newOwner.PublicKey,
                        Receiver = newOwner.PublicKey,
                        Position = position.PublicKey,
                        PositionMint = position.MintKeyPair.PublicKey,
                        PositionTokenAccount = newOwnerPositionToken
                    }
                },
                feePayer: newOwner 
            );
        
            Assert.IsTrue(closeResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(closeResult.Result, _defaultCommitment));
        }

        [Test] 
        [Description("fails to close a position with liquidity")]
        public static async Task FailsToClosePositionWithLiquidity()
        {
            PoolInitWithLiquidityResult poolInitResult = await PoolTestUtils.BuildPoolWithLiquidity(
                _context
            );
            
            Account receiverAccount = new Account(); 
            
            var closeResult = await PositionTestUtils.ClosePositionAsync(
                _context, 
                PositionTestUtils.GenerateCloseParams(
                    _context, 
                    poolInitResult.InitPositionParams, 
                    receiver: receiverAccount
                )
            ); 
            
            AssertUtils.AssertFailedWithCustomError(closeResult, WhirlpoolErrorType.ClosePositionNotEmpty); 
        }

        [Test] 
        [Description("fails if owner is not signer")]
        public static async Task FailsOwnerNotSigner()
        {
            PoolInitResult poolInitResult = _initPoolResult;

            Account ownerAccount = new Account();
            await SolUtils.FundTestAccountAsync(_context, ownerAccount, SolConstants.ONE_SOL); 

            //params to open position 
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context,
                poolInitResult.InitPoolParams.Accounts.Whirlpool,
                0, 128,
                ownerAccount
            );

            //open position 
            var openPositionResult = await PositionTestUtils.OpenPositionAsync(
                _context,
                openPositionParams
            );
            
            Assert.IsTrue(openPositionResult.WasSuccessful); 
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openPositionResult.Result, _defaultCommitment));

            //TODO: (LOW) all properties that are public keys should end in "Key". All properties that are accounts should end in "Account"
            
            //expect error Signature verification failed
            var closeResult = await PositionTestUtils.ClosePositionAsync(
                _context, 
                PositionTestUtils.GenerateCloseParams(
                    _context, 
                    openPositionParams, 
                    receiver: ownerAccount,
                    positionAuthority: ownerAccount
                )
            );

            AssertUtils.AssertSignatureError(closeResult);
        }

        [Test]  
        [Description("fails if delegate is not signer")]
        public static async Task FailsDelegateNotSigner()
        {
            PoolInitResult poolInitResult = _initPoolResult;

            Account ownerAccount = new Account();
            Account delegateAccount = new Account();

            await SolUtils.FundTestAccountAsync(_context, ownerAccount);
            await SolUtils.FundTestAccountAsync(_context, delegateAccount);

            //params to open position with ownerAccount as owner 
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context,
                poolInitResult.InitPoolParams.Accounts.Whirlpool,
                0, 128,
                owner: ownerAccount
            );

            //open position 
            var openPositionResult = await PositionTestUtils.OpenPositionAsync(
                _context,
                openPositionParams
            );

            Assert.IsTrue(openPositionResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openPositionResult.Result, _defaultCommitment));

            //approve delegate (1)
            var approveResult = await TokenUtils.ApproveTokenAsync(
                _context,
                tokenAccount: openPositionParams.Accounts.PositionTokenAccount,
                delegateAccount: delegateAccount,
                amount: 1,
                ownerAccount: ownerAccount, 
                commitment: Commitment.Confirmed
            );

            Assert.IsTrue(approveResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(approveResult.Result, _defaultCommitment));

            //set delegate as authority to close 
            var setAuthorityResult = await TokenUtils.SetAuthorityAsync(
                _context,
                tokenAccount: openPositionParams.Accounts.PositionTokenAccount,
                newAuthority: delegateAccount.PublicKey,
                authorityType: Solana.Unity.Programs.AuthorityType.CloseAccount,
                authorityAccount: ownerAccount,
                commitment: Commitment.Confirmed
            );
            
            Assert.IsTrue(setAuthorityResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(setAuthorityResult.Result, _defaultCommitment));
      
            //expect error Signature verification failed
            var closeResult = await PositionTestUtils.ClosePositionAsync(
                _context,
                PositionTestUtils.GenerateCloseParams(
                    _context,
                    openPositionParams,
                    receiver: ownerAccount,
                    positionAuthority: delegateAccount
                )
            );

            AssertUtils.AssertSignatureError(closeResult);
        }

        [Test]  
        [Description("fails if the authority does not match")]
        public static async Task FailsAuthorityNoMatch()
        {
            PoolInitResult poolInitResult = _initPoolResult;

            Account ownerAccount = new Account();
            Account fakeOwnerAccount = new Account();

            await SolUtils.FundTestAccountAsync(_context, ownerAccount);
            await SolUtils.FundTestAccountAsync(_context, fakeOwnerAccount);

            //params to open position 
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context,
                poolInitResult.InitPoolParams.Accounts.Whirlpool,
                0, 128,
                ownerAccount
            );

            //open position 
            var openPositionResult = await PositionTestUtils.OpenPositionAsync(
                _context,
                openPositionParams
            );
            
            Assert.IsTrue(openPositionResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openPositionResult.Result, _defaultCommitment));

            //closing position should throw exception
            //expect error 0x1783 MissingOrInvalidDelegate
            var closeResult = await PositionTestUtils.ClosePositionAsync(
                _context,
                PositionTestUtils.GenerateCloseParams(
                    _context,
                    openPositionParams,
                    receiver: ownerAccount,
                    positionAuthority: fakeOwnerAccount
                ),
                feePayer: fakeOwnerAccount 
            );

            AssertUtils.AssertFailedWithCustomError(closeResult, WhirlpoolErrorType.MissingOrInvalidDelegate);
        }

        [Test]  
        [Description("fails if position token account does not contain exactly one token")]
        public static async Task FailsTokenAccountCountNotOne()
        {
            WhirlpoolsTestFixture testFixture = await WhirlpoolsTestFixture.CreateInstance(
                _context, 
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = 0, TickUpperIndex = 128, LiquidityAmount = 0
                    }
                }
            );
            var position = testFixture.GetTestInfo().Positions[0];
            
            //create fake token account 
            PublicKey fakePositionTokenAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, 
                mint: position.MintKeyPair,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            //expect error ConstraintRaw
            var closeResult = await PositionTestUtils.ClosePositionAsync(
                _context,
                new ClosePositionParams
                {
                    Accounts = new ClosePositionAccounts
                    {
                        PositionAuthority = _context.WalletPubKey,
                        Receiver = _context.WalletPubKey,
                        Position = position.PublicKey,
                        PositionMint = position.MintKeyPair.PublicKey,
                        PositionTokenAccount = fakePositionTokenAccount
                    }
                }
            );

            AssertUtils.AssertFailedWithStandardError(closeResult, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Description("fails if delegated amount is 0")]
        public static async Task FailsDelegatedAmountZero()
        {
            PoolInitResult poolInitResult = _initPoolResult;

            Account ownerAccount = new Account();
            Account delegateAccount = new Account();

            //params to open position 
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context,
                poolInitResult.InitPoolParams.Accounts.Whirlpool,
                0, 128,
                owner: ownerAccount
            );

            //open position 
            var openPositionResult = await PositionTestUtils.OpenPositionAsync(
                _context,
                openPositionParams
            );

            Assert.IsTrue(openPositionResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openPositionResult.Result, _defaultCommitment));
            
            await SolUtils.FundTestAccountAsync(_context, ownerAccount);
            await SolUtils.FundTestAccountAsync(_context, delegateAccount);

            //approve delegate 
            var approveResult = await TokenUtils.ApproveTokenAsync(
                _context,
                tokenAccount: openPositionParams.Accounts.PositionTokenAccount,
                delegateAccount: delegateAccount.PublicKey,
                amount: 0,
                ownerAccount: ownerAccount,
                commitment: Commitment.Confirmed
            );

            Assert.IsTrue(approveResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(approveResult.Result, _defaultCommitment));

            //set newAuthority as authority
            var setAuthorityResult = await TokenUtils.SetAuthorityAsync(
                _context,
                tokenAccount: openPositionParams.Accounts.PositionTokenAccount,
                newAuthority: delegateAccount.PublicKey,
                authorityType: Solana.Unity.Programs.AuthorityType.CloseAccount,
                authorityAccount: ownerAccount,
                commitment: Commitment.Confirmed
            );

            Assert.IsTrue(setAuthorityResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(setAuthorityResult.Result, _defaultCommitment));

            //closing position should throw exception
            //expect error ConstraintRaw
            var closeResult = await PositionTestUtils.ClosePositionAsync(
                _context, 
                PositionTestUtils.GenerateCloseParams(
                    _context, 
                    openPositionParams,
                    receiver: ownerAccount, 
                    positionAuthority: delegateAccount
                ), 
                feePayer: delegateAccount
            );

            AssertUtils.AssertFailedWithCustomError(closeResult, WhirlpoolErrorType.InvalidPositionTokenAmount);
        }

        [Test] 
        [Description("fails if positionAuthority does not match delegate")]
        public static async Task FailsPositionAuthorityMismatch()
        {
            PoolInitResult poolInitResult = _initPoolResult;

            Account ownerAccount = new Account();
            Account delegateAccount = new Account();
            Account fakeDelegate = new Account();

            await SolUtils.FundTestAccountAsync(_context, ownerAccount);
            await SolUtils.FundTestAccountAsync(_context, fakeDelegate);
            await SolUtils.FundTestAccountAsync(_context, delegateAccount);
            

            //params to open position 
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context,
                poolInitResult.InitPoolParams.Accounts.Whirlpool,
                0, 128,
                ownerAccount
            );

            //open position 
            var openPositionResult = await PositionTestUtils.OpenPositionAsync(
                _context,
                openPositionParams
            );

            Assert.IsTrue(openPositionResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openPositionResult.Result, _defaultCommitment));

            //approve delegate 
            var approveResult = await TokenUtils.ApproveTokenAsync(
                _context,
                tokenAccount: openPositionParams.Accounts.PositionTokenAccount,
                delegateAccount: delegateAccount.PublicKey,
                amount: 1,
                ownerAccount: ownerAccount,
                commitment: Commitment.Confirmed
            );

            Assert.IsTrue(approveResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(approveResult.Result, _defaultCommitment));

            //set newAuthority as authority
            var setAuthorityResult = await TokenUtils.SetAuthorityAsync(
                _context,
                tokenAccount: openPositionParams.Accounts.PositionTokenAccount,
                newAuthority: delegateAccount.PublicKey,
                authorityType: Solana.Unity.Programs.AuthorityType.CloseAccount,
                authorityAccount: ownerAccount,
                commitment: Commitment.Confirmed
            );

            Assert.IsTrue(setAuthorityResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(setAuthorityResult.Result, _defaultCommitment));

            //expect error MissingOrInvalidDelegate
            var closeResult = await PositionTestUtils.ClosePositionAsync(
                _context,
                PositionTestUtils.GenerateCloseParams(
                    _context,
                    openPositionParams,
                    receiver: ownerAccount,
                    positionAuthority: fakeDelegate
                ),
                feePayer: fakeDelegate
            );

            AssertUtils.AssertFailedWithCustomError(closeResult, WhirlpoolErrorType.MissingOrInvalidDelegate);
        }

        [Test] 
        [Description("fails if position token account mint does not match position mint")]
        public static async Task FailsPositionTokenMintMismatch()
        {
            WhirlpoolsTestFixture testFixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = 0, TickUpperIndex = 128, LiquidityAmount = 0
                    }
                }
            );
            var position = testFixture.GetTestInfo().Positions[0];

            //create and mint to token 
            PublicKey fakePositionTokenAccount = await TokenUtils.CreateAndMintToTokenAccountAsync(
                _context,
                _context.WalletAccount,
                testFixture.GetTestInfo().InitPoolParams.Accounts.TokenMintA,
                amount: 1,
                commitment: Commitment.Confirmed
            );

            //closing position should throw exception
            //expect error ConstraintRaw
            var closeResult = await PositionTestUtils.ClosePositionAsync(
                _context,
                new ClosePositionParams 
                {
                    Accounts = new ClosePositionAccounts
                    {
                        PositionAuthority = _context.WalletPubKey,
                        Receiver = _context.WalletPubKey,
                        Position = position.PublicKey,
                        PositionMint = position.MintKeyPair.PublicKey,
                        PositionTokenAccount = fakePositionTokenAccount,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    }
                }
            );

            AssertUtils.AssertFailedWithStandardError(closeResult, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Ignore("TODO: fix this test")]
        [Description("fails if position_mint does not match position's position_mint field")]
        public static async Task FailsPositionMintMismatch()
        {
            WhirlpoolsTestFixture testFixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = 0, TickUpperIndex = 128, LiquidityAmount = 0
                    }
                }
            );
            var position = testFixture.GetTestInfo().Positions[0];

            //expect error ConstraintAddress
            var closeResult = await PositionTestUtils.ClosePositionAsync(
                _context,
                new ClosePositionParams
                {
                    Accounts = new ClosePositionAccounts
                    {
                        PositionAuthority = _context.WalletPubKey,
                        Receiver = _context.WalletPubKey,
                        Position = position.PublicKey,
                        PositionMint = testFixture.GetTestInfo().InitPoolParams.Accounts.TokenMintA,
                        PositionTokenAccount = position.TokenAccount
                    }
                }
            );

            AssertUtils.AssertFailedWithStandardError(closeResult, StandardErrorType.AddressConstraint);
        }
    }
}