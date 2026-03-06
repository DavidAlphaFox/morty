// =============================================================================
// 会话管理器
// =============================================================================
// 负责会话的创建、保存、加载和分支
// 会话存储格式: JSONL (每行一个 JSON 消息)
// =============================================================================

using System.Text.Json;
using Morty.Config;
using Morty.LLM;

namespace Morty.Agent;

/// <summary>
/// 会话管理器
/// </summary>
public class SessionManager
{
    /// <summary>
    /// 会话存储目录
    /// </summary>
    private readonly string _sessionDir;

    /// <summary>
    /// 当前会话
    /// </summary>
    private Session? _currentSession;

    /// <summary>
    /// JSON 序列化选项
    /// </summary>
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// 初始化会话管理器
    /// </summary>
    /// <param name="sessionDir">会话存储目录</param>
    public SessionManager(string sessionDir)
    {
        _sessionDir = Path.GetFullPath(ConfigLoader.ExpandPath(sessionDir));
        Directory.CreateDirectory(_sessionDir);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };
    }

    /// <summary>
    /// 获取当前会话
    /// </summary>
    /// <returns>当前会话</returns>
    /// <exception cref="InvalidOperationException">没有活动会话</exception>
    public Session GetCurrentSession() => _currentSession 
        ?? throw new InvalidOperationException("No active session");

    /// <summary>
    /// 创建新会话
    /// </summary>
    /// <param name="workingDirectory">工作目录 (可选)</param>
    /// <returns>新会话</returns>
    public async Task<Session> CreateSessionAsync(string? workingDirectory = null)
    {
        var session = new Session
        {
            Id = Guid.NewGuid().ToString("N"),
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _currentSession = session;

        // 保存会话元信息
        var metaPath = Path.Combine(_sessionDir, $"{session.Id}.meta.json");
        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(session));

        return session;
    }

    /// <summary>
    /// 添加消息到会话
    /// </summary>
    /// <param name="session">会话</param>
    /// <param name="message">消息内容</param>
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

        // 保存工具调用信息
        if (message.ToolCalls != null)
        {
            msg.ToolCalls = message.ToolCalls.Select(t => new ToolCall
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = t.Name,
                Arguments = t.Parameters?.ToString() ?? ""
            }).ToList();
        }

        session.LastMessageId = msg.Id;

        // 追加到 JSONL 文件
        var msgPath = Path.Combine(_sessionDir, $"{session.Id}.jsonl");
        await using var writer = new StreamWriter(msgPath, append: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(msg, _jsonOptions));
    }

    /// <summary>
    /// 加载会话历史
    /// </summary>
    /// <param name="session">会话</param>
    /// <returns>消息列表</returns>
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

    /// <summary>
    /// 分支会话 (Fork)
    /// </summary>
    /// <param name="session">源会话</param>
    /// <param name="fromMessageId">从指定消息处分支</param>
    /// <returns>新会话</returns>
    public async Task<Session> ForkAsync(Session session, string fromMessageId)
    {
        // 创建新会话
        var forked = await CreateSessionAsync(session.WorkingDirectory);

        var msgPath = Path.Combine(_sessionDir, $"{session.Id}.jsonl");
        var forkedMsgPath = Path.Combine(_sessionDir, $"{forked.Id}.jsonl");

        if (!File.Exists(msgPath))
            return forked;

        // 复制消息直到指定消息
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
