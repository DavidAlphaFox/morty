# 任务 2.1: 子 Agent (Task 工具)

## 阶段
Phase 2 — 功能扩展

## 目标
新增 `task` 工具，让主 Agent 可以产生子 Agent 处理独立的复杂子任务。

## 背景
复杂任务（如"重构整个模块"）需要大量上下文。主 Agent 上下文被工具输出填满时效率下降。opencode 通过 task 工具产生子会话，子 Agent 在独立上下文中工作，完成后只返回摘要。

## 设计方案

### 1. Task 工具定义

```csharp
// ToolRegistry.cs 新增
if (enabled.Contains("task"))
    tools.Add(AIFunctionFactory.Create(
        ([Description("Task description for the sub-agent")] string description,
         [Description("Agent mode: 'build' (full access) or 'explore' (read-only)")] string? mode) =>
            agentContext.RunTask(description, mode ?? "build"),
        "task",
        "Spawn a sub-agent to handle a complex subtask. " +
        "The sub-agent runs in its own context and returns a summary. " +
        "Use for tasks that require extensive exploration or would consume too much context."));
```

### 2. AgentContext — 子 Agent 运行环境

```csharp
// src/agent/AgentContext.cs

public class AgentContext
{
    private readonly IChatClient _client;
    private readonly string _cwd;
    private readonly string _sessionDir;
    private readonly List<AIFunction> _tools;
    private readonly string? _systemPrompt;

    public async Task<string> RunTask(string description, string mode = "build")
    {
        // 1. 创建子会话
        var sessionManager = SessionManager.Create(_cwd, _sessionDir);

        // 2. 创建子 Agent
        var agent = new CodingAgent(_client, sessionManager);

        // 3. 根据 mode 注册工具
        var tools = mode == "explore"
            ? ToolRegistry.CreateReadOnlyTools(_cwd)
            : ToolRegistry.CreateAllTools(_cwd);
        foreach (var tool in tools) agent.RegisterTool(tool);

        // 4. 设置子 Agent 系统提示
        agent.SystemPrompt = BuildSubAgentPrompt(description, mode);

        // 5. 执行
        var result = await agent.PromptAsync(description);

        // 6. 返回摘要
        return $"[Task completed]\n{result.Content}";
    }

    private string BuildSubAgentPrompt(string task, string mode)
    {
        var modeDesc = mode == "explore"
            ? "You are in explore mode. You can only read files and search code. Do not attempt to modify files."
            : "You are in build mode with full access to all tools.";

        return $"""
            You are a sub-agent working on a specific task.
            {modeDesc}

            Your task: {task}

            Work efficiently and return a concise summary of what you did and what you found.
            Current working directory: {_cwd}
            """;
    }
}
```

### 3. 最大递归深度

```csharp
// 防止无限嵌套
public class AgentContext
{
    private readonly int _depth;
    private const int MaxDepth = 3;

    public async Task<string> RunTask(string description, string mode = "build")
    {
        if (_depth >= MaxDepth)
            return "Error: Maximum sub-agent nesting depth reached.";
        // ... 创建子 Agent 时传递 _depth + 1
    }
}
```

### 4. 集成到 CodingAgent

```csharp
// CodingAgent 构造函数扩展
public CodingAgent(IChatClient client, SessionManager sessionManager,
    AgentContext? agentContext = null)
{
    _agentContext = agentContext ?? new AgentContext(client, ...);
}
```

## 实现步骤

1. [ ] 创建 `src/agent/AgentContext.cs` — 子 Agent 运行环境
2. [ ] `ToolRegistry.cs` — 新增 `task` 工具注册
3. [ ] `CodingAgent.cs` — 注入 AgentContext
4. [ ] 实现最大递归深度限制
5. [ ] 子 Agent 的事件冒泡 (可选: 子 Agent 进度透传到主 Agent 事件)
6. [ ] 配置: 子 Agent 默认工具集、系统提示

## 验收标准
- [ ] 主 Agent 可通过 task 工具产生子 Agent
- [ ] 子 Agent 在独立上下文中执行
- [ ] explore 模式子 Agent 只有只读工具
- [ ] 嵌套深度限制生效
- [ ] 子 Agent 完成后返回摘要给主 Agent

## 参考
- opencode: `src/tool/task.ts`, `src/agent/agent.ts`

## 相关文件
- `src/agent/AgentContext.cs` — 新建
- `src/agent/ToolRegistry.cs` — 新增 task 工具
- `src/agent/CodingAgent.cs` — 集成
