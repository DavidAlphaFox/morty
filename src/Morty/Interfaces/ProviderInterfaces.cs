namespace Morty.Interfaces;

/// <summary>
/// Claude 提供者接口
/// </summary>
public interface IClaudeProvider
{
    string Name { get; }
    string Type { get; }
    Task<ProviderResponse> SendMessageAsync(ProviderRequest request, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamMessageAsync(ProviderRequest request, CancellationToken ct = default);
}

/// <summary>
/// 提供者请求
/// </summary>
public record ProviderRequest(
    string Message,
    string? SystemPrompt = null,
    Dictionary<string, object>? Parameters = null,
    bool UsePlanMode = false,
    string? WorkingDirectory = null,
    string? EnvironmentVariables = null
);

/// <summary>
/// 提供者响应
/// </summary>
public record ProviderResponse(
    string Content,
    string? Error,
    int? TokenUsage,
    decimal? CostUsd,
    bool Success
);
