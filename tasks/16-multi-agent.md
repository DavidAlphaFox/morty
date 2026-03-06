# 任务 3.5: 多 Agent 架构

## 阶段
Phase 3 — 高级特性

## 目标
支持定义多个 Agent 角色，每个有独立的工具集、权限和系统提示。

## 设计方案

### 1. Agent 定义

```csharp
// src/agent/AgentDefinition.cs

public class AgentDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public AgentMode Mode { get; set; } = AgentMode.Build;
    public List<string>? Tools { get; set; }
    public List<PermissionRule>? Permissions { get; set; }
    public string? SystemPrompt { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
}
```

### 2. 内置 Agent

```csharp
public static class BuiltinAgents
{
    public static readonly AgentDefinition Build = new()
    {
        Name = "build",
        Description = "Full-access development agent",
        Mode = AgentMode.Build
    };

    public static readonly AgentDefinition Plan = new()
    {
        Name = "plan",
        Description = "Read-only exploration and planning agent",
        Mode = AgentMode.Plan,
        Tools = new() { "read", "grep", "glob", "ls", "bash", "webfetch" }
    };

    public static readonly AgentDefinition Explore = new()
    {
        Name = "explore",
        Description = "Fast search and analysis agent",
        Tools = new() { "read", "grep", "glob", "ls" }
    };
}
```

### 3. Agent 选择

```csharp
// CLI: morty --agent explore "find all API endpoints"
var agentOption = new Option<string?>("--agent", "-a") { Description = "Agent to use (build, plan, explore)" };
```

### 4. 自定义 Agent (配置文件)

```json
// .morty/agents/reviewer.json
{
  "name": "reviewer",
  "description": "Code review agent",
  "tools": ["read", "grep", "glob", "ls"],
  "systemPrompt": "You are a code reviewer. Analyze code for bugs, security issues, and style."
}
```

## 实现步骤

1. [ ] 创建 `src/agent/AgentDefinition.cs`
2. [ ] 创建 `src/agent/BuiltinAgents.cs` — 内置 agent 定义
3. [ ] Agent 加载: 合并内置 + `.morty/agents/` 自定义
4. [ ] `CodingAgent` — 根据 AgentDefinition 配置工具和权限
5. [ ] CLI — `--agent` 选项和 `morty agent list` 子命令
6. [ ] task 工具 — 支持指定子 agent 角色

## 验收标准
- [ ] `morty --agent plan` 使用 plan agent
- [ ] `morty agent list` 列出可用 agent
- [ ] 自定义 agent 从 `.morty/agents/` 加载
- [ ] task 工具可指定子 agent

## 参考
- opencode: `src/agent/agent.ts`

## 相关文件
- `src/agent/AgentDefinition.cs` — 新建
- `src/agent/BuiltinAgents.cs` — 新建
- `src/cli/Program.cs` — CLI 选项
