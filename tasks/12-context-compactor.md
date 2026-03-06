# 任务: 实现上下文压缩

## 阶段
Phase 4: Agent 核心

## 描述
实现上下文压缩功能，当 token 接近限制时，使用 LLM 总结旧消息，保留关键信息。

## 验收标准
- [ ] 可估算 token 数量
- [ ] 可压缩历史消息
- [ ] 保留关键信息 (文件修改、决策)
- [ ] 中文总结支持

## 实现步骤

### 12.1 创建上下文压缩器
创建 `src/agent/ContextCompactor.cs`:
```csharp
public class ContextCompactor
{
    private const int DefaultMaxTokens = 128000;
    
    public async Task<ChatHistory> CompressAsync(
        ChatHistory history,
        int maxTokens,
        IAgent agent,
        CancellationToken ct = default)
    {
        // 1. 计算当前 token 数
        var currentTokens = EstimateTokens(history);
        
        // 2. 如果未超阈值，直接返回
        if (currentTokens < maxTokens * 0.8)
            return history;
        
        // 3. 分离消息：保留最近的和可压缩的
        var (keep, compress) = SplitMessages(history);
        
        // 4. 使用 LLM 总结旧消息
        var summary = await SummarizeAsync(compress, agent, ct);
        
        // 5. 合并结果
        return BuildCompressedHistory(keep, summary);
    }
}
```

### 12.2 Token 估算
```csharp
public int EstimateTokens(ChatMessageContent message)
{
    // 简单估算: 中文字符 ≈ 2 tokens, 英文 ≈ 1.3 tokens
    var text = message.Content;
    var chineseChars = text.Count(c => IsChinese(c));
    var otherChars = text.Length - chineseChars;
    
    return chineseChars * 2 + (int)(otherChars * 1.3);
}

public int EstimateTokens(ChatHistory history)
{
    return history.Sum(EstimateTokens);
}
```

### 12.3 消息分离
```csharp
private (List<ChatMessageContent> keep, List<ChatMessageContent> compress) SplitMessages(
    ChatHistory history)
{
    var keep = new List<ChatMessageContent>();
    var compress = new List<ChatMessageContent>();
    
    // 保留最近 20 条消息
    const int keepCount = 20;
    
    for (int i = 0; i < history.Count; i++)
    {
        if (i >= history.Count - keepCount)
            keep.Add(history[i]);
        else
            compress.Add(history[i]);
    }
    
    // 保留工具调用结果
    keep.AddRange(history.Where(m => m.Role == "tool"));
    
    return (keep, compress);
}
```

### 12.4 消息总结
```csharp
private async Task<string> SummarizeAsync(
    List<ChatMessageContent> messages,
    IAgent agent,
    CancellationToken ct)
{
    var prompt = BuildCompressionPrompt(messages);
    
    var summaryRequest = new ChatRequest
    {
        Messages = new List<ChatMessageContent>
        {
            new() { Role = "system", Content = "你是一个专业的代码助手。请简洁总结以下对话，保留关键信息如文件名、修改内容、决策等。" },
            new() { Role = "user", Content = prompt }
        },
        MaxTokens = 2000
    };
    
    var response = await agent.ChatAsync(summaryRequest, ct);
    return response.Content;
}
```

### 12.5 构建压缩提示
```csharp
public string BuildCompressionPrompt(ChatHistory oldMessages)
{
    var sb = new StringBuilder();
    sb.AppendLine("请简洁总结以下对话历史：");
    sb.AppendLine();
    
    foreach (var msg in oldMessages)
    {
        var role = msg.Role switch
        {
            "user" => "用户",
            "assistant" => "助手",
            "tool" => "工具",
            _ => msg.Role
        };
        
        sb.AppendLine($"[{role}]: {msg.Content}");
        sb.AppendLine();
    }
    
    return sb.ToString();
}
```

## 相关文件
- tasks/10-coding-agent.md
- design/dotnet-coding-agent.md (3.3 上下文压缩)
