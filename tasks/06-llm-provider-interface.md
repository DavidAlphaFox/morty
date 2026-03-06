# 任务: 实现 LLM 提供商接口

## 阶段
Phase 3: LLM 提供商

## 描述
定义 LLM 提供商的统一接口，支持 Chat、Stream、Tool Call 等功能。

## 验收标准
- [ ] 接口定义完整
- [ ] 支持同步和异步
- [ ] 支持流式输出
- [ ] 支持工具调用

## 实现步骤

### 6.1 定义 LLM 提供商接口
创建 `src/llm/ILlmProvider.cs`:
```csharp
public interface ILlmProvider
{
    string Name { get; }
    IReadOnlyList<string> SupportedModels { get; }
    
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default);
    
    IAsyncEnumerable<string> StreamChatAsync(ChatRequest request, CancellationToken ct = default);
    
    Task<ChatResponse> ChatWithToolsAsync(ChatRequest request, IList<AgentTool> tools, CancellationToken ct = default);
}

public class ChatRequest
{
    public string Model { get; set; } = "";
    public List<ChatMessage> Messages { get; set; } = new();
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public List<string>? Tools { get; set; }
}

public class ChatMessage
{
    public string Role { get; set; } = "";  // system, user, assistant, tool
    public string Content { get; set; } = "";
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
}

public class ChatResponse
{
    public string Content { get; set; } = "";
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
    public Usage? Usage { get; set; }
    public string? FinishReason { get; set; }
}

public class Usage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
```

### 6.2 创建 Provider 工厂
创建 `src/llm/ProviderFactory.cs`:
```csharp
public static class ProviderFactory
{
    public static ILlmProvider Create(string providerName, string apiKey, string? baseUrl = null)
    {
        return providerName.ToLower() switch
        {
            "zhipu" => new ZhipuProvider(apiKey, baseUrl),
            "minimax" => new MiniMaxProvider(apiKey, baseUrl),
            "qianwen" or "qwen" => new QianwenProvider(apiKey, baseUrl),
            _ => throw new NotSupportedException($"不支持的 LLM 提供商: {providerName}")
        };
    }
}
```

## 相关文件
- design/dotnet-coding-agent.md (3.1 LLM 提供商)
- tasks/04-config-loader.md
