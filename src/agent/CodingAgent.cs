using System.Collections.Concurrent;
using Morty.LLM;

namespace Morty.Agent;

public class CodingAgent
{
    private readonly ILlmProvider _provider;
    private readonly SessionManager _sessionManager;
    private readonly List<AgentTool> _tools = new();
    private readonly ConcurrentQueue<ChatMessageContent> _steeringQueue = new();
    private readonly ConcurrentQueue<ChatMessageContent> _followUpQueue = new();
    private CancellationTokenSource? _cts;

    public CodingAgent(ILlmProvider provider, SessionManager sessionManager)
    {
        _provider = provider;
        _sessionManager = sessionManager;
    }

    public void RegisterTool(AgentTool tool)
    {
        _tools.Add(tool);
    }

    public async Task<ChatMessageContent> PromptAsync(string message, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var userMessage = new ChatMessageContent
        {
            Role = "user",
            Content = message
        };

        var session = _sessionManager.GetCurrentSession();
        await _sessionManager.AddMessageAsync(session, userMessage);

        var history = await _sessionManager.LoadHistoryAsync(session);

        var request = new ChatRequest
        {
            Model = _provider.SupportedModels.First(),
            Messages = history.Select(m => new ChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        };

        var response = await _provider.ChatAsync(request, _cts.Token);

        var assistantMessage = new ChatMessageContent
        {
            Role = "assistant",
            Content = response.Content
        };

        await _sessionManager.AddMessageAsync(session, assistantMessage);

        return assistantMessage;
    }

    public void Steer(string message)
    {
        _steeringQueue.Enqueue(new ChatMessageContent
        {
            Role = "system",
            Content = message
        });
    }

    public void FollowUp(string message)
    {
        _followUpQueue.Enqueue(new ChatMessageContent
        {
            Role = "user",
            Content = message
        });
    }

    public void Abort()
    {
        _cts?.Cancel();
    }

    public async Task ContinueAsync(CancellationToken ct = default)
    {
        while (_steeringQueue.TryDequeue(out var steerMsg))
        {
            Console.WriteLine($"[Steer] {steerMsg.Content}");
        }

        while (_followUpQueue.TryDequeue(out var followUpMsg))
        {
            await PromptAsync(followUpMsg.Content, ct);
        }
    }
}
