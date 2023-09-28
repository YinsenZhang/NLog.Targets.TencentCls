using Cls;
using Google.Protobuf;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets.TencentCls;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TencentCloud.Cls.V20201016;
using TencentCloud.Common;

namespace NLog.Targets.TencentClsTarget
{
    /// <summary>
    /// NLog Target for writing to TencentCls using low level client
    /// </summary>
    [Target("TencentCls")]
    public class TencentClsTarget : TargetWithLayout
    {
        public static String X_CLS_TOPIC_ID = "X-CLS-TopicId";
        public static String X_CLS_HASH_KEY = "X-CLS-HashKey";
        public static String X_CLS_COMPRESS_TYPE = "X-CLS-CompressType";
        public static String LZ_4 = "lz4";

        public static String SERVICE = "cls";
        public static String UPLOAD_LOG_URL = "UploadLog";

        private ClsClient _client;
        private Layout _secretId;
        private Layout _secretKey;
        private Layout _region;
        private Layout _topicId;
        private Layout _action;
        private Layout _service;
        private Layout _path;
        private HashSet<string> _excludedProperties = new HashSet<string>(new[] { "CallerMemberName", "CallerFilePath", "CallerLineNumber", "MachineName", "ThreadId" });
        private JsonSerializer _jsonSerializer;
        private JsonSerializer _flatJsonSerializer;
        private readonly Lazy<JsonSerializerSettings> _jsonSerializerSettings;
        private readonly Lazy<JsonSerializerSettings> _flatSerializerSettings;

        private JsonSerializer JsonSerializer => _jsonSerializer ?? (_jsonSerializer = JsonSerializer.CreateDefault(_jsonSerializerSettings.Value));
        private JsonSerializer JsonSerializerFlat => _flatJsonSerializer ?? (_flatJsonSerializer = JsonSerializer.CreateDefault(_flatSerializerSettings.Value));

        /// <summary>
        /// Gets or sets whether to include all properties of the log event in the document
        /// </summary>
        public bool IncludeAllProperties { get; set; }

        /// <summary>
        /// Gets or sets a list of additional fields to add to the TencentCls document.
        /// </summary>
        [ArrayParameter(typeof(Field), "field")]
        public IList<Field> Fields { get; set; } = new List<Field>();

        /// <summary>
        /// Gets or sets a list of object types and their override of JsonConverter
        /// </summary>
        [ArrayParameter(typeof(ObjectTypeConvert), "typeconverter")]
        public IList<ObjectTypeConvert> ObjectTypeConverters { get; set; }

        /// <summary>
        /// Gets or sets if exceptions will be rethrown.
        ///
        /// Set it to true if TencentClsTarget target is used within FallbackGroup target (https://github.com/NLog/NLog/wiki/FallbackGroup-target).
        /// </summary>
        [Obsolete("No longer needed", true)]
        public bool ThrowExceptions { get; set; }

        /// <summary>
        /// Gets or sets whether it should perform safe object-reflection (-1 = Unsafe, 0 - No Reflection, 1 - Simple Reflection, 2 - Full Reflection)
        /// </summary>
        public int MaxRecursionLimit { get; set; } = -1;

        /// <summary>
        /// <inheritdoc cref="ITencentClsTarget.SecretId"/>
        /// </summary>
        [RequiredParameter]
        public string SecretId { get => (_secretId as SimpleLayout)?.Text; set => _secretId = value ?? string.Empty; }

        /// <summary>
        /// <inheritdoc cref="ITencentClsTarget.SecretKey"/>
        /// </summary>
        [RequiredParameter]
        public string SecretKey { get => (_secretKey as SimpleLayout)?.Text; set => _secretKey = value ?? string.Empty; }

        [RequiredParameter]
        public string Region { get => (_region as SimpleLayout)?.Text; set => _region = value ?? string.Empty; }

        [RequiredParameter]
        public string TopicId { get => (_topicId as SimpleLayout)?.Text; set => _topicId = value ?? string.Empty; }

        public string Action { get => _action == null ? UPLOAD_LOG_URL : (_action as SimpleLayout)?.Text; set => _action = value ?? string.Empty; }
        public string Service { get => _service == null ? SERVICE : (_service as SimpleLayout)?.Text; set => _service = value ?? string.Empty; }
        public string Path { get => _path == null ? string.Empty : (_path as SimpleLayout)?.Text; set => _path = value ?? string.Empty; }

        public bool IncludeDefaultFields { get; set; } = true;

        public TencentClsTarget()
        {
            Name = "TencentCls";
            OptimizeBufferReuse = true;

            ObjectTypeConverters = new List<ObjectTypeConvert>()
            {
                new ObjectTypeConvert(typeof(System.Reflection.Assembly)),     // Skip serializing all types in application
                new ObjectTypeConvert(typeof(System.Reflection.Module)),       // Skip serializing all types in application
                new ObjectTypeConvert(typeof(System.Reflection.MemberInfo)),   // Skip serializing all types in application
                new ObjectTypeConvert(typeof(System.IO.Stream)),               // Skip serializing Stream properties, since they throw
                new ObjectTypeConvert(typeof(System.Net.IPAddress)),           // Skip serializing IPAdress properties, since they throw when IPv6 address
            };

            _jsonSerializerSettings = new Lazy<JsonSerializerSettings>(() => CreateJsonSerializerSettings(false, ObjectTypeConverters), LazyThreadSafetyMode.PublicationOnly);
            _flatSerializerSettings = new Lazy<JsonSerializerSettings>(() => CreateJsonSerializerSettings(true, ObjectTypeConverters), LazyThreadSafetyMode.PublicationOnly);
        }

        /// <inheritdoc />
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            var eventInfo = LogEventInfo.CreateNullEvent();
            var secretId = _secretId?.Render(eventInfo) ?? string.Empty;
            var secretKey = _secretKey?.Render(eventInfo) ?? string.Empty;
            var region = _region?.Render(eventInfo) ?? string.Empty;

            var cred = new Credential
            {
                SecretId = secretId,
                SecretKey = secretKey
            };

            _client = new ClsClient(cred, region);
        }

        /// <inheritdoc />
        protected override async void Write(AsyncLogEventInfo logEvent)
        {
            await SendBatch(new[] { logEvent });
        }

        /// <inheritdoc />
        protected override async void Write(IList<AsyncLogEventInfo> logEvents)
        {
            await SendBatch(logEvents);
        }

        private async Task<bool> SendBatch(ICollection<AsyncLogEventInfo> logEvents)
        {
            try
            {
                //var payload = EnableJsonLayout ? FromPayloadWithJsonLayout(logEvents) : FormPayload(logEvents);
                var payload = FormPayload(logEvents);
                var headers = new Dictionary<String, String>
                {
                    { X_CLS_TOPIC_ID, TopicId },
                    { X_CLS_HASH_KEY, "" }
                };
                // ConfigureAwait 放弃使用上下文
                var result = await _client.CallOctetStream(Action, Service, headers, payload).ConfigureAwait(false);
                if (result == null || result.Response == null)
                {
                    InternalLogger.Error($"TencentCls: Server error: Response is null");
                }
                else if (result.Response.Error != null)
                {
                    InternalLogger.Error($"TencentCls: Server error: Code:{result.Response.Error.Code} Message: {result.Response.Error.Message}");
                }
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex.FlattenToActualException(), "TencentCls: Error while sending log messages");
                foreach (var ev in logEvents)
                {
                    ev.Continuation(ex);
                }
                return false;
            }
            return true;
        }

        private byte[] FormPayload(ICollection<AsyncLogEventInfo> logEvents)
        {
            LogGroup logGroup = new LogGroup();

            foreach (var ev in logEvents)
            {
                Log log = new Log();
                var logEvent = ev.LogEvent;

                var document = GenerateDocumentProperties(logEvent);
                foreach (var item in document)
                {
                    if (item.Key == "message")
                    {
                        var msgDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(item.Value.ToString());
                        var strMsg = msgDict["message"];
                        if (strMsg.StartsWith("{") && strMsg.EndsWith("}"))
                        {
                            var jsonMsg = JObject.Parse(strMsg);
                            msgDict.Remove("message");
                            foreach (var item2 in jsonMsg)
                            {
                                msgDict[item2.Key] = item2.Value.ToString();
                            }
                        }
                        foreach (var kv in msgDict)
                        {
                            var content = new Log.Types.Content() { Key = kv.Key, Value = kv.Value };
                            log.Contents.Add(content);
                        }
                    }
                    else if (item.Key == "@timestamp")
                    {
                        long unixTime = ((DateTimeOffset)logEvent.TimeStamp).ToUnixTimeSeconds();
                        log.Time = unixTime;
                        //var content = new Log.Types.Content() { Key = "Time", Value = ""+ unixTime };
                        //log.Contents.Add(content);
                    }
                    else
                    {
                        var content = new Log.Types.Content() { Key = item.Key, Value = item.Value.ToString() };
                        log.Contents.Add(content);
                    }
                }
                //log.Contents.Capacity = log.Contents.Count;
                logGroup.Logs.Add(log);
            }
            //logGroup.Logs.Capacity = logGroup.Logs.Count;
            LogGroupList logGroupList = new LogGroupList();
            logGroupList.LogGroupList_.Add(logGroup);
            //logGroupList.LogGroupList_.Capacity = logGroupList.LogGroupList_.Count;
            return logGroupList.ToByteArray();
        }

        private Dictionary<string, object> GenerateDocumentProperties(LogEventInfo logEvent)
        {
            var document = new Dictionary<string, object>();

            if (IncludeDefaultFields)
            {
                document.Add("@timestamp", logEvent.TimeStamp);
                document.Add("level", logEvent.Level.Name);
                document.Add("message", RenderLogEvent(Layout, logEvent));
            }

            foreach (var field in Fields)
            {
                var renderedField = RenderLogEvent(field.Layout, logEvent);

                if (string.IsNullOrWhiteSpace(renderedField))
                    continue;

                try
                {
                    document[field.Name] = renderedField.ToSystemType(field.LayoutType, logEvent.FormatProvider, JsonSerializer);
                }
                catch (Exception ex)
                {
                    _jsonSerializer = null; // Reset as it might now be in bad state
                    InternalLogger.Warn(ex, "TencentCls: Error while formatting field: {0}", field.Name);
                }
            }

            if (IncludeDefaultFields)
            {
                if (logEvent.Exception != null && !document.ContainsKey("exception"))
                {
                    document.Add("exception", FormatValueSafe(logEvent.Exception, "exception"));
                }
            }

            if (IncludeAllProperties && logEvent.HasProperties)
            {
                foreach (var p in logEvent.Properties)
                {
                    var propertyKey = p.Key.ToString();
                    if (_excludedProperties.Contains(propertyKey))
                        continue;

                    if (document.ContainsKey(propertyKey))
                    {
                        propertyKey += "_1";
                        if (document.ContainsKey(propertyKey))
                            continue;
                    }

                    document[propertyKey] = FormatValueSafe(p.Value, propertyKey);
                }
            }

            return document;
        }

        private object FormatValueSafe(object value, string propertyName)
        {
            try
            {
                var jsonSerializer = (MaxRecursionLimit == 0 || MaxRecursionLimit == 1) ? JsonSerializerFlat : JsonSerializer;
                return ObjectConverter.FormatValueSafe(value, MaxRecursionLimit, jsonSerializer);
            }
            catch (Exception ex)
            {
                _jsonSerializer = null; // Reset as it might now be in bad state
                _flatJsonSerializer = null;
                InternalLogger.Debug(ex, "TencentCls: Error while formatting property: {0}", propertyName);
                return null;
            }
        }

        private static JsonSerializerSettings CreateJsonSerializerSettings(bool specialPropertyResolver, IList<ObjectTypeConvert> objectTypeConverters)
        {
            var jsonSerializerSettings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, CheckAdditionalContent = true };
            jsonSerializerSettings.Converters.Add(new StringEnumConverter());
            foreach (var typeConverter in objectTypeConverters ?? Array.Empty<ObjectTypeConvert>())
            {
                var jsonConverter = typeConverter.JsonConverter;
                if (jsonConverter != null)
                    jsonSerializerSettings.Converters.Add(jsonConverter);
                else
                    InternalLogger.Debug("TencentCls: TypeConverter for {0} has no JsonConverter", typeConverter.ObjectType);
            }
            jsonSerializerSettings.Error = (sender, args) =>
            {
                InternalLogger.Debug(args.ErrorContext.Error, $"TencentCls: Error serializing exception property '{args.ErrorContext.Member}', property ignored");
                args.ErrorContext.Handled = true;
            };
            if (specialPropertyResolver)
            {
                jsonSerializerSettings.ContractResolver = new FlatObjectContractResolver();
            }
            return jsonSerializerSettings;
        }
    }
}