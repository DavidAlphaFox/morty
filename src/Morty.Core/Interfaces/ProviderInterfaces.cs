namespace Morty.Core.Interfaces;

/// <summary>
/// Claude 供应商接口
/// </summary>
public interface IClaudeProvider
{
    /// <summary>供应商名称</summary>
    string Name { get; }
    /// <summary>供应商类型</summary>
    string Type { get; }
    /// <summary>发送消息</summary>
    Task<ProviderResponse> SendMessageAsync(ProviderRequest request, CancellationToken ct = default);
    /// <summary>流式发送消息</summary>
    IAsyncEnumerable<string> StreamMessageAsync(ProviderRequest request, CancellationToken ct = default);
}

/// <summary>
/// 供应商请求
/// </summary>
public record ProviderRequest(
    string Message,
    string? SystemPrompt = null,
    Dictionary<string, object>? Parameters = null
);

/// <summary>
/// 供应商响应
/// </summary>
public record ProviderResponse(
    string Content,
    string? Error,
    int? TokenUsage,
    decimal? CostUsd,
    bool Success
);

/// <summary>
/// Claude 供应商工厂接口
/// </summary>
public interface IClaudeProviderFactory
{
    /// <summary>根据名称获取供应商</summary>
    IClaudeProvider GetProvider(string name);
    /// <summary>根据类型获取供应商</summary>
    IClaudeProvider GetProviderByType(string type);
    /// <summary>获取默认供应商</summary>
    IClaudeProvider? GetDefaultProvider();
    /// <summary>根据计划类型获取供应商</summary>
    IClaudeProvider? GetProviderForPlanType(PlanUsageType planType);
    /// <summary>注册供应商</summary>
    void RegisterProvider(IClaudeProvider provider);
    /// <summary>设置默认供应商</summary>
    void SetDefaultProvider(string name);
}

/// <summary>
/// 计划使用类型
/// </summary>
public enum PlanUsageType
{
    /// <summary>规划阶段</summary>
    Planning,
    /// <summary>执行阶段</summary>
    Execution
}
