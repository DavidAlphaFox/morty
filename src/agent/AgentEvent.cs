// =============================================================================
// 智能体事件
// =============================================================================
// 定义智能体生命周期中发出的所有事件类型
// 用于 UI 更新、日志记录和状态同步
//
// 事件层级:
//   agent_start / agent_end           — 智能体整体生命周期
//   turn_start / turn_end             — 对话轮次 (一次 LLM 调用 + 工具执行)
//   message_start / message_end       — 消息生命周期
//   tool_execution_start/end          — 工具执行生命周期
// =============================================================================

namespace Morty.Agent;

/// <summary>
/// 智能体事件基类
/// </summary>
public abstract record AgentEvent
{
    /// <summary>
    /// 智能体开始处理
    /// </summary>
    public sealed record AgentStartEvent : AgentEvent;

    /// <summary>
    /// 智能体处理完成
    /// </summary>
    public sealed record AgentEndEvent(List<LLM.ChatMessageContent> NewMessages) : AgentEvent;

    /// <summary>
    /// 新的对话轮次开始 (一次 LLM 调用 + 可能的工具执行)
    /// </summary>
    public sealed record TurnStartEvent : AgentEvent;

    /// <summary>
    /// 对话轮次结束
    /// </summary>
    public sealed record TurnEndEvent : AgentEvent;

    /// <summary>
    /// 消息开始 (user / assistant / tool)
    /// </summary>
    public sealed record MessageStartEvent(string Role, string Content) : AgentEvent;

    /// <summary>
    /// 消息增量 (流式输出)
    /// </summary>
    public sealed record MessageDeltaEvent(string Role, string Delta) : AgentEvent;

    /// <summary>
    /// 消息结束
    /// </summary>
    public sealed record MessageEndEvent(string Role, string Content) : AgentEvent;

    /// <summary>
    /// 工具执行开始
    /// </summary>
    public sealed record ToolExecutionStartEvent(
        string ToolCallId,
        string ToolName,
        IDictionary<string, object?>? Args) : AgentEvent;

    /// <summary>
    /// 工具执行结束
    /// </summary>
    public sealed record ToolExecutionEndEvent(
        string ToolCallId,
        string ToolName,
        object? Result,
        bool IsError) : AgentEvent;

    /// <summary>
    /// API 调用重试
    /// </summary>
    public sealed record RetryEvent(int Attempt, TimeSpan Delay, string Error) : AgentEvent;

    /// <summary>
    /// 上下文压缩触发
    /// </summary>
    public sealed record CompactionTriggeredEvent : AgentEvent;

    /// <summary>
    /// Doom Loop 检测
    /// </summary>
    public sealed record DoomLoopDetectedEvent(string ToolName, int RepeatCount) : AgentEvent;
}
