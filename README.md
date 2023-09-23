# NLog.Targets.TencentCls
腾讯日志服务(TencentCls)结合Nlog，配置NlogTarget 实现日志自动上传到CLs



##
TencentCloudSDK.Cls未实现日志上传功能，通过扩展方法实现UploadLog方法

TestCls/Program.Main()
```java
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

            var res = client.CallOctetStream("UploadLog", Service, headers, body).Result;
