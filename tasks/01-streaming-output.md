# 任务 1.1: 流式输出

## 阶段
Phase 1 — 核心体验提升

## 目标
Agent 循环使用流式 API 接收 LLM 响应，逐 token 输出到终端，流式过程中实时解析 tool_call。

## 背景
当前 `CodingAgent.PromptAsync()` 使用 `_client.GetResponseAsync()` 等待完整响应后一次性输出。用户体验差：长回复时终端无任何反馈。三家 Provider 已实现 `GetStreamingResponseAsync()`（SSE 解析），但 Agent 循环未使用。

## 当前代码分析

### CodingAgent.cs (需改造)
```
// 第 139 行 — 当前是阻塞调用
var response = await _client.GetResponseAsync(contextMessages, options, _cts.Token);
var assistantMsg = response.Messages.Last();
```
需要替换为流式调用，边接收边：
1. 发出 `MessageDeltaEvent` 事件 (文本增量)
2. 累积完整文本
3. 解析流中的 tool_call 内容

### AgentEvent.cs (需新增事件)
当前只有 `MessageStartEvent` / `MessageEndEvent`，缺少增量事件。

### Program.cs (需改造输出)
当前 `OnAgentEvent` 在 `MessageEndEvent` 时一次性输出全文。需要改为在 `MessageDeltaEvent` 时逐块输出。

## 设计方案

### 1. 新增 AgentEvent 类型

```csharp
// AgentEvent.cs 新增
public sealed record MessageDeltaEvent(string Role, string Delta) : AgentEvent;

public sealed record ToolCallDeltaEvent(
    string ToolCallId,
    string ToolName,
    string ArgumentsDelta) : AgentEvent;
```

### 2. 改造 CodingAgent 内层循环

将 `GetResponseAsync` 替换为 `GetStreamingResponseAsync`：

```csharp
// CodingAgent.cs — 替换第 139-150 行
Emit(new AgentEvent.MessageStartEvent("assistant", ""));

var fullContents = new List<AIContent>();
var textBuilder = new StringBuilder();
var toolCallBuilders = new Dictionary<string, ToolCallAccumulator>();

await foreach (var update in _client.GetStreamingResponseAsync(
    contextMessages, options, _cts.Token))
{
    // 处理文本增量
    if (!string.IsNullOrEmpty(update.Text))
    {
        textBuilder.Append(update.Text);
        Emit(new AgentEvent.MessageDeltaEvent("assistant", update.Text));
    }

    // 处理工具调用增量
    foreach (var content in update.Contents)
    {
        if (content is FunctionCallContent fc)
        {
            fullContents.Add(fc);
        }
    }
}

var text = textBuilder.ToString();
Emit(new AgentEvent.MessageEndEvent("assistant", text));

// 构建 assistant 消息加入 chatMessages
var assistantMsg = new ChatMessage(ChatRole.Assistant, text);
if (fullContents.Count > 0)
{
    assistantMsg = new ChatMessage(ChatRole.Assistant, fullContents);
}
chatMessages.Add(assistantMsg);
```

### 3. 流式 tool_call 解析

M.E.AI 的 `ChatResponseUpdate` 中 `FunctionCallContent` 可能分多个 chunk 到达。需要累积：

```csharp
internal class ToolCallAccumulator
{
    public string CallId { get; set; } = "";
    public string Name { get; set; } = "";
    public StringBuilder ArgumentsJson { get; } = new();

    public FunctionCallContent Build()
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(
            ArgumentsJson.ToString());
        return new FunctionCallContent(CallId, Name, args);
    }
}
```

### 4. CLI 层流式渲染

```csharp
// Program.cs OnAgentEvent
case AgentEvent.MessageDeltaEvent delta:
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write(delta.Delta);  // 不换行，逐块输出
    Console.ResetColor();
    break;

case AgentEvent.MessageEndEvent { Role: "assistant" }:
    Console.WriteLine();  // 消息结束换行
    break;
```

### 5. Provider 流式 tool_call 支持

当前三家 Provider 的 `GetStreamingResponseAsync` 只解析文本 delta，不解析 tool_call delta。需要扩展 SSE 解析逻辑处理 `delta.tool_calls`：

```csharp
// ZhipuProvider.cs GetStreamingResponseAsync — 扩展 chunk 解析
if (chunk?.Choices?.First()?.Delta?.ToolCalls is { } toolCalls)
{
    foreach (var tc in toolCalls)
    {
        yield return new ChatResponseUpdate
        {
            Contents = new List<AIContent>
            {
                new FunctionCallContent(tc.Id, tc.Function.Name,
                    ParseArgs(tc.Function.Arguments))
            }
        };
    }
}
```

## 实现步骤

1. [ ] `AgentEvent.cs` — 新增 `MessageDeltaEvent` 和 `ToolCallDeltaEvent`
2. [ ] Provider 层 — 三家 Provider 的 `GetStreamingResponseAsync` 支持 tool_call delta 解析
3. [ ] `CodingAgent.cs` — 内层循环替换为流式调用
4. [ ] `CodingAgent.cs` — 实现 `ToolCallAccumulator` 累积流式 tool_call
5. [ ] `Program.cs` — `OnAgentEvent` 处理 `MessageDeltaEvent` 逐块输出
6. [ ] 测试: 长文本回复能逐字显示
7. [ ] 测试: tool_call 能正确从流中解析并执行

## 验收标准
- [ ] LLM 回复逐 token 显示在终端 (打字机效果)
- [ ] 流式过程中 tool_call 能被正确解析和执行
- [ ] Ctrl+C 中断时流式输出立即停止
- [ ] 三家 Provider 均支持流式 tool_call

## 参考
- opencode: `src/session/processor.ts` (流事件处理), `src/session/llm.ts` (streamText)
- pi-mono: `packages/coding-agent/src/core/agent-session.ts` (streaming)
- M.E.AI: `IChatClient.GetStreamingResponseAsync()` 返回 `IAsyncEnumerable<ChatResponseUpdate>`

## 相关文件
- `src/agent/CodingAgent.cs` — 主要改造
- `src/agent/AgentEvent.cs` — 新增事件
- `src/cli/Program.cs` — 输出渲染
- `src/llm/ZhipuProvider.cs` — 流式 tool_call
- `src/llm/MiniMaxProvider.cs` — 流式 tool_call
- `src/llm/QianwenProvider.cs` — 流式 tool_call
