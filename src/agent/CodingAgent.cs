// =============================================================================
// Agent 核心
// =============================================================================
// 负责与 LLM Provider 交互，处理消息和工具调用
// 支持干预机制 (steer/followUp) 和会话管理
// =============================================================================

using System.Collections.Concurrent;
using Morty.LLM;

namespace Morty.Agent;

/// <summary>
/// Coding Agent 核心类
/// </summary>
public class CodingAgent
{
    /// <summary>
    /// LLM Provider
    /// </summary>
    private readonly ILlmProvider _provider;

    /// <summary>
    /// 会话管理器
    /// </summary>
    private readonly SessionManager _sessionManager;

    /// <summary>
    /// 已注册的工具列表
    /// </summary>
    private readonly List<AgentTool> _tools = new();

    /// <summary>
    /// 干预消息队列 (steer)
    /// </summary>
    private readonly ConcurrentQueue<ChatMessageContent> _steeringQueue = new();

    /// <summary>
    /// 跟进消息队列 (followUp)
    /// </summary>
    private readonly ConcurrentQueue<ChatMessageContent> _followUpQueue = new();

    /// <summary>
    /// 取消令牌源
    /// </summary>
    private CancellationTokenSource? _cts;

    /// <summary>
    /// 初始化 Coding Agent
    /// </summary>
    /// <param name="provider">LLM Provider</param>
    /// <param name="sessionManager">会话管理器</param>
    public CodingAgent(ILlmProvider provider, SessionManager sessionManager)
    {
        _provider = provider;
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// 注册工具
    /// </summary>
    /// <param name="tool">工具</param>
    public void RegisterTool(AgentTool tool)
    {
        _tools.Add(tool);
    }

    /// <summary>
    /// 发送消息并获取回复
    /// </summary>
    /// <param name="message">用户消息</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>Assistant 回复</returns>
    public async Task<ChatMessageContent> PromptAsync(string message, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // 创建用户消息
        var userMessage = new ChatMessageContent
        {
            Role = "user",
            Content = message
        };

        // 获取当前会话并添加消息
        var session = _sessionManager.GetCurrentSession();
        await _sessionManager.AddMessageAsync(session, userMessage);

        // 加载历史消息
        var history = await _sessionManager.LoadHistoryAsync(session);

        // 构建请求
        var request = new ChatRequest
        {
            Model = _provider.SupportedModels.First(),
            Messages = history.Select(m => new ChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        };

        // 发送请求
        var response = await _provider.ChatAsync(request, _cts.Token);

        // 保存回复
        var assistantMessage = new ChatMessageContent
        {
            Role = "assistant",
            Content = response.Content
        };

        await _sessionManager.AddMessageAsync(session, assistantMessage);

        return assistantMessage;
    }

    /// <summary>
    /// 干预消息 - 在当前工具执行完成后送达
    /// </summary>
    /// <param name="message">干预内容</param>
    public void Steer(string message)
    {
        _steeringQueue.Enqueue(new ChatMessageContent
        {
            Role = "system",
            Content = message
        });
    }

    /// <summary>
    /// 跟进消息 - 在 Agent 完成后送达
    /// </summary>
    /// <param name="message">跟进内容</param>
    public void FollowUp(string message)
    {
        _followUpQueue.Enqueue(new ChatMessageContent
        {
            Role = "user",
            Content = message
        });
    }

    /// <summary>
    /// 中止当前操作
    /// </summary>
    public void Abort()
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// 继续执行 (处理干预和跟进队列)
    /// </summary>
    /// <param name="ct">取消令牌</param>
    public async Task ContinueAsync(CancellationToken ct = default)
    {
        // 处理干预队列
        while (_steeringQueue.TryDequeue(out var steerMsg))
        {
            Console.WriteLine($"[Steer] {steerMsg.Content}");
        }

        // 处理跟进队列
        while (_followUpQueue.TryDequeue(out var followUpMsg))
        {
            await PromptAsync(followUpMsg.Content, ct);
        }
    }
}
