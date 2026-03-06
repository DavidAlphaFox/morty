# 任务 2.5: Plan 模式

## 阶段
Phase 2 — 功能扩展

## 目标
新增 plan 模式（只读探索 Agent），默认拒绝文件修改，适用于代码分析和方案设计。

## 背景
用户有时只想让 Agent 分析代码、制定方案，不希望它修改任何文件。opencode 有独立的 plan agent，拒绝 edit/write 权限，可通过 `plan_exit` 切换回 build 模式。

## 设计方案

### 1. Agent 模式定义

```csharp
// src/agent/AgentMode.cs

public enum AgentMode
{
    /// <summary>完整权限，可读写执行</summary>
    Build,

    /// <summary>只读模式，只能读取和搜索</summary>
    Plan
}

public static class AgentModeConfig
{
    public static List<string> GetTools(AgentMode mode) => mode switch
    {
        AgentMode.Build => new() { "read", "write", "edit", "bash", "grep", "glob", "ls", "task", "batch", "webfetch" },
        AgentMode.Plan => new() { "read", "grep", "glob", "ls", "bash", "webfetch", "plan_exit" },
        _ => throw new ArgumentOutOfRangeException()
    };

    public static List<PermissionRule> GetPermissions(AgentMode mode) => mode switch
    {
        AgentMode.Build => new()
        {
            new() { Permission = "read", Action = PermissionAction.Allow },
            new() { Permission = "edit", Action = PermissionAction.Ask },
            new() { Permission = "bash", Action = PermissionAction.Ask }
        },
        AgentMode.Plan => new()
        {
            new() { Permission = "read", Action = PermissionAction.Allow },
            new() { Permission = "edit", Action = PermissionAction.Deny },
            new() { Permission = "bash", Action = PermissionAction.Ask }  // 只允许只读命令
        },
        _ => new()
    };

    public static string GetSystemPromptSuffix(AgentMode mode) => mode switch
    {
        AgentMode.Plan => """

            You are in PLAN mode. You can read, search, and analyze code but CANNOT modify files.
            Use this mode to understand the codebase and create a plan.
            When you have a plan ready, use the plan_exit tool to switch to build mode.
            """,
        _ => ""
    };
}
```

### 2. plan_exit 工具

```csharp
// ToolRegistry.cs 新增
if (enabled.Contains("plan_exit"))
    tools.Add(AIFunctionFactory.Create(
        ([Description("Summary of the plan to execute")] string plan) =>
            agentContext.ExitPlanMode(plan),
        "plan_exit",
        "Switch from plan mode to build mode. Provide a summary of your analysis and plan."));
```

### 3. CLI 集成

```csharp
// Program.cs — 新增 --plan 选项
var planOption = new Option<bool>("--plan") { Description = "Start in plan (read-only) mode" };

// RunInteractiveAsync 中
var mode = plan ? AgentMode.Plan : AgentMode.Build;
Console.WriteLine($"Mode: {mode}");
```

### 4. 运行时模式切换

```csharp
// 在交互循环中支持 /plan 和 /build 命令
if (input.Equals("/plan", StringComparison.OrdinalIgnoreCase))
{
    // 切换到 plan 模式，重新配置工具和权限
    SwitchMode(agent, AgentMode.Plan);
    continue;
}
```

## 实现步骤

1. [ ] 创建 `src/agent/AgentMode.cs` — 模式定义 + 工具/权限映射
2. [ ] `ToolRegistry.cs` — 新增 `plan_exit` 工具
3. [ ] `CodingAgent.cs` — 支持运行时切换工具集
4. [ ] `SystemPromptBuilder.cs` — plan 模式系统提示后缀
5. [ ] `Program.cs` — `--plan` 选项 + `/plan` `/build` 交互命令
6. [ ] Plan 模式下 bash 工具自动过滤写操作

## 验收标准
- [ ] `morty --plan` 进入只读模式
- [ ] Plan 模式下 edit/write 被拒绝
- [ ] Agent 可通过 plan_exit 切换到 build 模式
- [ ] /plan /build 交互命令可切换模式

## 参考
- opencode: `src/agent/agent.ts` plan agent

## 相关文件
- `src/agent/AgentMode.cs` — 新建
- `src/agent/ToolRegistry.cs` — plan_exit 工具
- `src/agent/CodingAgent.cs` — 模式切换
- `src/cli/Program.cs` — CLI 选项
