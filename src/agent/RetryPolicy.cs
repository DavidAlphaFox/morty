// =============================================================================
// 重试策略
// =============================================================================
// API 调用失败时自动重试 (指数退避)
// 区分可重试/不可重试/上下文溢出错误
// =============================================================================

using System.Net;

namespace Morty.Agent;

/// <summary>
/// 错误类别
/// </summary>
public enum ErrorCategory
{
    /// <summary>可重试: rate limit, 服务过载, 网络超时</summary>
    Retryable,

    /// <summary>上下文溢出: 需触发压缩后重试</summary>
    ContextOverflow,

    /// <summary>不可重试: 认证失败, 无效请求</summary>
    Fatal
}

/// <summary>
/// 错误分类器
/// </summary>
public static class ErrorClassifier
{
    private static readonly string[] ContextOverflowPatterns =
    {
        "context_length_exceeded",
        "max_tokens",
        "too many tokens",
        "context window",
        "maximum context length",
        "token limit"
    };

    public static ErrorCategory Classify(Exception ex)
    {
        if (ex is HttpRequestException httpEx)
        {
            return httpEx.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => ErrorCategory.Retryable,     // 429
                HttpStatusCode.ServiceUnavailable => ErrorCategory.Retryable,  // 503
                HttpStatusCode.GatewayTimeout => ErrorCategory.Retryable,      // 504
                HttpStatusCode.BadGateway => ErrorCategory.Retryable,          // 502
                HttpStatusCode.RequestTimeout => ErrorCategory.Retryable,      // 408
                HttpStatusCode.Unauthorized => ErrorCategory.Fatal,            // 401
                HttpStatusCode.Forbidden => ErrorCategory.Fatal,               // 403
                _ => ErrorCategory.Fatal
            };
        }

        // 超时
        if (ex is TaskCanceledException or TimeoutException)
            return ErrorCategory.Retryable;

        // 上下文溢出
        var message = ex.Message;
        foreach (var pattern in ContextOverflowPatterns)
        {
            if (message.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return ErrorCategory.ContextOverflow;
        }

        return ErrorCategory.Fatal;
    }
}

/// <summary>
/// 重试策略
/// </summary>
public class RetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(2);
    public double BackoffMultiplier { get; set; } = 2.0;
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 计算第 n 次重试的等待时间
    /// </summary>
    public TimeSpan GetDelay(int attempt, TimeSpan? retryAfter = null)
    {
        if (retryAfter.HasValue)
            return retryAfter.Value;

        var delay = TimeSpan.FromMilliseconds(
            InitialDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, attempt));
        return delay > MaxDelay ? MaxDelay : delay;
    }
}
