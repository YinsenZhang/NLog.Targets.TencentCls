using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace TestWebApi;

/// <summary>
///
/// </summary>
public class RequestLoggingFilter : IActionFilter
{
    private static string TRACE = "TraceId";
    private Stopwatch _stopwatch;//统计程序耗时
    private string _reqBody;
    private readonly ILogger<RequestLoggingFilter> _logger;

    public RequestLoggingFilter(ILogger<RequestLoggingFilter> logger)
    {
        _stopwatch = Stopwatch.StartNew();
        _logger = logger;
    }

    /// <inheritdoc/>
    public void OnActionExecuted(ActionExecutedContext context)
    {
        var setting = JsonConvert.DefaultSettings.Invoke();
        setting.NullValueHandling = NullValueHandling.Ignore;// 设置json序列化忽略空值
        _stopwatch.Stop();
        var request = context.HttpContext.Request;
        var response = context.HttpContext.Response;
        var traceId = request.Headers[TRACE].ToString();
        if (!response.Headers.ContainsKey(TRACE))
        {
            response.Headers[TRACE] = traceId;
        }
        var requestInfo = $"Request {request.Method} {request.Path} responded {response.StatusCode} in {_stopwatch.Elapsed.TotalMilliseconds} ms";

        // 读取Request
        if (request.HasFormContentType)
        {
            var files = JsonConvert.SerializeObject(request.Form.Files, setting);
            _reqBody = JsonConvert.SerializeObject(
            new
            {
                request.Form.Files,
                request.Form
            }, setting);
        }
        else
        {
            request.EnableBuffering();
            request.Body.Seek(0, SeekOrigin.Begin);
            using var m = new MemoryStream();
            request.Body.CopyTo(m);
            _reqBody = Encoding.UTF8.GetString(m.ToArray());
        }
        Dictionary<string, string> logDict = new Dictionary<string, string>()
        {
            {TRACE,traceId},
            {"Info", requestInfo},
            {"Request", _reqBody},
            //{"Response", JsonConvert.SerializeObject(context.Result)},
        };

        if (context.Exception != null)
        {
            logDict.Add("Exception", JsonConvert.SerializeObject(context.Result, setting));
            _logger.LogError(JsonConvert.SerializeObject(logDict, setting));
        }
        else
        {
            var resJson = string.Empty;
            // 文件流返回处理
            if (context.Result as FileStreamResult is not null)
            {
                var file = (FileStreamResult)context.Result;
                resJson = JsonConvert.SerializeObject(new
                {
                    ContentType = file.ContentType,
                    FileDownloadName = file.FileDownloadName,
                }, setting);
            }
            else
            {
                resJson = JsonConvert.SerializeObject(context.Result, setting);
            }
            logDict["Response"] = resJson;
            _logger.LogInformation(JsonConvert.SerializeObject(logDict, setting));
        }
    }

    /// <inheritdoc/>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;
        var response = context.HttpContext.Response;
        var traceId = request.Headers[TRACE].ToString();
        if (string.IsNullOrEmpty(traceId))
        {
            traceId = Guid.NewGuid().ToString("N");
            request.Headers.Add(TRACE, traceId);
        }
    }
}