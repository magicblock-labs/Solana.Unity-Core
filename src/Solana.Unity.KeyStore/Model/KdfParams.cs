using Newtonsoft.Json;

namespace Solana.Unity.KeyStore.Model
{
    public class KdfParams
    {
        // ReSharper disable once StringLiteralTypo
        [JsonProperty("dklen")]
        // ReSharper disable once IdentifierTypo
        public int Dklen { get; init; }

        [JsonProperty("salt")]
        public string Salt { get; set; }
    }
}