# 任务 1.3: 重试与错误恢复

## 阶段
Phase 1 — 核心体验提升

## 目标
API 调用失败时自动重试（指数退避），区分可重试/不可重试错误，上下文溢出时自动触发压缩。

## 背景
当前 `CodingAgent.PromptAsync` 中 LLM 调用失败直接抛出异常到 CLI 层，用户看到错误后只能手动重试。opencode 有完善的重试机制：指数退避、retry-after 头、上下文溢出自动压缩。

## 设计方案

### 1. 错误分类

```csharp
// src/agent/RetryPolicy.cs

public enum ErrorCategory
{
    /// <summary>可重试: rate limit, 服务过载, 网络超时</summary>
    Retryable,

    /// <summary>上下文溢出: 需触发压缩后重试</summary>
    ContextOverflow,

    /// <summary>不可重试: 认证失败, 无效请求</summary>
    Fatal
}

public static class ErrorClassifier
{
    public static ErrorCategory Classify(Exception ex)
    {
        // HTTP 状态码分类
        if (ex is HttpRequestException httpEx)
        {
            return httpEx.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => ErrorCategory.Retryable,     // 429
                HttpStatusCode.ServiceUnavailable => ErrorCategory.Retryable,  // 503
                HttpStatusCode.GatewayTimeout => ErrorCategory.Retryable,      // 504
                HttpStatusCode.BadGateway => ErrorCategory.Retryable,          // 502
                HttpStatusCode.Unauthorized => ErrorCategory.Fatal,            // 401
                HttpStatusCode.Forbidden => ErrorCategory.Fatal,               // 403
                HttpStatusCode.BadRequest => ErrorCategory.Fatal,              // 400
                _ => ErrorCategory.Fatal
            };
        }

        // 超时
        if (ex is TaskCanceledException or TimeoutException)
            return ErrorCategory.Retryable;

        // 上下文溢出 — 根据错误消息判断
        if (ex.Message.Contains("context_length_exceeded", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("max_tokens", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("too many tokens", StringComparison.OrdinalIgnoreCase))
            return ErrorCategory.ContextOverflow;

        return ErrorCategory.Fatal;
    }
}
```

### 2. 重试策略

```csharp
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

        var delay = InitialDelay * Math.Pow(BackoffMultiplier, attempt);
        return delay > MaxDelay ? MaxDelay : delay;
    }
}
```

### 3. 改造 CodingAgent 内层循环

```csharp
// CodingAgent.cs — LLM 调用包裹重试逻辑
private async Task<ChatResponse> CallLlmWithRetryAsync(
    List<ChatMessage> messages,
    ChatOptions? options,
    CancellationToken ct)
{
    var policy = new RetryPolicy();

    for (var attempt = 0; attempt <= policy.MaxRetries; attempt++)
    {
        try
        {
            return await _client.GetResponseAsync(messages, options, ct);
        }
        catch (Exception ex) when (attempt < policy.MaxRetries)
        {
            var category = ErrorClassifier.Classify(ex);

            switch (category)
            {
                case ErrorCategory.Retryable:
                    var delay = policy.GetDelay(attempt);
                    Emit(new AgentEvent.RetryEvent(attempt + 1, delay, ex.Message));
                    await Task.Delay(delay, ct);
                    continue;

                case ErrorCategory.ContextOverflow:
                    // 触发上下文压缩后重试
                    Emit(new AgentEvent.CompactionTriggeredEvent());
                    if (TransformContext != null)
                    {
                        messages = await TransformContext(messages, ct);
                        continue;
                    }
                    throw;

                case ErrorCategory.Fatal:
                    throw;
            }
        }
    }

    throw new InvalidOperationException("Unreachable");
}
```

### 4. 新增 AgentEvent

```csharp
// AgentEvent.cs 新增
public sealed record RetryEvent(int Attempt, TimeSpan Delay, string Error) : AgentEvent;
public sealed record CompactionTriggeredEvent : AgentEvent;
```

### 5. CLI 层显示重试信息

```csharp
case AgentEvent.RetryEvent retry:
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  [Retry {retry.Attempt}] {retry.Error}");
    Console.WriteLine($"  Waiting {retry.Delay.TotalSeconds:F0}s...");
    Console.ResetColor();
    break;
```

### 6. Provider 层 — 解析 retry-after 头

```csharp
// 在 Provider 中捕获 HTTP 响应头
catch (HttpRequestException ex)
{
    // 从 HttpResponseMessage 提取 retry-after
    // 包装到自定义异常中传递给 Agent 层
    throw new LlmApiException(ex.StatusCode, ex.Message, retryAfter);
}
```

## 实现步骤

1. [ ] 创建 `src/agent/RetryPolicy.cs` — 重试策略 + 错误分类
2. [ ] `AgentEvent.cs` — 新增 `RetryEvent`, `CompactionTriggeredEvent`
3. [ ] `CodingAgent.cs` — 提取 `CallLlmWithRetryAsync` 方法
4. [ ] `CodingAgent.cs` — 上下文溢出时自动触发 `TransformContext`
5. [ ] Provider 层 — 解析 retry-after 头，包装为 `LlmApiException`
6. [ ] `Program.cs` — 显示重试状态
7. [ ] 配置化: 最大重试次数、初始延迟可通过配置调整

## 验收标准
- [ ] Rate limit (429) 自动重试，指数退避
- [ ] 网络超时自动重试
- [ ] 上下文溢出自动压缩并重试
- [ ] 认证失败不重试，直接报错
- [ ] 终端显示重试进度
- [ ] 尊重 retry-after 响应头

## 参考
- opencode: `src/session/retry.ts`

## 相关文件
- `src/agent/RetryPolicy.cs` — 新建
- `src/agent/CodingAgent.cs` — 集成重试逻辑
- `src/agent/AgentEvent.cs` — 新增事件
- `src/cli/Program.cs` — 显示重试
