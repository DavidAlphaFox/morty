// =============================================================================
// Agent 上下文 — 子 Agent 运行环境
// =============================================================================
// 支持主 Agent 通过 task 工具产生子 Agent 处理独立子任务
// 子 Agent 在独立上下文中工作，完成后返回摘要
// =============================================================================

using Microsoft.Extensions.AI;
using Morty.Config;

namespace Morty.Agent;

/// <summary>
/// Agent 上下文 — 管理子 Agent 的创建和执行
/// </summary>
public class AgentContext
{
    private readonly IChatClient _client;
    private readonly string _cwd;
    private readonly string _sessionDir;
    private readonly ToolsConfig? _toolsConfig;
    private readonly PermissionChecker? _permissionChecker;
    private readonly int _depth;
    private const int MaxDepth = 3;

    /// <summary>
    /// 子 Agent 事件代理
    /// </summary>
    public event Action<AgentEvent>? OnEvent;

    public AgentContext(
        IChatClient client,
        string cwd,
        string sessionDir,
        ToolsConfig? toolsConfig = null,
        PermissionChecker? permissionChecker = null,
        int depth = 0)
    {
        _client = client;
        _cwd = cwd;
        _sessionDir = sessionDir;
        _toolsConfig = toolsConfig;
        _permissionChecker = permissionChecker;
        _depth = depth;
    }

    /// <summary>
    /// 执行子任务
    /// </summary>
    public async Task<string> RunTask(string description, string mode = "build")
    {
        if (_depth >= MaxDepth)
            return $"Error: Maximum sub-agent nesting depth ({MaxDepth}) reached. Cannot spawn more sub-agents.";

        // 创建子会话
        var sessionManager = SessionManager.Create(_cwd, _sessionDir);

        // 创建子 Agent
        var agent = new CodingAgent(_client, sessionManager);

        // 根据 mode 注册工具
        var tools = mode == "explore"
            ? ToolRegistry.CreateReadOnlyTools(_cwd, _toolsConfig, _permissionChecker)
            : ToolRegistry.CreateAllTools(_cwd, _toolsConfig, _permissionChecker);
        foreach (var tool in tools) agent.RegisterTool(tool);

        // 子 Agent 也有 task 工具 (递增深度)
        if (mode != "explore")
        {
            var childContext = new AgentContext(
                _client, _cwd, _sessionDir, _toolsConfig, _permissionChecker, _depth + 1);
            RegisterTaskTool(agent, childContext);
        }

        // 继承权限检查器
        agent.PermissionChecker = _permissionChecker;

        // 事件冒泡
        agent.OnEvent += evt => OnEvent?.Invoke(evt);

        // 设置子 Agent 系统提示
        agent.SystemPrompt = BuildSubAgentPrompt(description, mode);

        // 执行
        try
        {
            var result = await agent.PromptAsync(description);
            return $"[Task completed]\n{result.Content}";
        }
        catch (Exception ex)
        {
            return $"[Task failed] {ex.Message}";
        }
    }

    /// <summary>
    /// 为 Agent 注册 task 工具
    /// </summary>
    public static void RegisterTaskTool(CodingAgent agent, AgentContext context)
    {
        var tool = AIFunctionFactory.Create(
            ([System.ComponentModel.Description("Task description for the sub-agent")] string description,
             [System.ComponentModel.Description("Agent mode: 'build' (full access) or 'explore' (read-only)")] string? mode) =>
                context.RunTask(description, mode ?? "build"),
            "task",
            "Spawn a sub-agent to handle a complex subtask. " +
            "The sub-agent runs in its own context and returns a summary. " +
            "Use for tasks that require extensive exploration or would consume too much context.");
        agent.RegisterTool(tool);
    }

    private string BuildSubAgentPrompt(string task, string mode)
    {
        var modeDesc = mode == "explore"
            ? "You are in explore mode. You can only read files and search code. Do not attempt to modify files."
            : "You are in build mode with full access to all tools.";

        return $"""
            You are a sub-agent working on a specific task.
            {modeDesc}

            Work efficiently and return a concise summary of what you did and what you found.
            Current working directory: {_cwd}
            """;
    }
}
