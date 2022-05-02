using Newtonsoft.Json;
using Sol.Unity.Rpc.Models;
using System;


namespace Sol.Unity.Rpc.Converters
{
    /// <summary>
    /// Converts a TransactionError from json into its model representation.
    /// </summary>
    public class TransactionErrorJsonConverter : JsonConverter<TransactionError>
    {
        /// <summary>
        /// Reads and converts the JSON to type <c>TransactionError</c>.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="objectType"> The type to convert.</param>
        /// <param name="existingValue">An existing values</param>
        /// <param name="hasExistingValue">If it has an existing values</param>
        /// <param name="serializer">The serializer</param>
        /// <returns>The converted value.</returns>
        public override TransactionError ReadJson(JsonReader reader, Type objectType, TransactionError existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            var err = new TransactionError();

            if (reader.TokenType == JsonToken.String)
            {
                var enumValue = reader.ReadAsString();

                Enum.TryParse(enumValue, ignoreCase: false, out TransactionErrorType errorType);
                err.Type = errorType;
                reader.Read();
                return err;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new JsonException("Unexpected error value.");
            }

            reader.Read();

            if (reader.TokenType != JsonToken.PropertyName)
            {
                throw new JsonException("Unexpected error value.");
            }


            {
                var enumValue = (string)reader.Value;
                Enum.TryParse(enumValue, ignoreCase: false, out TransactionErrorType errorType);
                err.Type = errorType;
            }

            reader.Read();
            err.InstructionError = new InstructionError();

            if (reader.TokenType != JsonToken.StartArray)
            {
                throw new JsonException("Unexpected error value.");
            }

            reader.Read();

            if (reader.TokenType != JsonToken.Integer)
            {
                throw new JsonException("Unexpected error value.");
            }

            err.InstructionError.InstructionIndex = Convert.ToInt32(reader.Value);

            reader.Read();

            if (reader.TokenType == JsonToken.String)
            {
                var enumValue = (string)reader.Value;

                Enum.TryParse(enumValue, ignoreCase: false, out InstructionErrorType errorType);
                err.InstructionError.Type = errorType;
                reader.Read(); //string

                reader.Read(); //endarray
                return err;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new JsonException("Unexpected error value.");
            }

            reader.Read();


            if (reader.TokenType != JsonToken.PropertyName)
            {
                throw new JsonException("Unexpected error value.");
            }
            {
                var enumValue = (string)reader.Value;
                Enum.TryParse(enumValue, ignoreCase: false, out InstructionErrorType errorType);
                err.InstructionError.Type = errorType;
            }

            reader.Read();

            if (reader.TokenType == JsonToken.Integer)
            {
                err.InstructionError.CustomError = Convert.ToUInt32(reader.Value);
                reader.Read(); //number
                reader.Read(); //endobj
                reader.Read(); //endarray

                return err;
            }

            if (reader.TokenType != JsonToken.String)
            {
                throw new JsonException("Unexpected error value.");
            }

            err.InstructionError.BorshIoError = reader.ReadAsString();
            reader.Read(); //string
            reader.Read(); //endobj
            reader.Read(); //endarray

            return err;
        }
        
        public override void WriteJson(JsonWriter writer, TransactionError value, JsonSerializer serializer)
        {
            if (value.InstructionError != null)
            {

                // looking to output something like this...
                // { 'InstructionError': [0, 'InvalidAccountData'] }
                writer.WriteStartObject();
                writer.WritePropertyName("InstructionError");

                // innards
                var enumName = value.InstructionError.Type.ToString();
                writer.WriteStartArray();
                writer.WriteValue(value.InstructionError.InstructionIndex);
                writer.WriteValue(enumName);
                writer.WriteEndArray();

                writer.WriteEndObject();

            }
            else
                throw new NotImplementedException();
        }
    }
}