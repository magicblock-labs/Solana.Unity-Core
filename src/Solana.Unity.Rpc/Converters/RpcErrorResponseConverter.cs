using Newtonsoft.Json;
using Solana.Unity.Rpc.Messages;
using System;

namespace Solana.Unity.Rpc.Converters
{
    /// <summary>
    /// Converts a TransactionError from json into its model representation.
    /// </summary>
    public class RpcErrorResponseConverter : JsonConverter<JsonRpcErrorResponse>
    {
        
        /// <summary>
        /// Reads and converts the JSON to type <c>JsonRpcErrorResponse</c>.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="objectType"> The type to convert.</param>
        /// <param name="existingValue">An existing values</param>
        /// <param name="hasExistingValue">If it has an existing values</param>
        /// <param name="serializer">The serializer</param>
        /// <returns>The converted value.</returns>
        public override JsonRpcErrorResponse ReadJson(JsonReader reader, Type objectType, JsonRpcErrorResponse existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.StartObject) return null;

            reader.Read();

            var err = new JsonRpcErrorResponse();

            while (reader.TokenType != JsonToken.EndObject)
            {
                var prop = (string)reader.Value;

                reader.Read();

                if ("jsonrpc" == prop)
                {
                    // do nothing
                }
                else if ("id" == prop)
                {
                    err.Id = Convert.ToInt32(reader.Value);
                }
                else if ("error" == prop)
                {
                    if(reader.TokenType == JsonToken.String)
                    {
                        err.ErrorMessage = (string)reader.Value;
                    }
                    else if(reader.TokenType == JsonToken.StartObject)
                    {
                        err.Error = serializer.Deserialize<ErrorContent>(reader);
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
                else
                {
                    reader.Skip();
                }

                reader.Read();
            }
            return err;
        }
        
        /// <summary>
        /// Not yet implemented
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="serializer"></param>
        /// <exception cref="NotImplementedException"></exception>
        public override void WriteJson(JsonWriter writer, JsonRpcErrorResponse value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}