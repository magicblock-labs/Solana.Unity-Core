using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sol.Unity.Rpc.Converters
{
    /// <inheritdoc/>
    public class AccountDataConverter : JsonConverter<List<string>>
    {
        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, List<string> value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Read an AccountDataConverter from json into its model representation.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="objectType"></param>
        /// <param name="existingValue"></param>
        /// <param name="hasExistingValue"></param>
        /// <param name="serializer"></param>
        /// <returns></returns>
        /// <exception cref="JsonException"></exception>
        public override List<string> ReadJson(JsonReader reader, Type objectType, List<string> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
                return serializer.Deserialize<List<string>>(reader);

            if(reader.TokenType == JsonToken.StartObject)
            {
                var doc = JObject.Load(reader);
                var jsonAsString = doc.Root.ToString();
            
                return new List<string>() { jsonAsString, "jsonParsed" };
            }

            throw new JsonException("Unable to parse account data");
        }
    }
}