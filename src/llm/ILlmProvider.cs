// =============================================================================
// LLM 类型定义
// =============================================================================
// 定义会话持久化和工具调用相关的数据类型
// LLM 提供商接口使用 Microsoft.Extensions.AI 的 IChatClient
// =============================================================================

namespace Morty.LLM;

/// <summary>
/// Agent 工具定义
/// </summary>
public class AgentTool
{
    /// <summary>
    /// 工具名称
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 工具描述
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 参数 Schema
    /// </summary>
    public object? Parameters { get; set; }
}

/// <summary>
/// 聊天消息内容 (用于会话历史持久化)
/// </summary>
public class ChatMessageContent
{
    /// <summary>
    /// 角色
    /// </summary>
    public string Role { get; set; } = "";

    /// <summary>
    /// 内容
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// 模型提供商 (可选)
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// 模型 ID (可选)
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 工具调用列表
    /// </summary>
    public List<AgentTool>? ToolCalls { get; set; }

    /// <summary>
    /// 工具结果列表
    /// </summary>
    public List<ToolResult>? ToolResults { get; set; }
}

/// <summary>
/// 工具执行结果
/// </summary>
public class ToolResult
{
    /// <summary>
    /// 工具调用 ID
    /// </summary>
    public string ToolCallId { get; set; } = "";

    /// <summary>
    /// 执行结果
    /// </summary>
    public string Result { get; set; } = "";
}
