// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

using Newtonsoft.Json;

namespace Solana.Unity.Rpc.Models
{
    /// <summary>
    /// Represents a token account.
    /// </summary>
    public class TokenAccount
    {
        /// <summary>
        /// The token account info.
        /// </summary>
        public TokenAccountInfo Account { get; set; }

        /// <summary>
        /// A base-58 encoded public key representing the account's public key.
        /// </summary>
        [JsonProperty("pubkey")]
        public string PublicKey { get; set; }
    }
}