using Newtonsoft.Json;

namespace Solana.Unity.KeyStore.Model
{
    public class Pbkdf2Params : KdfParams
    {
        [JsonProperty("c")]
        public int Count { get; init; }

        [JsonProperty("prf")]
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public string Prf { get; init; }
    }
}