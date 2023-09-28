// See https://aka.ms/new-console-template for more information

using Cls;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using TencentCloud.Common;

public static class StructuredlogTest
{
    private const string _endpoint = "ap-shanghai.cls.tencentcs.com";
    private const string _version = "2020-10-16";
    private const string SECRET_ID = "XXXX";
    private const string SECRET_KEY = "XXXX";

    private const string _service = "cls";
    private const string _region = "ap-shanghai";
    private const string _topicId = "8e8edc72-bf58-4d8e-8240-892494981266";
    private const string _action = "StructuredLog";
    private const string _contentType = "application/x-protobuf";

    public static void Main(string[] args)
    {
        HttpClient client = new HttpClient();
        HttpRequestMessage request = new HttpRequestMessage();
        request.Method = HttpMethod.Post;
        //request.Headers.Add("Content-Type", "application/x-protobuf");
        //client.DefaultRequestHeaders.Add("Content-Type", "application/x-protobuf");
        string queryString = $"topic_id={_topicId}";
        string urlPath = $"/{_action}";
        request.RequestUri = new Uri($"https://{_endpoint}{urlPath.ToLower()}?{queryString}");

        Cls.LogGroupList logGroupList = new Cls.LogGroupList();
        LogGroup logGroup = new LogGroup();
        Log log = new Log();
        Log.Types.Content content = new Log.Types.Content() { Key = "reqId", Value = "88888888888888" };
        log.Contents.Add(content);
        log.Time = DateTime.UtcNow.ToTimestamp().Seconds;
        logGroup.Logs.Add(log);
        logGroupList.LogGroupList_.Add(logGroup);

        var body = logGroupList.ToByteArray();
        request.Content = new ByteArrayContent(body, 0, body.Length);

        var hh = BuildHeaders(_contentType, body, $"{urlPath.ToLower()}?{queryString}", queryString);
        //var hh= BuildHeaders(_contentType, body, urlPath, queryString);
        foreach (var kvp in hh)
        {
            if (kvp.Key.Equals("Content-Type"))
            {
                request.Content.Headers.Remove(kvp.Key);
                request.Content.Headers.Add("Content-Type", _contentType);
                //msg.Content = new StringContent(payload, Encoding.UTF8, kvp.Value);
            }
            else if (kvp.Key.Equals("Host"))
            {
                request.Headers.Host = kvp.Value;
            }
            else if (kvp.Key.Equals("Authorization"))
            {
                //request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                request.Headers.Authorization = new AuthenticationHeaderValue("TC3-HMAC-SHA256", kvp.Value.Substring("TC3-HMAC-SHA256".Length));
            }
            else
            {
                request.Headers.Add(kvp.Key, kvp.Value);
            }
        }
        //request.Headers.Add("X-CLS-TopicId", "8e8edc72-bf58-4d8e-8240-892494981266");
        //request.Headers.Add("X-TC-Action", "UploadLog");
        //client.BaseAddress = new Uri(_endpoint);
        var res = client.Send(request);
        StreamReader sr = new StreamReader(res.Content.ReadAsStream(), Encoding.UTF8);
        //var result = sr.ReadToEnd().Trim();
        var result = res.Content.ReadAsStringAsync().Result;
        sr.Close();
    }

    private static Dictionary<string, string> BuildHeaders(string contentType, byte[] body, string canonicalURI = "/", string canonicalQueryString = "")
    {
        string endpoint = _endpoint;
        //if (!string.IsNullOrEmpty(this.Profile.HttpProfile.Endpoint))
        //{
        //    endpoint = this.Profile.HttpProfile.Endpoint;
        //}
        string httpRequestMethod = "POST";
        //string canonicalURI = "/";
        string canonicalHeaders = "content-type:" + contentType + "\nhost:" + endpoint + "\n";
        string signedHeaders = "content-type;host";
        //string hashedRequestPayload = SignHelper.SHA256Hex(requestPayload);
        string hashedRequestPayload = SHA256Hex(body);
        string canonicalRequest = httpRequestMethod + "\n"
            + canonicalURI + "\n"
            + canonicalQueryString + "\n"
            + canonicalHeaders + "\n"
            + signedHeaders + "\n"
            + hashedRequestPayload;
        Console.WriteLine("############### canonicalRequest ##################");
        Console.WriteLine(canonicalRequest);

        string algorithm = "TC3-HMAC-SHA256";
        long timestamp = DateTime.UtcNow.ToTimestamp().Seconds;
        string requestTimestamp = timestamp.ToString();
        string date = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp).ToString("yyyy-MM-dd");
        string service = _service;
        string credentialScope = date + "/" + service + "/" + "tc3_request";
        string hashedCanonicalRequest = SignHelper.SHA256Hex(canonicalRequest);
        string stringToSign = algorithm + "\n"
            + requestTimestamp + "\n"
            + credentialScope + "\n"
            + hashedCanonicalRequest;
        Console.WriteLine("############### Sign ##################");
        Console.WriteLine(stringToSign);

        byte[] tc3SecretKey = Encoding.UTF8.GetBytes("TC3" + SECRET_KEY);
        byte[] secretDate = SignHelper.HmacSHA256(tc3SecretKey, Encoding.UTF8.GetBytes(date));
        byte[] secretService = SignHelper.HmacSHA256(secretDate, Encoding.UTF8.GetBytes(service));
        byte[] secretSigning = SignHelper.HmacSHA256(secretService, Encoding.UTF8.GetBytes("tc3_request"));
        byte[] signatureBytes = SignHelper.HmacSHA256(secretSigning, Encoding.UTF8.GetBytes(stringToSign));

        string signature = BitConverter.ToString(signatureBytes).Replace("-", "").ToLower();
        //string signature = SignHelper(SECRET_KEY,);

        string authorization = algorithm + " "
            + "Credential=" + SECRET_ID + "/" + credentialScope + ", "
            + "SignedHeaders=" + signedHeaders + ", "
            + "Signature=" + signature;
        Console.WriteLine("############### authorization ##################");
        Console.WriteLine(authorization);
        Dictionary<string, string> headers = new Dictionary<string, string>
        {
            { "Authorization", authorization },
            { "Host", endpoint },
            { "Content-Type", contentType },
            //{ "X-TC-Timestamp", requestTimestamp },
            //{ "X-TC-Version", _version },
            //{ "X-TC-Region", _region },
            //{ "X-CLS-HashKey",""},
            //{ "X-CLS-CompressType",""},
            //{ "X-TC-Action",_action},
            //{ "X-TC-RequestClient","SDK_PYTHON_3.0.983"},
            //{ "X-TC-RequestClient","SDK_NET_3.0.815"},
            //{ "X-TC-RequestClient","SDK_JAVA_3.1.867"},
        };
        //headers.Add("X-TC-RequestClient", this.SdkVersion);
        //if (!string.IsNullOrEmpty(this.Credential.Token))
        //{
        //    headers.Add("X-TC-Token", this.Credential.Token);
        //}
        //if (this.Profile.Language == Language.EN_US)
        //{
        //    headers.Add("X-TC-Language", "en-US");
        //}
        //else
        {
            //headers.Add("X-TC-Language", "zh-CN");
        }
        return headers;
    }

    private static long ToTimestamp()
    {
#if NET45
            DateTime startTime = new System.DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            DateTime nowTime = DateTime.UtcNow;
            long unixTime = (long)Math.Round((nowTime - startTime).TotalMilliseconds, MidpointRounding.AwayFromZero);
            return unixTime;
#endif

        DateTimeOffset expiresAtOffset = DateTimeOffset.Now;
        var totalSeconds = expiresAtOffset.ToUnixTimeMilliseconds();
        return totalSeconds;
    }

    public static string SHA256Hex(byte[] bytes)
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