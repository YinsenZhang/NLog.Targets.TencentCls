// See https://aka.ms/new-console-template for more information

using Cls;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using TestCls;

public static class Program11
{
    private const  string _endpoint = "ap-shanghai.cls.tencentcs.com";
    private const string _version = "2020-10-16";
    private const string SECRET_ID = "XXXX";
    private const string SECRET_KEY = "XXXX";
    public static void Main11(string[]  args)
    {
        HttpClient client = new HttpClient();
        HttpRequestMessage request = new HttpRequestMessage();
        request.Method = HttpMethod.Post;
        //request.Headers.Add("Content-Type", "application/x-protobuf");
        //client.DefaultRequestHeaders.Add("Content-Type", "application/x-protobuf");
        request.RequestUri = new Uri("https://ap-shanghai.cls.tencentcs.com" + "/structuredlog?topic_id=8e8edc72-bf58-4d8e-8240-892494981266");


        Cls.LogGroupList logGroupList = new Cls.LogGroupList();
        LogGroup logGroup = new LogGroup();
        Log log = new Log();
        Log.Types.Content content = new Log.Types.Content() { Key="test",Value="testValue"};
        log.Contents.Add(content);
        log.Time = DateTime.UtcNow.ToTimestamp().Seconds;
        logGroup.Logs.Add(log);
        logGroupList.LogGroupList_.Add(logGroup);
        var s = logGroupList.ToByteString();
        request.Content = new ByteArrayContent(logGroupList.ToByteArray());
        request.Content.Headers.Add("Content-Type", "application/x-protobuf");
        var hh= GetSign(logGroupList.ToByteArray());
        foreach (var x in hh)
        {
            request.Headers.TryAddWithoutValidation(x.Key, x.Value);
        }
        //client.BaseAddress = new Uri("https://ap-shanghai.cls.tencentcs.com");
        var res = client.Send(request);
        StreamReader sr = new StreamReader(res.Content.ReadAsStream(), Encoding.UTF8);
        var result = sr.ReadToEnd().Trim();
        sr.Close();

    }
    public static string SHA256Hex(string s)
    {
        using (SHA256 algo = SHA256.Create())
        {
            byte[] hashbytes = algo.ComputeHash(Encoding.UTF8.GetBytes(s));
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < hashbytes.Length; ++i)
            {
                builder.Append(hashbytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }
    public static string SHA256Hex(byte[] hashbytes)
    {
        using (SHA256 algo = SHA256.Create())
        {
             //= algo.ComputeHash(Encoding.UTF8.GetBytes(s));
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < hashbytes.Length; ++i)
            {
                builder.Append(hashbytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }

    public static byte[] HmacSHA256(byte[] key, byte[] msg)
    {
        using (HMACSHA256 mac = new HMACSHA256(key))
        {
            return mac.ComputeHash(msg);
        }
    }

    public static Dictionary<String, String> BuildHeaders(string secretid,
        string secretkey, string service, string endpoint, string region,
        string action, string version, DateTime date, byte[] requestPayload)
    {
        string datestr = date.ToString("yyyy-MM-dd");
        DateTime startTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        long requestTimestamp = (long)Math.Round((date - startTime).TotalMilliseconds, MidpointRounding.AwayFromZero) / 1000;
        // ************* 步骤 1：拼接规范请求串 *************
        string algorithm = "TC3-HMAC-SHA256";
        string httpRequestMethod = "POST";
        string canonicalUri = "/";
        string canonicalQueryString = "";
        string contentType = "application/x-protobuf";
        string canonicalHeaders = "content-type:" + contentType + "\n"
            + "host:" + endpoint + "\n"
            + "x-tc-action:" + action.ToLower() + "\n";
        string signedHeaders = "content-type;host;x-tc-action";
        string hashedRequestPayload = SHA256Hex(requestPayload);
        string canonicalRequest = httpRequestMethod + "\n"
            + canonicalUri + "\n"
            + canonicalQueryString + "\n"
            + canonicalHeaders + "\n"
            + signedHeaders + "\n"
            + hashedRequestPayload;
        Console.WriteLine(canonicalRequest);

        // ************* 步骤 2：拼接待签名字符串 *************
        string credentialScope = datestr + "/" + service + "/" + "tc3_request";
        string hashedCanonicalRequest = SHA256Hex(canonicalRequest);
        string stringToSign = algorithm + "\n"
            + requestTimestamp.ToString() + "\n"
            + credentialScope + "\n"
            + hashedCanonicalRequest;
        Console.WriteLine(stringToSign);

        // ************* 步骤 3：计算签名 *************
        byte[] tc3SecretKey = Encoding.UTF8.GetBytes("TC3" + secretkey);
        byte[] secretDate = HmacSHA256(tc3SecretKey, Encoding.UTF8.GetBytes(datestr));
        byte[] secretService = HmacSHA256(secretDate, Encoding.UTF8.GetBytes(service));
        byte[] secretSigning = HmacSHA256(secretService, Encoding.UTF8.GetBytes("tc3_request"));
        byte[] signatureBytes = HmacSHA256(secretSigning, Encoding.UTF8.GetBytes(stringToSign));
        string signature = BitConverter.ToString(signatureBytes).Replace("-", "").ToLower();
        Console.WriteLine(signature);

        // ************* 步骤 4：拼接 Authorization *************
        string authorization = algorithm + " "
            + "Credential=" + secretid + "/" + credentialScope + ", "
            + "SignedHeaders=" + signedHeaders + ", "
            + "Signature=" + signature;
        Console.WriteLine(authorization);

        Dictionary<string, string> headers = new Dictionary<string, string>();
        headers.Add("Authorization", authorization);
        headers.Add("Host", endpoint);
        headers.Add("Content-Type", contentType);
        headers.Add("X-TC-Timestamp", requestTimestamp.ToString());
        headers.Add("X-TC-Version", version);
        headers.Add("X-TC-Action", action);
        headers.Add("X-TC-Region", region);
        return headers;
    }
  
    public static Dictionary<string, string> GetSign(byte[] requestPayload)
    {

        string service = "cls";
        string endpoint = _endpoint;
        string region = "ap-shanghai";
        string action = "structuredlog";
        string version = _version;

        // 此处由于示例规范的原因，采用时间戳2019-02-26 00:44:25，此参数作为示例，如果在项目中，您应当使用：
        // DateTime date = DateTime.UtcNow;
        // 注意时区，建议此时间统一采用UTC时间戳，否则容易出错
        DateTime date = DateTime.UtcNow;
        //string requestPayload = "{\"Limit\": 1, \"Filters\": [{\"Values\": [\"\\u672a\\u547d\\u540d\"], \"Name\": \"instance-name\"}]}";

        Dictionary<string, string> headers = BuildHeaders(SECRET_ID, SECRET_KEY, service
            , endpoint, region, action, version, date, requestPayload);

        //Console.WriteLine("POST https://cvm.tencentcloudapi.com");
        //foreach (KeyValuePair<string, string> kv in headers)
        //{
        //    Console.WriteLine(kv.Key + ": " + kv.Value);
        //}
        //Console.WriteLine();
        //Console.WriteLine(requestPayload);

        return headers;
    }
}