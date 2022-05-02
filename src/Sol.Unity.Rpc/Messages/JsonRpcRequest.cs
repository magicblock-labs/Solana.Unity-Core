using System.Collections.Generic;

namespace Sol.Unity.Rpc.Messages
{
    /// <summary>
    /// Rpc request message.
    /// </summary>
    public class JsonRpcRequest : JsonRpcBase
    {
        /// <summary>
        /// The request method.
        /// </summary>
        public string Method { get; }

        /// <summary>
        /// The method parameters list.
        /// </summary>
        public IList<object> Params { get; }

        
        /// <summary>
        /// Serialize params only if not null
        /// </summary>
        /// <returns></returns>
        public bool ShouldSerializeParams()
        {
            return Params != null;
        }
        internal JsonRpcRequest(int id, string method, IList<object> parameters)
        {
            Params = parameters;
            Method = method;
            Id = id;
            Jsonrpc = "2.0";
        }
    }
}