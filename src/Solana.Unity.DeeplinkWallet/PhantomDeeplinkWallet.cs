using System;
using System.Collections.Specialized;
using System.Text;
using System.Web;
using base58;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Models;
using UnityEngine;
using UnityEngine.Networking;
using X25519;

namespace Solana.Unity.DeeplinkWallet
{
    // TODO: Add a transaction counter to be able to keep track across multiple transactions
    // TODO: App signing transaction without sending.
    // TODO: Improve deeplink url parsing
    public class DeeplinkWallet : IDeeplinkWallet
    {
        public event Action<string> OnDeepLinkTriggered;
        public event Action<IDeeplinkWallet.DeeplinkWalletConnectSuccess> OnDeeplinkWalletConnectionSuccess;
        public event Action<IDeeplinkWallet.DeeplinkWalletError> OnDeeplinkWalletError;
        public event Action<IDeeplinkWallet.DeeplinkWalletTransactionSuccessful> OnDeeplinkTransactionSuccessful;

        private string phantomWalletPublicKey;
        private string sessionId;

        private X25519KeyPair localKeyPairForPhantomConnection;
        private string base58PublicKey = "";
        private string phantomEncryptionPubKey;
        private string phantomApiVersion = "v1";
        private string appMetaDataUrl = "https://beavercrush.com";
        private string deeplinkUrlSceme = "SolPlay";

        private IRpcClient rpcClient;

        public void Init(string deeplinkUrlSceme, IRpcClient rpcClient, string appMetaDataUrl, string apiVersion = "v1")
        {
            phantomApiVersion = apiVersion;

            this.deeplinkUrlSceme = deeplinkUrlSceme;
            this.appMetaDataUrl = appMetaDataUrl;
            this.rpcClient = rpcClient;

            Application.deepLinkActivated += OnDeepLinkActivated;
            if (!String.IsNullOrEmpty(Application.absoluteURL))
            {
                OnDeepLinkActivated(Application.absoluteURL);
            }

            CreateEncryptionKeys();
        }

        public void Connect()
        {
            string appMetaDataUrl = this.appMetaDataUrl;
            string redirectUri = UnityWebRequest.EscapeURL($"{deeplinkUrlSceme}://onPhantomConnected");
            string url =
                $"https://phantom.app/ul/{phantomApiVersion}/connect?app_url={appMetaDataUrl}&dapp_encryption_public_key={base58PublicKey}&redirect_link={redirectUri}";

            Application.OpenURL(url);
        }

        public bool TryGetWalletPublicKey(out string phantomPublicKey)
        {
            if (!string.IsNullOrEmpty(phantomWalletPublicKey))
            {
                phantomPublicKey = phantomWalletPublicKey;
                return true;
            }

            phantomPublicKey = "";
            return false;
        }

        public bool TryGetSessionId(out string session)
        {
            if (!string.IsNullOrEmpty(sessionId))
            {
                session = sessionId;
                return true;
            }

            session = "";
            return false;
        }

        public string GetAppMetaDataUrl()
        {
            return appMetaDataUrl;
        }

        public async void SignAndSendTransaction(Transaction transaction)
        {
            var blockHash = await rpcClient.GetRecentBlockHashAsync();

            if (blockHash.Result == null)
            {
                OnDeeplinkWalletError?.Invoke(
                    new IDeeplinkWallet.DeeplinkWalletError("0", "Block hash null. Connected to internet?"));
                return;
            }

            string redirectUri = $"{deeplinkUrlSceme}://transactionSuccessful";

            byte[] serializedTransaction = transaction.Serialize();
            string base58Transaction = Base58.Encode(serializedTransaction);

            PhantomTransactionPayload transactionPayload = new PhantomTransactionPayload(base58Transaction, sessionId);
            string transactionPayloadJson = JsonUtility.ToJson(transactionPayload);

            byte[] bytesJson = Encoding.UTF8.GetBytes(transactionPayloadJson);

            byte[] randomNonce = new byte[24];
            TweetNaCl.TweetNaCl.RandomBytes(randomNonce);

            byte[] encryptedMessage = TweetNaCl.TweetNaCl.CryptoBox(bytesJson, randomNonce,
                Base58.Decode(phantomEncryptionPubKey), localKeyPairForPhantomConnection.PrivateKey);

            string base58Payload = Base58.Encode(encryptedMessage);

            string url =
                $"https://phantom.app/ul/v1/signAndSendTransaction?dapp_encryption_public_key={base58PublicKey}&redirect_link={redirectUri}&nonce={Base58.Encode(randomNonce)}&payload={base58Payload}";

            Application.OpenURL(url);
        }

        public void OpenUrlInWalletBrowser(string url)
        {
#if UNITY_EDITOR || UNITY_WEBGL
            string inWalletUrl = url;
#else
            string refUrl = UnityWebRequest.EscapeURL(GetAppMetaDataUrl());
            string escapedUrl = UnityWebRequest.EscapeURL(url);
            string inWalletUrl = $"https://phantom.app/ul/browse/{url}?ref=refUrl";
#endif
            Application.OpenURL(inWalletUrl);
        }

        private void OnDeepLinkActivated(string url)
        {
            if (url.Contains("transactionSuccessful"))
            {
                ParseSuccessfulTransaction(url);
                return;
            }

            ParseConnectionSuccessful(url);
        }

        private void ParseConnectionSuccessful(string url)
        {
            string phantomResponse = url.Split("?"[0])[1];

            NameValueCollection result = HttpUtility.ParseQueryString(phantomResponse);
            phantomEncryptionPubKey = result.Get("phantom_encryption_public_key");

            string phantomNonce = result.Get("nonce");
            string data = result.Get("data");
            string errorMessage = result.Get("errorMessage");

            if (!string.IsNullOrEmpty(errorMessage))
            {
                OnDeeplinkWalletError?.Invoke(new IDeeplinkWallet.DeeplinkWalletError("0", errorMessage));
                return;
            }

            if (string.IsNullOrEmpty(data))
            {
                OnDeeplinkWalletError?.Invoke(
                    new IDeeplinkWallet.DeeplinkWalletError("0", "Phantom connect canceled."));
                return;
            }

            byte[] uncryptedMessage = TweetNaCl.TweetNaCl.CryptoBoxOpen(Base58.Decode(data),
                Base58.Decode(phantomNonce), Base58.Decode(phantomEncryptionPubKey),
                localKeyPairForPhantomConnection.PrivateKey);

            string bytesToUtf8String = Encoding.UTF8.GetString(uncryptedMessage);

            PhantomWalletConnectSuccess connectSuccess =
                JsonUtility.FromJson<PhantomWalletConnectSuccess>(bytesToUtf8String);
            PhantomWalletError error = JsonUtility.FromJson<PhantomWalletError>(bytesToUtf8String);

            if (!string.IsNullOrEmpty(connectSuccess.public_key))
            {
                phantomWalletPublicKey = connectSuccess.public_key;
                sessionId = connectSuccess.session;
                OnDeeplinkWalletConnectionSuccess?.Invoke(
                    new IDeeplinkWallet.DeeplinkWalletConnectSuccess(connectSuccess.public_key,
                        connectSuccess.session));
            }
            else
            {
                if (!string.IsNullOrEmpty(error.errorCode))
                {
                    OnDeeplinkWalletError?.Invoke(
                        new IDeeplinkWallet.DeeplinkWalletError(error.errorCode, error.errorMessage));
                }
            }
        }

        private void ParseSuccessfulTransaction(string url)
        {
            string phantomResponse = url.Split("?"[0])[1];

            NameValueCollection result = HttpUtility.ParseQueryString(phantomResponse);
            string nonce = result.Get("nonce");
            string data = result.Get("data");
            string errorMessage = result.Get("errorMessage");

            if (!string.IsNullOrEmpty(errorMessage))
            {
                OnDeeplinkWalletError?.Invoke(
                    new IDeeplinkWallet.DeeplinkWalletError("0", $"Error: {errorMessage} + Data: {data}"));
                return;
            }

            byte[] uncryptedMessage = TweetNaCl.TweetNaCl.CryptoBoxOpen(Base58.Decode(data), Base58.Decode(nonce),
                Base58.Decode(phantomEncryptionPubKey), localKeyPairForPhantomConnection.PrivateKey);
            string bytesToUtf8String = Encoding.UTF8.GetString(uncryptedMessage);

            PhantomWalletTransactionSuccessful success =
                JsonUtility.FromJson<PhantomWalletTransactionSuccessful>(bytesToUtf8String);

            OnDeeplinkTransactionSuccessful?.Invoke(
                new IDeeplinkWallet.DeeplinkWalletTransactionSuccessful(success.signature));
        }

        private void CreateEncryptionKeys()
        {
            localKeyPairForPhantomConnection = X25519KeyAgreement.GenerateKeyPair();
            base58PublicKey = Base58.Encode(localKeyPairForPhantomConnection.PublicKey);
        }

        [Serializable]
        private class PhantomTransactionPayload
        {
            public string transaction;
            public string session;

            public PhantomTransactionPayload(string transaction, string session)
            {
                this.transaction = transaction;
                this.session = session;
            }
        }

        [Serializable]
        public class PhantomWalletError
        {
            public string errorCode;
            public string errorMessage;
        }

        [Serializable]
        public class PhantomWalletConnectSuccess
        {
            public string public_key;
            public string session;
        }

        [Serializable]
        public class PhantomWalletTransactionSuccessful
        {
            public string signature;
        }
    }
}