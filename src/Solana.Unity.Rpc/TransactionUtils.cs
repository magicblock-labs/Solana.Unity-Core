using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Messages;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Solana.Unity.Rpc
{
    /// <summary>
    /// Utilities to check transaction confirmation status.
    /// </summary>
    public static class TransactionConfirmationUtils
    {
        /// <summary>
        /// Confirms a transaction - same method as web3.js.
        /// </summary>
        /// <param name="rpc">The rpc client instance.</param>
        /// <param name="streamingRpcClient">The streaming rpc client instance.</param>
        /// <param name="hash">The hash of the transaction.</param>
        /// <param name="validBlockHeight">The last valid block height of the blockhash used in the transaction.</param>
        /// <param name="commitment">The state commitment to consider when querying the ledger state.</param>
        /// <returns>Returns null if the transaction wasn't confirmed, otherwise returns the confirmation slot and possible transaction error.</returns>
        public static async Task<ResponseValue<ErrorResult>> ConfirmTransaction(IRpcClient rpc, IStreamingRpcClient streamingRpcClient, 
            string hash, ulong validBlockHeight, Commitment commitment = Commitment.Finalized)
        {
            TaskCompletionSource<object> t = new();
            ResponseValue<ErrorResult> result = null;

            var s = await streamingRpcClient.SubscribeSignatureAsync(hash, (s, e) =>
            {
                result = e;
                t.SetResult(null);
            },
            commitment);

            var checkTask = Task.Run(async () =>
            {
                var currHeight = await rpc.GetBlockHeightAsync(commitment);
                while (currHeight.Result < validBlockHeight)
                {
                    currHeight = await rpc.GetBlockHeightAsync(commitment);
                }
            });


            Task.WaitAny(t.Task, checkTask);

            if (!t.Task.IsCompleted)
            {
                await s.UnsubscribeAsync();
            }

            return result;
        }

        /// <summary>
        /// Confirms a transaction - using polling and constant timeout based on commitment parameter.
        /// </summary>
        /// <param name="rpc">The rpc client instance.</param>
        /// <param name="hash">The hash of the transaction.</param>
        /// <param name="commitment">The state commitment to consider when querying the ledger state.</param>
        /// <returns>Returns false if the transaction wasn't confirmed, otherwise returns true.</returns>
        public static async Task<bool> ConfirmTransaction(
            IRpcClient rpc, string hash, Commitment commitment = Commitment.Finalized)
        {
            //timeout in milliseconds, if commitment level not reached 
            var timeout = commitment == Commitment.Finalized ? TimeSpan.FromSeconds(60) : TimeSpan.FromSeconds(30);
            CancellationTokenSource cancelSource = new();
            CancellationToken cancelToken = cancelSource.Token;
            cancelSource.CancelAfter(timeout);

            //Commitment.Processed is meaningless here 
            if (commitment == Commitment.Processed)
                commitment = Commitment.Confirmed;

            //response must be valid, or else return false 
            //wait until timeout 
            while (!cancelToken.IsCancellationRequested)
            {
                //try to get completed transaction 
                var tx = await rpc.GetSignatureStatusesAsync(
                    new List<string>(){hash}, true);
                if (tx.WasSuccessful && tx.Result?.Value != null && tx.Result.Value.TrueForAll(sgn =>
                        sgn?.ConfirmationStatus != null && (sgn.ConfirmationStatus.Equals(commitment.ToString().ToLower())
                        || (commitment.Equals(Commitment.Confirmed) && 
                            sgn.ConfirmationStatus.Equals(Commitment.Finalized.ToString().ToLower())))))
                {
                    return true;
                }
            }

            return false;
        }

    }
}