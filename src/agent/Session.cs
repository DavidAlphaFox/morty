// =============================================================================
// 会话模型
// =============================================================================
// 定义会话和消息的数据结构
// =============================================================================

using System.Text.Json.Serialization;

namespace Morty.Agent;

/// <summary>
/// 会话
/// </summary>
public class Session
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    /// 工作目录
    /// </summary>
    [JsonPropertyName("workingDirectory")]
    public string WorkingDirectory { get; set; } = "";

    /// <summary>
    /// 创建时间 (Unix 时间戳)
    /// </summary>
    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; }

    /// <summary>
    /// 最后消息 ID
    /// </summary>
    [JsonPropertyName("lastMessageId")]
    public string? LastMessageId { get; set; }
}

/// <summary>
/// 会话消息
/// </summary>
public class SessionMessage
{
    /// <summary>
    /// 消息 ID
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    /// 父消息 ID (用于会话分支)
    /// </summary>
    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    /// <summary>
    /// 角色: user, assistant, system, tool
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    /// <summary>
    /// 消息内容
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    /// <summary>
    /// 时间戳
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>
    /// 工具调用列表
    /// </summary>
    [JsonPropertyName("toolCalls")]
    public List<ToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// 工具结果列表
    /// </summary>
    [JsonPropertyName("toolResults")]
    public List<ToolResult>? ToolResults { get; set; }
}

/// <summary>
/// 工具调用
/// </summary>
public class ToolCall
{
    /// <summary>
    /// 调用 ID
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    /// 工具名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// 参数 (JSON 字符串)
    /// </summary>
    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = "";
}

/// <summary>
/// 工具结果
/// </summary>
public class ToolResult
{
    /// <summary>
    /// 工具调用 ID
    /// </summary>
    [JsonPropertyName("toolCallId")]
    public string ToolCallId { get; set; } = "";

    /// <summary>
    /// 执行结果
    /// </summary>
    [JsonPropertyName("result")]
    public string Result { get; set; } = "";
}
