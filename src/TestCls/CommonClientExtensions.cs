using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using TencentCloud.Common;

namespace TestCls
{
    public static class CommonClientExtensions
    {
        private class HttpClientHolder
        {
            private static readonly ConcurrentDictionary<string, HttpClientHolder> httpclients = new ConcurrentDictionary<string, HttpClientHolder>();

            public static HttpClient GetClient(string proxy)
            {
                string key = string.IsNullOrEmpty(proxy) ? "" : proxy;
                HttpClientHolder result = httpclients.GetOrAdd(key, (k) =>
                {
                    return new HttpClientHolder(k);
                });
                TimeSpan timeSpan = DateTime.Now - result.createTime;

                // A new connection is created every 5 minutes
                // and old connections are discarded to avoid DNS flushing issues.
                while (timeSpan.TotalSeconds > 300)
                {
                    ICollection<KeyValuePair<string, HttpClientHolder>> kv = httpclients;
                    kv.Remove(new KeyValuePair<string, HttpClientHolder>(key, result));
                    result = httpclients.GetOrAdd(key, (k) =>
                    {
                        return new HttpClientHolder(k);
                    });
                    timeSpan = DateTime.Now - result.createTime;
                }
                return result.client;
            }

            public readonly HttpClient client;

            public readonly DateTime createTime;

            public HttpClientHolder(string proxy)
            {
                string p = string.IsNullOrEmpty(proxy) ? "" : proxy;
                if (p == "")
                {
                    this.client = new HttpClient();
                }
                else
                {
                    var httpClientHandler = new HttpClientHandler
                    {
                        Proxy = new WebProxy(proxy),
                    };

                    this.client = new HttpClient(handler: httpClientHandler, disposeHandler: true);
                }
                this.client.Timeout = TimeSpan.FromSeconds(60);
                this.createTime = DateTime.Now;
            }
        }

        public static async Task<TencentCLsRes> CallOctetStream(this AbstractClient client, String action, string service, Dictionary<String, String> headers, byte[] body)
        {
            var result = string.Empty;

            var clientHeaders = client.BuildHeaders();
            foreach (var kv in headers)
            {
                clientHeaders.TryAdd(kv.Key, kv.Value);
            }
            clientHeaders.Add("X-TC-Action", action);
            clientHeaders.Add("Content-Type", "application/octet-stream; charset=utf-8");
            clientHeaders.Add("Authorization", client.GetAuthorization(clientHeaders, service, body));
            var http = HttpClientHolder.GetClient("");//client.HttpClient;

            string url = $"{client.Profile.HttpProfile.Protocol}{client.Endpoint}";
            using CancellationTokenSource cts = new CancellationTokenSource(client.Profile.HttpProfile.Timeout * 1000);
            using HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Post, url);
            msg.Content = new ByteArrayContent(body);
            foreach (KeyValuePair<string, string> kvp in clientHeaders)
            {
                if (kvp.Key.Equals("Content-Type"))
                {
                    msg.Content.Headers.Remove(kvp.Key);
                    msg.Content.Headers.Add("Content-Type", kvp.Value);
                    //msg.Content = new StringContent(payload, Encoding.UTF8, kvp.Value);
                }
                else if (kvp.Key.Equals("Host"))
                {
                    msg.Headers.Host = kvp.Value;
                }
                else if (kvp.Key.Equals("Authorization"))
                {
                    msg.Headers.Authorization = new AuthenticationHeaderValue("TC3-HMAC-SHA256", kvp.Value.Substring("TC3-HMAC-SHA256".Length));
                }
                else
                {
                    msg.Headers.Add(kvp.Key, kvp.Value);
                }
            }

            var res = await http.SendAsync(msg, cts.Token);
            result = await res.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TencentCLsRes>(result);
        }

        private static Dictionary<string, string> BuildHeaders(this AbstractClient client)
        {

            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                //{ "Authorization", authorization },
                { "Host", client.Endpoint },
                //{ "Content-Type", " \"application/octet-stream; charset=utf-8" },
                { "X-TC-Timestamp", DateTime.UtcNow.ToTimestamp().Seconds.ToString() },
                { "X-TC-Version", client.ApiVersion },
                { "X-TC-Region", client.Region },
                { "X-TC-Language", "zh-CN"},
                //{ "X-TC-Action",_action},
                //{ "X-TC-RequestClient","SDK_PYTHON_3.0.983"},
                //{ "X-TC-RequestClient","SDK_NET_3.0.815"},
                //{ "X-TC-RequestClient","SDK_JAVA_3.1.867"},
            };
            return headers;
        }
        private static string GetAuthorization(this AbstractClient client, Dictionary<String, String> headers,string service, byte[] body)
        {
            string endpoint = client.Endpoint;
            string contentType = headers["Content-Type"];

            string httpRequestMethod = "POST";
            string canonicalURI = "/";
            String canonicalQueryString = "";
            string canonicalHeaders = "content-type:" + contentType + "\nhost:" + endpoint + "\n";
            string signedHeaders = "content-type;host";
            string hashedRequestPayload = SHA256Hex(body);
            string canonicalRequest = httpRequestMethod + "\n"
                + canonicalURI + "\n"
                + canonicalQueryString + "\n"
                + canonicalHeaders + "\n"
                + signedHeaders + "\n"
                + hashedRequestPayload;

            string algorithm = "TC3-HMAC-SHA256";
            //long timestamp = DateTime.UtcNow.ToTimestamp().Seconds;
            //string requestTimestamp = timestamp.ToString();
            string requestTimestamp = headers["X-TC-Timestamp"];
            long.TryParse(requestTimestamp, out long timestamp);
            string date = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp).ToString("yyyy-MM-dd");
            //string service = endpoint.Split('.')[0]; // client.Profile.HttpProfile;##########################
            string credentialScope = date + "/" + service + "/" + "tc3_request";
            string hashedCanonicalRequest = SignHelper.SHA256Hex(canonicalRequest);
            string stringToSign = algorithm + "\n"
                + requestTimestamp + "\n"
                + credentialScope + "\n"
                + hashedCanonicalRequest;

            byte[] tc3SecretKey = Encoding.UTF8.GetBytes("TC3" + client.Credential.SecretKey);
            byte[] secretDate = SignHelper.HmacSHA256(tc3SecretKey, Encoding.UTF8.GetBytes(date));
            byte[] secretService = SignHelper.HmacSHA256(secretDate, Encoding.UTF8.GetBytes(service));
            byte[] secretSigning = SignHelper.HmacSHA256(secretService, Encoding.UTF8.GetBytes("tc3_request"));
            byte[] signatureBytes = SignHelper.HmacSHA256(secretSigning, Encoding.UTF8.GetBytes(stringToSign));

            string signature = BitConverter.ToString(signatureBytes).Replace("-", "").ToLower();

            string authorization = algorithm + " "
                + "Credential=" + client.Credential.SecretId + "/" + credentialScope + ", "
                + "SignedHeaders=" + signedHeaders + ", "
                + "Signature=" + signature;

            return authorization;
        }
        private static string SHA256Hex(byte[] bytes)
        {
            //byte[] array = sHA.ComputeHash(Encoding.UTF8.GetBytes(s));
            using SHA256 sHA = SHA256.Create();
            byte[] array = sHA.ComputeHash(bytes);
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < array.Length; i++)
            {
                stringBuilder.Append(array[i].ToString("x2"));
            }
            return stringBuilder.ToString();
        }
    }
}
