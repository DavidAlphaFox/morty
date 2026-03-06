// =============================================================================
// Agent 核心
// =============================================================================
// 实现完整的智能体循环，参考 pi-mono 的双层循环架构:
//   外层循环: 处理 follow-up 消息
//   内层循环: LLM 调用 → 工具执行 → steering 检查
//
// 关键特性:
// - 每个工具执行后检查 steering，发现中断则跳过剩余工具
// - 支持上下文变换钩子 (用于压缩、外部上下文注入)
// - 结构化事件系统供 UI 层消费
// - 可配置的系统提示词
// =============================================================================

using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.AI;
using Morty.LLM;

namespace Morty.Agent;

/// <summary>
/// Coding Agent 核心类
/// </summary>
public class CodingAgent
{
    private const int MaxToolIterations = 20;

    private readonly IChatClient _client;
    private readonly SessionManager _sessionManager;
    private readonly List<AIFunction> _tools = new();
    private readonly ConcurrentQueue<ChatMessageContent> _steeringQueue = new();
    private readonly ConcurrentQueue<ChatMessageContent> _followUpQueue = new();
    private readonly RetryPolicy _retryPolicy = new();
    private readonly DoomLoopDetector _doomLoopDetector = new();
    private PermissionChecker? _permissionChecker;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// 事件订阅
    /// </summary>
    public event Action<AgentEvent>? OnEvent;

    /// <summary>
    /// 系统提示词
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// 上下文变换钩子 — 在每次 LLM 调用前对消息列表进行变换
    /// 可用于上下文压缩、外部上下文注入等
    /// </summary>
    public Func<List<ChatMessage>, CancellationToken, Task<List<ChatMessage>>>? TransformContext { get; set; }

    public CodingAgent(IChatClient client, SessionManager sessionManager)
    {
        _client = client;
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// 注册工具
    /// </summary>
    public void RegisterTool(AIFunction tool)
    {
        _tools.Add(tool);
    }

    /// <summary>
    /// 设置权限检查器
    /// </summary>
    public PermissionChecker? PermissionChecker
    {
        get => _permissionChecker;
        set => _permissionChecker = value;
    }

    /// <summary>
    /// 发送消息并获取回复 (完整的双层循环)
    /// </summary>
    public async Task<ChatMessageContent> PromptAsync(string message, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // 保存用户消息
        var userMessage = new ChatMessageContent { Role = "user", Content = message };
        var session = _sessionManager.GetCurrentSession();
        await _sessionManager.AddMessageAsync(session, userMessage);

        // 加载历史消息
        var history = await _sessionManager.LoadHistoryAsync(session);
        var chatMessages = history
            .Select(m => new ChatMessage(new ChatRole(m.Role), m.Content))
            .ToList();

        // 注入系统提示词
        if (!string.IsNullOrEmpty(SystemPrompt)
            && !chatMessages.Any(m => m.Role == ChatRole.System))
        {
            chatMessages.Insert(0, new ChatMessage(ChatRole.System, SystemPrompt));
        }

        var options = _tools.Count > 0
            ? new ChatOptions { Tools = _tools.Cast<AITool>().ToList() }
            : null;

        var allNewMessages = new List<ChatMessageContent>();

        Emit(new AgentEvent.AgentStartEvent());

        // 循环开始前先检查是否有预存的 steering 消息
        var pendingMessages = DrainSteeringQueue();
        var iteration = 0;

        // ============================================================
        // 外层循环: follow-up 消息
        // ============================================================
        while (true)
        {
            var hasMoreToolCalls = true;
            List<ChatMessageContent>? steeringFromTools = null;

            // ========================================================
            // 内层循环: 工具调用 + steering
            // ========================================================
            while (hasMoreToolCalls || pendingMessages.Count > 0)
            {
                if (++iteration > MaxToolIterations)
                {
                    Emit(new AgentEvent.AgentEndEvent(allNewMessages));
                    break;
                }

                _cts.Token.ThrowIfCancellationRequested();
                Emit(new AgentEvent.TurnStartEvent());

                // 注入待处理消息 (steering / follow-up)
                foreach (var pm in pendingMessages)
                {
                    chatMessages.Add(new ChatMessage(new ChatRole(pm.Role), pm.Content));
                    Emit(new AgentEvent.MessageStartEvent(pm.Role, pm.Content));
                    Emit(new AgentEvent.MessageEndEvent(pm.Role, pm.Content));
                }
                pendingMessages.Clear();

                // 应用上下文变换 (压缩等)
                var contextMessages = TransformContext != null
                    ? await TransformContext(chatMessages, _cts.Token)
                    : chatMessages;

                // 流式调用 LLM (含重试)
                var (text, functionCalls) = await StreamLlmWithRetryAsync(
                    contextMessages, options, _cts.Token);

                // 构建 assistant 消息加入历史
                var assistantContents = new List<AIContent>();
                if (!string.IsNullOrEmpty(text))
                    assistantContents.Add(new TextContent(text));
                assistantContents.AddRange(functionCalls);
                chatMessages.Add(assistantContents.Count > 0
                    ? new ChatMessage(ChatRole.Assistant, assistantContents)
                    : new ChatMessage(ChatRole.Assistant, text));

                hasMoreToolCalls = functionCalls.Count > 0;

                if (hasMoreToolCalls)
                {
                    var (toolResults, steering) =
                        await ExecuteToolCallsAsync(functionCalls, _cts.Token);
                    chatMessages.Add(new ChatMessage(ChatRole.Tool, toolResults));
                    steeringFromTools = steering;
                }

                Emit(new AgentEvent.TurnEndEvent());

                // 处理 steering 消息
                if (steeringFromTools is { Count: > 0 })
                {
                    pendingMessages = steeringFromTools;
                    steeringFromTools = null;
                }
                else
                {
                    pendingMessages = DrainSteeringQueue();
                }
            }

            if (iteration > MaxToolIterations) break;

            // 检查 follow-up 消息
            var followUps = DrainFollowUpQueue();
            if (followUps.Count > 0)
            {
                pendingMessages = followUps;
                continue;
            }

            break;
        }

        // 提取最终文本回复
        var finalMsg = chatMessages.LastOrDefault(m => m.Role == ChatRole.Assistant);
        var result = new ChatMessageContent
        {
            Role = "assistant",
            Content = finalMsg?.Text ?? ""
        };

        allNewMessages.Add(result);
        Emit(new AgentEvent.AgentEndEvent(allNewMessages));
        await _sessionManager.AddMessageAsync(session, result);

        return result;
    }

    /// <summary>
    /// 流式调用 LLM，含重试和上下文溢出自动压缩
    /// </summary>
    private async Task<(string Text, List<FunctionCallContent> FunctionCalls)> StreamLlmWithRetryAsync(
        List<ChatMessage> contextMessages, ChatOptions? options, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= _retryPolicy.MaxRetries; attempt++)
        {
            try
            {
                Emit(new AgentEvent.MessageStartEvent("assistant", ""));
                var textBuilder = new StringBuilder();
                var functionCalls = new List<FunctionCallContent>();

                await foreach (var update in _client.GetStreamingResponseAsync(
                    contextMessages, options, ct))
                {
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        textBuilder.Append(update.Text);
                        Emit(new AgentEvent.MessageDeltaEvent("assistant", update.Text));
                    }

                    foreach (var fc in update.Contents.OfType<FunctionCallContent>())
                        functionCalls.Add(fc);
                }

                var text = textBuilder.ToString();
                Emit(new AgentEvent.MessageEndEvent("assistant", text));
                return (text, functionCalls);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (attempt < _retryPolicy.MaxRetries)
            {
                var category = ErrorClassifier.Classify(ex);

                switch (category)
                {
                    case ErrorCategory.Retryable:
                        var delay = _retryPolicy.GetDelay(attempt);
                        Emit(new AgentEvent.RetryEvent(attempt + 1, delay, ex.Message));
                        await Task.Delay(delay, ct);
                        continue;

                    case ErrorCategory.ContextOverflow:
                        Emit(new AgentEvent.CompactionTriggeredEvent());
                        if (TransformContext != null)
                        {
                            contextMessages = await TransformContext(contextMessages, ct);
                            continue;
                        }
                        throw;

                    case ErrorCategory.Fatal:
                    default:
                        throw;
                }
            }
        }

        throw new InvalidOperationException("Max retries exceeded");
    }

    /// <summary>
    /// 执行工具调用 — 每个工具后检查 steering，中断时跳过剩余工具
    /// </summary>
    private async Task<(List<AIContent> Results, List<ChatMessageContent>? Steering)>
        ExecuteToolCallsAsync(List<FunctionCallContent> functionCalls, CancellationToken ct)
    {
        var results = new List<AIContent>();
        List<ChatMessageContent>? steeringMessages = null;

        for (var i = 0; i < functionCalls.Count; i++)
        {
            var fc = functionCalls[i];
            Emit(new AgentEvent.ToolExecutionStartEvent(fc.CallId, fc.Name, fc.Arguments));

            // Doom loop 检测
            if (_doomLoopDetector.RecordAndCheck(fc.Name, fc.Arguments))
            {
                Emit(new AgentEvent.DoomLoopDetectedEvent(fc.Name, _doomLoopDetector.Threshold));
                var doomMsg = $"Doom loop detected: {fc.Name} called {_doomLoopDetector.Threshold} times with same arguments. Try a different approach.";
                Emit(new AgentEvent.ToolExecutionEndEvent(fc.CallId, fc.Name, doomMsg, true));
                results.Add(new FunctionResultContent(fc.CallId, doomMsg));
                _steeringQueue.Enqueue(new ChatMessageContent
                {
                    Role = "system",
                    Content = $"You've called {fc.Name} with the same arguments {_doomLoopDetector.Threshold} times consecutively. This approach isn't working. Try a different strategy."
                });
                steeringMessages = DrainSteeringQueue();
                break;
            }

            // 权限检查
            if (_permissionChecker != null)
            {
                var allowed = await _permissionChecker.CheckAsync(fc.Name, fc.Arguments);
                if (!allowed)
                {
                    var denyMsg = PermissionChecker.GetDeniedMessage(fc.Name, fc.Arguments);
                    Emit(new AgentEvent.ToolExecutionEndEvent(fc.CallId, fc.Name, denyMsg, true));
                    results.Add(new FunctionResultContent(fc.CallId, denyMsg));
                    continue;
                }
            }

            var tool = _tools.FirstOrDefault(t => t.Name == fc.Name);
            object? result;
            var isError = false;

            try
            {
                if (tool == null)
                    throw new InvalidOperationException($"未知工具: {fc.Name}");

                var args = new AIFunctionArguments(
                    fc.Arguments ?? new Dictionary<string, object?>());
                result = await tool.InvokeAsync(args, ct);
            }
            catch (Exception ex)
            {
                result = $"工具执行失败: {ex.Message}";
                isError = true;
            }

            Emit(new AgentEvent.ToolExecutionEndEvent(fc.CallId, fc.Name, result, isError));
            results.Add(new FunctionResultContent(fc.CallId, result));

            // 每个工具执行后检查 steering
            var steering = DrainSteeringQueue();
            if (steering.Count > 0)
            {
                steeringMessages = steering;

                // 跳过剩余工具调用
                for (var j = i + 1; j < functionCalls.Count; j++)
                {
                    var skipped = functionCalls[j];
                    var skipMsg = "已跳过: 用户发送了新消息";
                    Emit(new AgentEvent.ToolExecutionStartEvent(
                        skipped.CallId, skipped.Name, skipped.Arguments));
                    Emit(new AgentEvent.ToolExecutionEndEvent(
                        skipped.CallId, skipped.Name, skipMsg, true));
                    results.Add(new FunctionResultContent(skipped.CallId, skipMsg));
                }
                break;
            }
        }

        return (results, steeringMessages);
    }

    // ================================================================
    // 干预 / 跟进
    // ================================================================

    /// <summary>
    /// 干预消息 — 在当前工具执行完成后送达，会中断剩余工具调用
    /// </summary>
    public void Steer(string message)
    {
        _steeringQueue.Enqueue(new ChatMessageContent
        {
            Role = "user",
            Content = message
        });
    }

    /// <summary>
    /// 跟进消息 — 在智能体即将停止时送达
    /// </summary>
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

    // ================================================================
    // 内部方法
    // ================================================================

    private void Emit(AgentEvent evt) => OnEvent?.Invoke(evt);

    private List<ChatMessageContent> DrainSteeringQueue()
    {
        var messages = new List<ChatMessageContent>();
        while (_steeringQueue.TryDequeue(out var msg))
            messages.Add(msg);
        return messages;
    }

    private List<ChatMessageContent> DrainFollowUpQueue()
    {
        var messages = new List<ChatMessageContent>();
        while (_followUpQueue.TryDequeue(out var msg))
            messages.Add(msg);
        return messages;
    }
}
