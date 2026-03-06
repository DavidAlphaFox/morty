# 任务 2.2: Batch 工具

## 阶段
Phase 2 — 功能扩展

## 目标
新增 `batch` 工具，让 Agent 可以并行执行多个工具调用。

## 背景
当 LLM 需要同时读取多个文件或执行多个独立操作时，逐个顺序执行很慢。opencode 的 batch 工具允许最多 25 个并行调用。

## 设计方案

### 工具定义

```csharp
// ToolRegistry.cs 新增
if (enabled.Contains("batch"))
    tools.Add(AIFunctionFactory.Create(
        ([Description("JSON array of tool calls: [{\"tool\":\"read_file\",\"args\":{\"path\":\"...\"}}]")]
         string calls) => batchExecutor.ExecuteAsync(calls),
        "batch",
        "Execute multiple tool calls in parallel. " +
        "Input: JSON array of {tool, args} objects. Max 25 calls. " +
        "Returns aggregated results. Use when you need to perform multiple independent operations."));
```

### BatchExecutor

```csharp
// src/agent/BatchExecutor.cs

public class BatchExecutor
{
    private readonly List<AIFunction> _tools;
    private const int MaxConcurrent = 25;

    public async Task<string> ExecuteAsync(string callsJson)
    {
        var calls = JsonSerializer.Deserialize<List<BatchCall>>(callsJson);
        if (calls == null || calls.Count == 0)
            return "Error: No tool calls provided";
        if (calls.Count > MaxConcurrent)
            return $"Error: Maximum {MaxConcurrent} concurrent calls allowed";

        var tasks = calls.Select(async (call, i) =>
        {
            var tool = _tools.FirstOrDefault(t => t.Name == call.Tool);
            if (tool == null)
                return $"[{i}] Error: Unknown tool '{call.Tool}'";

            try
            {
                var args = new AIFunctionArguments(call.Args ?? new());
                var result = await tool.InvokeAsync(args);
                return $"[{i}] {call.Tool}: {result}";
            }
            catch (Exception ex)
            {
                return $"[{i}] {call.Tool} Error: {ex.Message}";
            }
        });

        var results = await Task.WhenAll(tasks);
        var success = results.Count(r => !r.Contains("Error:"));
        return $"Batch: {success}/{calls.Count} succeeded\n\n" +
               string.Join("\n\n", results);
    }
}
```

## 实现步骤

1. [ ] 创建 `src/agent/BatchExecutor.cs`
2. [ ] `ToolRegistry.cs` — 新增 batch 工具注册
3. [ ] 权限检查: batch 中的每个子调用都需独立检查权限
4. [ ] 输出截断: 聚合结果过大时截断

## 验收标准
- [ ] Agent 可通过 batch 并行读取多个文件
- [ ] 并发限制 25 个
- [ ] 单个失败不影响其他调用
- [ ] 返回聚合统计

## 参考
- opencode: `src/tool/batch.ts`

## 相关文件
- `src/agent/BatchExecutor.cs` — 新建
- `src/agent/ToolRegistry.cs` — 注册
