using Newtonsoft.Json;

namespace Solana.Unity.KeyStore.Model
{
    public class KeyStore<TKdfParams> where TKdfParams : KdfParams
    {
        [JsonProperty("crypto")]
        public CryptoInfo<TKdfParams> Crypto { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonConstructor]
        public KeyStore()
        {
        }

        public KeyStore(CryptoInfo<TKdfParams> crypto, string id, string address, int version)
        {
            Crypto = crypto;
            Id = id;
            Address = address;
            Version = version;
        }
    }
}