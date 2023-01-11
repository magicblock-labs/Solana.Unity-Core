using System;
using System.Linq;
using System.Collections.Generic;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public class MetadataParser
    {
        private const int URI_INDEX = 119; 
        private const int URI_LENGTH = 100; 
        private const int UPDATE_AUTH_INDEX = 1; 
        private const int MINT_INDEX = 33; 
        
        public string UpdateAuthority { get; private set; }
        public string Mint { get; private set; }
        public string Uri { get; private set; }
        
        public MetadataParser(IList<string> accountData) 
        {
            if (accountData.Count < 2 || accountData[1] != "base64")
                throw new ArgumentException("MetadataParser: Metadata is not in the correct format");

            byte[] rawData = Convert.FromBase64String(accountData[0]);
            this.Uri = System.Text.Encoding.ASCII.GetString(GetByteRange(rawData, URI_INDEX, URI_LENGTH)).Replace("\0", String.Empty);
            this.UpdateAuthority = Base58Convert.Encode(GetByteRange(rawData, UPDATE_AUTH_INDEX, 32));
            this.Mint = Base58Convert.Encode(GetByteRange(rawData, MINT_INDEX, 32));
        }
        
        private byte[] GetByteRange(byte[] data, int start, int count) 
        {
            return data.ToList().GetRange(start, count).ToArray();
        }
    }
}