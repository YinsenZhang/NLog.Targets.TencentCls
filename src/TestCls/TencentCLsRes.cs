namespace TestCls;

public class TencentCLsRes
{
    public TencentCLsResponse Response { get; set; }
}

public class TencentCLsResponse
{
    /// <summary>
    /// TencentCLs 返回
    /// </summary>
    public string RequestId { get; set; }

    /// <summary>
    /// err 正确响应为空
    /// </summary>
    public TencentCLsResError Error { get; set; }
}

public class TencentCLsResError
{
    /// <summary>
    /// 例如：nvalidParameter.Content
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// 例如 parse pb failed RequestId:[e2dfb80b-fb46-4a51-868f-f0343a03bedf]
    /// </summary>
    public string Message { get; set; }
}