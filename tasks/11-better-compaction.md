# 任务 2.6: 更好的上下文压缩

## 阶段
Phase 2 — 功能扩展

## 目标
改进上下文压缩策略：工具输出裁剪、专用压缩 prompt、压缩状态反馈。

## 背景
当前 `ContextCompactor` 比较简陋：简单按消息数分割（保留最近 20 条），token 估算不精确，压缩 prompt 固定。opencode 采用更精细的策略：先裁剪旧工具输出（保留最近 40K token），再做 LLM 摘要。

## 设计方案

### 1. 工具输出裁剪（压缩前的轻量化处理）

```csharp
// ContextCompactor.cs — 新增 PruneToolOutputs

/// <summary>
/// 裁剪旧的工具输出，保留最近的工具结果
/// </summary>
public List<ChatMessage> PruneToolOutputs(
    List<ChatMessage> messages, int keepRecentToolTokens = 40000)
{
    var result = new List<ChatMessage>();
    var toolTokensFromEnd = 0;

    // 从后向前扫描，保留最近 N token 的工具输出
    for (var i = messages.Count - 1; i >= 0; i--)
    {
        var msg = messages[i];
        if (msg.Role == ChatRole.Tool)
        {
            var tokens = EstimateTokens(msg.Text ?? "");
            toolTokensFromEnd += tokens;

            if (toolTokensFromEnd > keepRecentToolTokens)
            {
                // 替换为 [compacted] 标记
                result.Insert(0, new ChatMessage(ChatRole.Tool, "[Output compacted]"));
                continue;
            }
        }
        result.Insert(0, msg);
    }

    return result;
}
```

### 2. 专用压缩 prompt

```csharp
// 改进 SummarizeAsync
private const string CompactionSystemPrompt = """
    You are summarizing a coding conversation for context compression.
    Preserve ALL of the following:
    - File paths that were read, edited, or created
    - Code changes made (what was changed and why)
    - Decisions and rationale
    - Current task status and next steps
    - Error messages and their resolutions
    - User preferences and requirements mentioned

    Be concise but complete. Use bullet points. Do not lose any actionable information.
    """;
```

### 3. 两阶段压缩

```csharp
public async Task<List<ChatMessage>> CompressAsync(
    List<ChatMessage> history, int maxTokens, IChatClient client, CancellationToken ct)
{
    var current = EstimateTokens(history);
    if (current < maxTokens * 0.8) return history;

    // 阶段 1: 裁剪旧工具输出
    var pruned = PruneToolOutputs(history);
    current = EstimateTokens(pruned);
    if (current < maxTokens * 0.8) return pruned;

    // 阶段 2: LLM 摘要
    var (keep, compress) = SplitMessages(pruned);
    if (compress.Count == 0) return keep;

    var summary = await SummarizeAsync(compress, client, ct);
    keep.Insert(0, new ChatMessage(ChatRole.System, $"[Conversation summary]\n{summary}"));
    return keep;
}
```

### 4. 压缩事件

```csharp
// AgentEvent 新增
public sealed record CompactionStartEvent(int TokensBefore) : AgentEvent;
public sealed record CompactionEndEvent(int TokensBefore, int TokensAfter) : AgentEvent;
```

### 5. 改进 token 估算

```csharp
// 对 ChatMessage 列表的估算 (包括 tool call 内容)
public int EstimateTokens(List<ChatMessage> messages)
{
    var total = 0;
    foreach (var msg in messages)
    {
        total += 4; // 消息格式开销
        foreach (var content in msg.Contents)
        {
            total += content switch
            {
                TextContent text => EstimateTokens(text.Text),
                FunctionCallContent fc =>
                    EstimateTokens(fc.Name) +
                    EstimateTokens(JsonSerializer.Serialize(fc.Arguments)),
                FunctionResultContent fr =>
                    EstimateTokens(fr.Result?.ToString() ?? ""),
                _ => 0
            };
        }
    }
    return total;
}
```

## 实现步骤

1. [ ] `ContextCompactor.cs` — 实现 `PruneToolOutputs` 工具输出裁剪
2. [ ] `ContextCompactor.cs` — 改进压缩 prompt，保留关键信息
3. [ ] `ContextCompactor.cs` — 两阶段压缩逻辑
4. [ ] `ContextCompactor.cs` — 改进 token 估算 (支持 ChatMessage)
5. [ ] `AgentEvent.cs` — 新增压缩事件
6. [ ] `Program.cs` — 显示压缩状态

## 验收标准
- [ ] 旧工具输出优先被裁剪
- [ ] 压缩摘要保留文件路径、代码变更、决策等关键信息
- [ ] 终端显示压缩进度
- [ ] 两阶段压缩，轻量裁剪能解决时不触发 LLM 摘要

## 参考
- opencode: `src/session/compaction.ts`

## 相关文件
- `src/agent/ContextCompactor.cs` — 主要改造
- `src/agent/AgentEvent.cs` — 新增事件
- `src/cli/Program.cs` — 显示
