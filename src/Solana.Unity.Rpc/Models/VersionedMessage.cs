using Solana.Unity.Rpc.Utilities;
using Solana.Unity.Wallet;
using Solana.Unity.Wallet.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Solana.Unity.Rpc.Models
{

    /// <summary>
    /// Represents the Message of a Solana <see cref="Transaction"/>.
    /// </summary>
    public class VersionedMessage: Message
    {
        /// <summary>
        /// Version prefix Mask.
        /// </summary>
        public const byte VersionPrefixMask = 0x7F;

        /// <summary>
        /// Address table lookup
        /// </summary>
        public List<MessageAddressTableLookup> AddressTableLookups { get; set; }


        /// <summary>
        /// Deserialize a compiled message into a Message object.
        /// </summary>
        /// <param name="data">The data to deserialize into the Message object.</param>
        /// <returns>The Message object instance.</returns>
        public static new VersionedMessage Deserialize(ReadOnlySpan<byte> data)
        {
            byte prefix = data[0];
            byte maskedPrefix = (byte)(prefix & VersionPrefixMask);
            
            if(prefix == maskedPrefix)
                throw new NotSupportedException("Expected versioned message but received legacy message");
            
            byte version = maskedPrefix;
            
            if(version != 0)
                throw new NotSupportedException($"Expected versioned message with version 0 but found version {version}");
            
            data = data.Slice(1, data.Length - 1); // Remove the processed prefix byte
            
            // Read message header
            byte numRequiredSignatures = data[MessageHeader.Layout.RequiredSignaturesOffset];
            byte numReadOnlySignedAccounts = data[MessageHeader.Layout.ReadOnlySignedAccountsOffset];
            byte numReadOnlyUnsignedAccounts = data[MessageHeader.Layout.ReadOnlyUnsignedAccountsOffset];

            // Read account keys
            (int accountAddressLength, int accountAddressLengthEncodedLength) =
                ShortVectorEncoding.DecodeLength(data.Slice(MessageHeader.Layout.HeaderLength,
                    ShortVectorEncoding.SpanLength));
            List<PublicKey> accountKeys = new(accountAddressLength);
            for (int i = 0; i < accountAddressLength; i++)
            {
                ReadOnlySpan<byte> keyBytes = data.Slice(
                    MessageHeader.Layout.HeaderLength + accountAddressLengthEncodedLength +
                    i * PublicKey.PublicKeyLength,
                    PublicKey.PublicKeyLength);
                accountKeys.Add(new PublicKey(keyBytes));
            }

            // Read block hash
            string blockHash =
                Encoders.Base58.EncodeData(data.Slice(
                    MessageHeader.Layout.HeaderLength + accountAddressLengthEncodedLength +
                    accountAddressLength * PublicKey.PublicKeyLength,
                    PublicKey.PublicKeyLength).ToArray());

            // Read the number of instructions in the message
            (int instructionsLength, int instructionsLengthEncodedLength) =
                ShortVectorEncoding.DecodeLength(
                    data.Slice(
                        MessageHeader.Layout.HeaderLength + accountAddressLengthEncodedLength +
                        (accountAddressLength * PublicKey.PublicKeyLength) + PublicKey.PublicKeyLength,
                        ShortVectorEncoding.SpanLength));

            List<CompiledInstruction> instructions = new(instructionsLength);
            int instructionsOffset =
                MessageHeader.Layout.HeaderLength + accountAddressLengthEncodedLength +
                (accountAddressLength * PublicKey.PublicKeyLength) + PublicKey.PublicKeyLength +
                instructionsLengthEncodedLength;
            ReadOnlySpan<byte> instructionsData = data[instructionsOffset..];

            int instructionsDataLength = 0;

            // Read the instructions in the message
            for (int i = 0; i < instructionsLength; i++)
            {
                (CompiledInstruction compiledInstruction, int instructionLength) =
                    CompiledInstruction.Deserialize(instructionsData);
                instructions.Add(compiledInstruction);
                instructionsData = instructionsData[instructionLength..];
                instructionsDataLength += instructionLength;
            }
            
            // Read the address table lookups
            int tableLookupOffset =
                MessageHeader.Layout.HeaderLength + accountAddressLengthEncodedLength +
                (accountAddressLength * PublicKey.PublicKeyLength) + PublicKey.PublicKeyLength +
                instructionsLengthEncodedLength + instructionsDataLength;
            
            ReadOnlySpan<byte> tableLookupData = data[tableLookupOffset..];
            
            (int addressTableLookupsCount, int addressTableLookupsEncodedCount) = ShortVectorEncoding.DecodeLength(tableLookupData);
            List<MessageAddressTableLookup> addressTableLookups = new();
            tableLookupData = tableLookupData[addressTableLookupsEncodedCount..];
            
            for (int i = 0; i < addressTableLookupsCount; i++)
            {
                byte[] accountKeyBytes = tableLookupData.Slice(0, PublicKey.PublicKeyLength).ToArray();
                PublicKey accountKey = new(accountKeyBytes);
                tableLookupData = tableLookupData.Slice(PublicKey.PublicKeyLength);

                (int writableIndexesLength, int writableIndexesEncodedLength) = ShortVectorEncoding.DecodeLength(tableLookupData);
                List<byte> writableIndexes = tableLookupData.Slice(writableIndexesEncodedLength, writableIndexesLength).ToArray().ToList();
                tableLookupData = tableLookupData.Slice(writableIndexesEncodedLength + writableIndexesLength);

                (int readonlyIndexesLength, int readonlyIndexesEncodedLength) = ShortVectorEncoding.DecodeLength(tableLookupData);
                List<byte> readonlyIndexes = tableLookupData.Slice(readonlyIndexesEncodedLength, readonlyIndexesLength).ToArray().ToList();
                tableLookupData = tableLookupData.Slice(readonlyIndexesEncodedLength + readonlyIndexesLength);

                addressTableLookups.Add(new MessageAddressTableLookup
                {
                    AccountKey = accountKey,
                    WritableIndexes = writableIndexes.ToArray(),
                    ReadonlyIndexes = readonlyIndexes.ToArray()
                });
            }

            return new VersionedMessage()
            {
                Header = new MessageHeader()
                {
                    RequiredSignatures = numRequiredSignatures,
                    ReadOnlySignedAccounts = numReadOnlySignedAccounts,
                    ReadOnlyUnsignedAccounts = numReadOnlyUnsignedAccounts
                },
                RecentBlockhash = blockHash,
                AccountKeys = accountKeys,
                Instructions = instructions,
                AddressTableLookups = addressTableLookups
            };
        }
        
        /// <summary>
        /// Deserialize a compiled message encoded as base-64 into a Message object.
        /// </summary>
        /// <param name="data">The data to deserialize into the Message object.</param>
        /// <returns>The Transaction object.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the given string is null.</exception>
        public static new VersionedMessage Deserialize(string data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            byte[] decodedBytes;

            try
            {
                decodedBytes = Convert.FromBase64String(data);
            }
            catch (Exception ex)
            {
                throw new Exception("could not decode message data from base64", ex);
            }

            return Deserialize(decodedBytes);
        }

        /// <summary>
        /// Deserialize the message version
        /// </summary>
        /// <param name="serializedMessage"></param>
        /// <returns></returns>
        public static string DeserializeMessageVersion(byte[] serializedMessage)
        {
            byte prefix = serializedMessage[0];
            byte maskedPrefix = (byte)(prefix & VersionPrefixMask);

            // If the highest bit of the prefix is not set, the message is not versioned
            if (maskedPrefix == prefix)
            {
                return "legacy";
            }

            // The lower 7 bits of the prefix indicate the message version
            return maskedPrefix.ToString();
        }
    }

    /// <summary>
    /// Message Address Lookup table
    /// </summary>
    public class MessageAddressTableLookup
    {
        /// <summary>
        /// Account Key
        /// </summary>
        public PublicKey AccountKey { get; set; }

        /// <summary>
        /// Writable indexes
        /// </summary>
        public byte[] WritableIndexes { get; set; }

        /// <summary>
        /// Read only indexes
        /// </summary>
        public byte[] ReadonlyIndexes { get; set; }
    }
    
    /// <summary>
    /// Message Address Lookup table
    /// </summary>
    public static class AddressTableLookupUtils
    {
        /// <summary>
        /// Serialize the address table lookups
        /// </summary>
        /// <param name="addressTableLookups"></param>
        /// <returns></returns>
        public static byte[] SerializeAddressTableLookups(List<MessageAddressTableLookup> addressTableLookups)
        {
            MemoryStream buffer = new();
            
            var encodedAddressTableLookupsLength = ShortVectorEncoding.EncodeLength(addressTableLookups.Count);
            buffer.Write(encodedAddressTableLookupsLength, 0, encodedAddressTableLookupsLength.Length);

            foreach (var lookup in addressTableLookups)
            {
                // Write the Account Key
                buffer.Write(lookup.AccountKey, 0, PublicKey.PublicKeyLength);
                
                // Write the Writable Indexes
                var encodedWritableIndexesLength = ShortVectorEncoding.EncodeLength(lookup.WritableIndexes.Length);
                buffer.Write(encodedWritableIndexesLength, 0, encodedWritableIndexesLength.Length);
                buffer.Write(lookup.WritableIndexes, 0, lookup.WritableIndexes.Length);
                
                // Write the Readonly Indexes
                var encodedReadonlyIndexesLength = ShortVectorEncoding.EncodeLength(lookup.ReadonlyIndexes.Length);
                buffer.Write(encodedReadonlyIndexesLength, 0, encodedReadonlyIndexesLength.Length);
                buffer.Write(lookup.ReadonlyIndexes, 0, lookup.ReadonlyIndexes.Length);
            }

            return buffer.ToArray();
        }
    }
}