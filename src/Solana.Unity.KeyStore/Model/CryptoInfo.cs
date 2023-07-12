using Newtonsoft.Json;

namespace Solana.Unity.KeyStore.Model
{
    public class CryptoInfo<TKdfParams> where TKdfParams : KdfParams
    {
        
        [JsonConstructor]
        public CryptoInfo()
        {
        }

        public CryptoInfo(string cipher, byte[] cipherText, byte[] iv, byte[] mac, byte[] salt, TKdfParams kdfParams,
            string kdfType)
        {
            Cipher = cipher;
            CipherText = cipherText.ToHex();
            Mac = mac.ToHex();
            CipherParams = new CipherParams(iv);
            Kdfparams = kdfParams;
            Kdfparams.Salt = salt.ToHex();
            Kdf = kdfType;
        }

        [JsonProperty("cipher")]
        // ReSharper disable once MemberCanBePrivate.Global
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public string Cipher { get; }

        [JsonProperty("ciphertext")]
        // ReSharper disable once MemberCanBePrivate.Global
        public string CipherText { get; init; }

        // ReSharper disable once StringLiteralTypo
        [JsonProperty("cipherparams")]
        // ReSharper disable once MemberCanBePrivate.Global
        public CipherParams CipherParams { get; init; }

        [JsonProperty("kdf")]
        // ReSharper disable once MemberCanBePrivate.Global
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public string Kdf { get; }

        [JsonProperty("mac")]
        // ReSharper disable once MemberCanBePrivate.Global
        public string Mac { get; init; }

        // ReSharper disable once StringLiteralTypo
        [JsonProperty("kdfparams")]
        // ReSharper disable once IdentifierTypo
        // ReSharper disable once MemberCanBePrivate.Global
        public TKdfParams Kdfparams { get; init; }
    }
}