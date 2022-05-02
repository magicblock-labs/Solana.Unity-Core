using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sol.Unity.KeyStore.Exceptions;
using Sol.Unity.KeyStore.Model;
using Sol.Unity.KeyStore.Services;
using System;
using System.Runtime.Serialization;

namespace Sol.Unity.KeyStore
{
    /// <summary>
    /// Implements a checker for the <see cref="KeyStore{TKdfParams}"/>'s <see cref="KdfType"/>.
    /// </summary>
    public static class KeyStoreKdfChecker
    {
        /// <summary>
        /// Get the kdf type string from the json document.
        /// </summary>
        /// <param name="keyStoreDocument">The json document.</param>
        /// <returns>The kdf type string.</returns>
        /// <exception cref="JsonException">Throws exception when json property <c>crypto</c> or <c>kdf</c> couldn't be found</exception>
        private static string GetKdfTypeFromJson(JObject keyStoreDocument)
        {
            var cryptoObj = keyStoreDocument.Property("crypto");
            if (cryptoObj == null) throw new JsonException("could not get crypto params object from json");

            var kdfObj = ((JObject)cryptoObj.Value).Property("kdf");
            if (kdfObj == null) throw new JsonException("could not get kdf object from json");

            return kdfObj.Value.ToString();
        }

        /// <summary>
        /// Gets the kdf type from the json keystore.
        /// </summary>
        /// <param name="json">The json keystore.</param>
        /// <returns>The kdf type.</returns>
        /// <exception cref="ArgumentNullException">Throws exception when <c>json</c> param is null.</exception>
        /// <exception cref="SerializationException">Throws exception when file could not be processed to <see cref="JsonDocument"/>.</exception>
        /// <exception cref="JsonException">Throws exception when <c>kdf</c> json property is <c>null</c>.</exception>
        /// <exception cref="InvalidKdfException">Throws exception when the <c>kdf</c> json property has an invalid <see cref="KdfType"/> value.</exception>
        public static KdfType GetKeyStoreKdfType(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var keyStoreDocument = JsonConvert.DeserializeObject<JObject>(json);
            if (keyStoreDocument == null) throw new SerializationException("could not process json");

            var kdfString = GetKdfTypeFromJson(keyStoreDocument);

            if (kdfString == null) throw new JsonException("could not get kdf type from json");
            return kdfString switch
            {
                KeyStorePbkdf2Service.KdfType => KdfType.Pbkdf2,
                KeyStoreScryptService.KdfType => KdfType.Scrypt,
                _ => throw new InvalidKdfException(kdfString)
            };
        }
    }
}