# 任务 1.4: Doom Loop 检测

## 阶段
Phase 1 — 核心体验提升

## 目标
检测 Agent 循环中重复的工具调用模式，自动中断并提示用户。

## 背景
LLM 有时会陷入死循环：反复调用同一工具、相同参数但期待不同结果。opencode 检测连续 3+ 次相同 tool_call 自动中断。当前 morty 只有 `MaxToolIterations = 20` 的硬限制，浪费大量 token。

## 设计方案

### 1. 检测逻辑

```csharp
// src/agent/DoomLoopDetector.cs

public class DoomLoopDetector
{
    private readonly int _threshold;
    private readonly List<string> _recentCallSignatures = new();

    public DoomLoopDetector(int threshold = 3)
    {
        _threshold = threshold;
    }

    /// <summary>
    /// 记录一次工具调用，返回是否检测到 doom loop
    /// </summary>
    public bool RecordAndCheck(string toolName, IDictionary<string, object?>? args)
    {
        var signature = BuildSignature(toolName, args);
        _recentCallSignatures.Add(signature);

        // 检查最近 N 次是否相同
        if (_recentCallSignatures.Count < _threshold)
            return false;

        var recent = _recentCallSignatures
            .Skip(_recentCallSignatures.Count - _threshold)
            .ToList();

        return recent.All(s => s == recent[0]);
    }

    /// <summary>
    /// 重置检测器 (新轮次时调用)
    /// </summary>
    public void Reset() => _recentCallSignatures.Clear();

    private static string BuildSignature(string toolName, IDictionary<string, object?>? args)
    {
        if (args == null || args.Count == 0)
            return toolName;

        // 排序 key 保证稳定性
        var sortedArgs = args
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}={kv.Value}")
            .ToArray();

        return $"{toolName}({string.Join(",", sortedArgs)})";
    }
}
```

### 2. 集成到 CodingAgent

```csharp
// CodingAgent.cs — ExecuteToolCallsAsync 中

// 在工具执行前检测
if (_doomLoopDetector.RecordAndCheck(fc.Name, fc.Arguments))
{
    Emit(new AgentEvent.DoomLoopDetectedEvent(fc.Name, _doomLoopDetector.Threshold));

    // 注入系统消息提示 LLM 换方法
    _steeringQueue.Enqueue(new ChatMessageContent
    {
        Role = "system",
        Content = $"You've called {fc.Name} with the same arguments {_doomLoopDetector.Threshold} times. " +
                  "This suggests the current approach isn't working. Try a different strategy."
    });

    result = $"Doom loop detected: {fc.Name} called {_doomLoopDetector.Threshold} times with same args. Trying different approach.";
    isError = true;
    break;
}
```

### 3. 新增 AgentEvent

```csharp
public sealed record DoomLoopDetectedEvent(string ToolName, int RepeatCount) : AgentEvent;
```

### 4. CLI 显示

```csharp
case AgentEvent.DoomLoopDetectedEvent doom:
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  [Doom loop] {doom.ToolName} repeated {doom.RepeatCount} times");
    Console.ResetColor();
    break;
```

## 实现步骤

1. [ ] 创建 `src/agent/DoomLoopDetector.cs`
2. [ ] `AgentEvent.cs` — 新增 `DoomLoopDetectedEvent`
3. [ ] `CodingAgent.cs` — 在 `ExecuteToolCallsAsync` 中集成检测
4. [ ] `CodingAgent.cs` — 检测到后注入 steering 消息让 LLM 换策略
5. [ ] `Program.cs` — 显示 doom loop 警告
6. [ ] 配置化: 阈值可通过配置文件调整

## 验收标准
- [ ] 连续 3 次相同 tool_call 触发中断
- [ ] 中断后注入提示让 LLM 尝试不同方法
- [ ] 终端显示 doom loop 警告
- [ ] 阈值可配置
- [ ] 参数不同的调用不触发 (如读不同文件)

## 参考
- opencode: `src/session/processor.ts` doom loop 检测

## 相关文件
- `src/agent/DoomLoopDetector.cs` — 新建
- `src/agent/CodingAgent.cs` — 集成
- `src/agent/AgentEvent.cs` — 新增事件
- `src/cli/Program.cs` — 显示
