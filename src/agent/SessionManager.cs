using System.Text.Json;
using Morty.Config;
using Morty.LLM;

namespace Morty.Agent;

public class SessionManager
{
    private readonly string _sessionDir;
    private Session? _currentSession;
    private readonly JsonSerializerOptions _jsonOptions;

    public SessionManager(string sessionDir)
    {
        _sessionDir = Path.GetFullPath(ConfigLoader.ExpandPath(sessionDir));
        Directory.CreateDirectory(_sessionDir);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };
    }

    public Session GetCurrentSession() => _currentSession ?? throw new InvalidOperationException("No active session");

    public async Task<Session> CreateSessionAsync(string? workingDirectory = null)
    {
        var session = new Session
        {
            Id = Guid.NewGuid().ToString("N"),
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _currentSession = session;

        var metaPath = Path.Combine(_sessionDir, $"{session.Id}.meta.json");
        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(session));

        return session;
    }

    public async Task AddMessageAsync(Session session, ChatMessageContent message)
    {
        var msg = new SessionMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            ParentId = session.LastMessageId,
            Role = message.Role,
            Content = message.Content,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        session.LastMessageId = msg.Id;

        if (message.ToolCalls != null)
        {
            msg.ToolCalls = message.ToolCalls.Select(t => new ToolCall
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = t.Name,
                Arguments = t.Parameters?.ToString() ?? ""
            }).ToList();
        }

        var msgPath = Path.Combine(_sessionDir, $"{session.Id}.jsonl");
        await using var writer = new StreamWriter(msgPath, append: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(msg, _jsonOptions));
    }

    public async Task<List<ChatMessageContent>> LoadHistoryAsync(Session session)
    {
        var messages = new List<ChatMessageContent>();
        var msgPath = Path.Combine(_sessionDir, $"{session.Id}.jsonl");

        if (!File.Exists(msgPath))
            return messages;

        var lines = await File.ReadAllLinesAsync(msgPath);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var msg = JsonSerializer.Deserialize<SessionMessage>(line, _jsonOptions);
            if (msg == null) continue;

            messages.Add(new ChatMessageContent
            {
                Role = msg.Role,
                Content = msg.Content,
                ToolCalls = msg.ToolCalls?.Select(t => new LLM.AgentTool
                {
                    Name = t.Name,
                    Parameters = t.Arguments
                }).ToList(),
                ToolResults = msg.ToolResults?.Select(t => new LLM.ToolResult
                {
                    ToolCallId = t.ToolCallId,
                    Result = t.Result
                }).ToList()
            });
        }

        return messages;
    }

    public async Task<Session> ForkAsync(Session session, string fromMessageId)
    {
        var forked = await CreateSessionAsync(session.WorkingDirectory);

        var msgPath = Path.Combine(_sessionDir, $"{session.Id}.jsonl");
        var forkedMsgPath = Path.Combine(_sessionDir, $"{forked.Id}.jsonl");

        if (!File.Exists(msgPath))
            return forked;

        var lines = await File.ReadAllLinesAsync(msgPath);
        var writer = new StreamWriter(forkedMsgPath);

        foreach (var line in lines)
        {
            var msg = JsonSerializer.Deserialize<SessionMessage>(line, _jsonOptions);
            if (msg?.Id == fromMessageId)
            {
                await writer.WriteLineAsync(line);
                break;
            }
            await writer.WriteLineAsync(line);
        }

        await writer.DisposeAsync();

        return forked;
    }
}
