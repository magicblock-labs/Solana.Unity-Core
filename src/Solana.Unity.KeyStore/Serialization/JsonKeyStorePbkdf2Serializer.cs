using Solana.Unity.KeyStore.Model;
using Newtonsoft.Json;

namespace Solana.Unity.KeyStore.Serialization
{
    public static class JsonKeyStorePbkdf2Serializer
    {
        public static string SerialisePbkdf2(KeyStore<Pbkdf2Params> pbkdf2KeyStore)
        {
            return JsonConvert.SerializeObject(pbkdf2KeyStore);
        }

        public static KeyStore<Pbkdf2Params> DeserializePbkdf2(string json)
        {
            return JsonConvert.DeserializeObject<KeyStore<Pbkdf2Params>>(json);
        }
    }
}