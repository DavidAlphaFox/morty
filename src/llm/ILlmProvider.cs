// =============================================================================
// LLM 提供商接口定义
// =============================================================================
// 定义 LLM 提供商的统一接口，支持 Chat、Stream、Tool Call 等功能
// =============================================================================

namespace Morty.LLM;

/// <summary>
/// LLM 提供商接口
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// 提供商名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 支持的模型列表
    /// </summary>
    IReadOnlyList<string> SupportedModels { get; }

    /// <summary>
    /// 发送聊天请求
    /// </summary>
    /// <param name="request">聊天请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>聊天响应</returns>
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default);

    /// <summary>
    /// 发送聊天请求 (流式输出)
    /// </summary>
    /// <param name="request">聊天请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>流式响应枚举器</returns>
    IAsyncEnumerable<string> StreamChatAsync(ChatRequest request, CancellationToken ct = default);

    /// <summary>
    /// 发送带工具调用的聊天请求
    /// </summary>
    /// <param name="request">聊天请求</param>
    /// <param name="tools">工具列表</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>聊天响应 (可能包含工具调用)</returns>
    Task<ChatResponse> ChatWithToolsAsync(ChatRequest request, IList<AgentTool> tools, CancellationToken ct = default);
}

/// <summary>
/// 聊天请求
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// 模型名称
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    /// 消息历史
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// 温度参数 (0-2)
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// 最大 token 数
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// 工具列表
    /// </summary>
    public List<string>? Tools { get; set; }
}

/// <summary>
/// 聊天消息
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// 角色: system, user, assistant, tool
    /// </summary>
    public string Role { get; set; } = "";

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// 工具调用 ID
    /// </summary>
    public string? ToolCallId { get; set; }

    /// <summary>
    /// 工具名称
    /// </summary>
    public string? ToolName { get; set; }
}

/// <summary>
/// 聊天响应
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// 回复内容
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// 工具调用 ID
    /// </summary>
    public string? ToolCallId { get; set; }

    /// <summary>
    /// 工具名称
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// Token 使用量
    /// </summary>
    public Usage? Usage { get; set; }

    /// <summary>
    /// 结束原因
    /// </summary>
    public string? FinishReason { get; set; }
}

/// <summary>
/// Token 使用量
/// </summary>
public class Usage
{
    /// <summary>
    /// 输入 token 数
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// 输出 token 数
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// 总 token 数
    /// </summary>
    public int TotalTokens { get; set; }
}

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
/// 聊天消息内容 (用于会话历史)
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
