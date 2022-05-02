using Newtonsoft.Json;
using Sol.Unity.KeyStore.Model;

namespace Sol.Unity.KeyStore.Serialization
{
    public static class JsonKeyStoreScryptSerializer
    {
        public static string SerializeScrypt(KeyStore<ScryptParams> scryptKeyStore)
        {
            return JsonConvert.SerializeObject(scryptKeyStore);
        }

        public static KeyStore<ScryptParams> DeserializeScrypt(string json)
        {
            return JsonConvert.DeserializeObject<KeyStore<ScryptParams>>(json);
        }
    }
}