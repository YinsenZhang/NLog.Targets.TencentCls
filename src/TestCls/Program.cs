using Google.Protobuf.WellKnownTypes;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TencentCloud.Cls.V20201016;
using TencentCloud.Cls.V20201016.Models;
using Cls;

namespace TestCls
{
    public static class Program
    {
        private const string SECRET_ID = "XXXX";
        private const string SECRET_KEY = "XXXX";
        private const string region = "ap-shanghai";
        private const string topicId = "8e8edc72-bf58-4d8e-8240-892494981266";
        public static  String X_CLS_TOPIC_ID = "X-CLS-TopicId";
        public static  String X_CLS_HASH_KEY = "X-CLS-HashKey";
        public static  String X_CLS_COMPRESS_TYPE = "X-CLS-CompressType";
        public static  String LZ_4 = "lz4";

        public static  String Service = "cls";
        public static  String UPLOAD_LOG_URL = "UploadLog";
        public static void Main1(string[] args)
        {
            ClsClient client = new ClsClient(new TencentCloud.Common.Credential { SecretId = SECRET_ID, SecretKey = SECRET_KEY }, region);

            var headers = new Dictionary<String, String>
            {
                { X_CLS_TOPIC_ID, topicId },
                { X_CLS_HASH_KEY, "" }
            };
            // body lz4 压缩
            //headers.Add(X_CLS_COMPRESS_TYPE, LZ_4);
            
            Log log = new Log();
            var content = new Log.Types.Content{ Key = "reqId", Value = "88888888888888" };
            log.Contents.Add(content);
            log.Time = DateTime.UtcNow.ToTimestamp().Seconds;

            LogGroup logGroup = new LogGroup();
            logGroup.Logs.Add(log);

            LogGroupList logGroupList = new LogGroupList();
            logGroupList.LogGroupList_.Add(logGroup);
            var body = logGroupList.ToByteArray();

            var res = client.CallOctetStream(UPLOAD_LOG_URL, Service, headers, body).Result;
        }
    }
}
