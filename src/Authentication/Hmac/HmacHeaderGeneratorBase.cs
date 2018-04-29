using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using StandardDot.CoreExtensions;

namespace StandardDot.Authentication.Hmac
{
    /// <summary>
    /// The base for an HMAC authentication generator. The scheme, app id, and secret key can be passed in or overridden.
    /// </summary>
    public abstract class HmacHeaderGeneratorBase
    {
        public HmacHeaderGeneratorBase()
        {}

        /// <param name="customHeaderScheme">The header scheme that will be used for generating headers</param>
        public HmacHeaderGeneratorBase(string customHeaderScheme)
        {
            CustomHeaderScheme = customHeaderScheme;
        }

        /// <param name="customHeaderScheme">The header scheme that will be used for generating headers</param>
        /// <param name="appId">The appid that will be used to generate headers</param>
        /// <param name="secretKey">The secret key that will be used to generate headers</param>
        public HmacHeaderGeneratorBase(string customHeaderScheme, string appId, string secretKey)
            : this(customHeaderScheme)
        {
            AppId = appId;
            SecretKey = secretKey;
        }

        // Obtained from Provider, SecretKey MUST be stored securely
        protected virtual string AppId { get; }

        // Override this with a secure way to get the secret key
        protected virtual string SecretKey { get; }

        // Override this with the scheme being used
        protected virtual string CustomHeaderScheme { get; }

        /// <summary>
        /// Adds the HMAC Authentication headers to the client
        /// </summary>
        /// <param name="client">The client that will be used to make the request</param>
        /// <param name="requestUri">The Uri that the request will be sent to</param>
        /// <param name="method">The method that the request will be used</param>
        /// <param name="content">The content of the request</param>
        public virtual void AddHmacHeaders(HttpClient client, Uri requestUri, HttpMethod method, string content = null)
        {
            string requestContentBase64String = string.Empty;

            string requestUriString = System.Web.HttpUtility.UrlEncode(requestUri.AbsoluteUri.ToLower());

            string requestHttpMethod = method.Method;

            // Calculate UNIX time
            DateTime epochStart = new DateTime(1970, 01, 01, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan timeSpan = DateTime.UtcNow - epochStart;
            string requestTimeStamp = Convert.ToUInt64(timeSpan.TotalSeconds).ToString();

            // Create random nonce for each request
            string nonce = Guid.NewGuid().ToString("N");

            // Checking if the request contains body, usually will be null with HTTP GET and DELETE
            if (content != null)
            {
                MD5 md5 = MD5.Create();
                // Hashing the request body, any change in request body will result in different hash, we'll incure message integrity
                byte[] requestContentHash;
                using (Stream contentStream = content.GetStreamFromString())
                {
                    requestContentHash = md5.ComputeHash(contentStream);
                }
                requestContentBase64String = Convert.ToBase64String(requestContentHash);
            }

            // Creating the raw signature string
            string signatureRawData =
                $"{AppId}{requestHttpMethod}{requestUriString}{requestTimeStamp}{nonce}{requestContentBase64String}";

            byte[] secretKeyByteArray = Convert.FromBase64String(SecretKey);

            byte[] signature = Encoding.UTF8.GetBytes(signatureRawData);

            using (HMACSHA256 hmac = new HMACSHA256(secretKeyByteArray))
            {
                byte[] signatureBytes = hmac.ComputeHash(signature);
                string requestSignatureBase64String = Convert.ToBase64String(signatureBytes);
                // Setting the values in the Authorization header using custom scheme (sds)
                client.DefaultRequestHeaders.Add("Authorization","sds " +
                    $"{AppId}:{requestSignatureBase64String}:{nonce}:{requestTimeStamp}");
            }
        }
    }
}