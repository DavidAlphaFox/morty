// =============================================================================
// Agent 模式
// =============================================================================
// Build 模式: 完整权限，可读写执行
// Plan 模式: 只读探索，适用于代码分析和方案设计
// =============================================================================

namespace Morty.Agent;

/// <summary>
/// Agent 运行模式
/// </summary>
public enum AgentMode
{
    /// <summary>完整权限，可读写执行</summary>
    Build,

    /// <summary>只读模式，只能读取和搜索</summary>
    Plan
}

/// <summary>
/// Agent 模式配置
/// </summary>
public static class AgentModeConfig
{
    /// <summary>
    /// 获取模式对应的工具列表
    /// </summary>
    public static List<string> GetTools(AgentMode mode) => mode switch
    {
        AgentMode.Build => new()
        {
            "read", "write", "edit", "bash", "grep", "glob", "ls",
            "task", "batch", "webfetch"
        },
        AgentMode.Plan => new()
        {
            "read", "grep", "glob", "ls", "bash", "webfetch"
        },
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    /// <summary>
    /// 获取模式对应的权限规则
    /// </summary>
    public static List<PermissionRule> GetPermissions(AgentMode mode) => mode switch
    {
        AgentMode.Build => new()
        {
            new() { Permission = "read", Pattern = "*", Action = PermissionAction.Allow },
            new() { Permission = "edit", Pattern = "*", Action = PermissionAction.Ask },
            new() { Permission = "bash", Pattern = "*", Action = PermissionAction.Ask }
        },
        AgentMode.Plan => new()
        {
            new() { Permission = "read", Pattern = "*", Action = PermissionAction.Allow },
            new() { Permission = "edit", Pattern = "*", Action = PermissionAction.Deny },
            new() { Permission = "bash", Pattern = "*", Action = PermissionAction.Ask }
        },
        _ => new()
    };

    /// <summary>
    /// 获取模式对应的系统提示后缀
    /// </summary>
    public static string GetSystemPromptSuffix(AgentMode mode) => mode switch
    {
        AgentMode.Plan => """

            You are in PLAN mode. You can read, search, and analyze code but CANNOT modify files.
            Use this mode to understand the codebase and create a plan.
            Focus on analysis, architecture, and recommendations.
            """,
        _ => ""
    };
}
