using Newtonsoft.Json;

namespace Sol.Unity.KeyStore.Model
{
    public class CipherParams
    {
        public CipherParams()
        {
        }

        public CipherParams(byte[] iv)
        {
            Iv = iv.ToHex();
        }

        [JsonProperty("iv")]
        // ReSharper disable once MemberCanBePrivate.Global
        public string Iv { get; init; }
    }
}