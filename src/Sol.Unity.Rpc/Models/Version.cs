using Newtonsoft.Json;

namespace Sol.Unity.Rpc.Models
{
    /// <summary>
    /// Represents the current solana versions running on the node.
    /// </summary>
    public class NodeVersion
    {
        /// <summary>
        /// Software version of solana-core.
        /// </summary>
        [JsonProperty("solana-core")]
        public string SolanaCore { get; set; }

        /// <summary>
        /// unique identifier of the current software's feature set.
        /// </summary>
        [JsonProperty("feature-set")]
        public ulong? FeatureSet { get; set; }
    }
}