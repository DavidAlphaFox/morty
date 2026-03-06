# 任务: 实现会话管理

## 阶段
Phase 4: Agent 核心

## 描述
实现会话管理系统，支持会话存储 (JSONL 格式)、加载历史、会话分支 (Fork)。

## 验收标准
- [ ] 会话存储为 JSONL 格式
- [ ] 可加载历史会话
- [ ] 支持会话分支 (Fork)
- [ ] 与 pi 格式兼容

## 实现步骤

### 11.1 创建会话管理器
创建 `src/agent/SessionManager.cs`:
```csharp
public class SessionManager
{
    private readonly string _sessionDir;
    private Session? _currentSession;
    
    public SessionManager(string sessionDir)
    {
        _sessionDir = Path.GetFullPath(ExpandPath(sessionDir));
        Directory.CreateDirectory(_sessionDir);
    }
    
    public Session GetCurrentSession() => _currentSession ?? CreateSession();
}
```

### 11.2 创建会话
```csharp
public async Task<Session> CreateSessionAsync(string? workingDirectory = null)
{
    var session = new Session
    {
        Id = Guid.NewGuid().ToString("N"),
        WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
        CreatedAt = DateTimeOffset.UtcNow
    };
    
    _currentSession = session;
    
    // 保存会话元信息
    var metaPath = Path.Combine(_sessionDir, $"{session.Id}.meta.json");
    await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(session));
    
    return session;
}
```

### 11.3 保存消息
```csharp
public async Task AddMessageAsync(Session session, ChatMessageContent message)
{
    var msg = new SessionMessage
    {
        Id = Guid.NewGuid().ToString("N"),
        ParentId = session.LastMessageId,
        Role = message.Role,
        Content = message.Content,
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        ToolCalls = message.ToolCalls,
        ToolResults = message.ToolResults
    };
    
    session.LastMessageId = msg.Id;
    
    // 追加到 JSONL 文件
    var msgPath = Path.Combine(_sessionDir, $"{session.Id}.jsonl");
    await using var writer = new StreamWriter(msgPath, append: true);
    await writer.WriteLineAsync(JsonSerializer.Serialize(msg));
}
```

### 11.4 加载历史
```csharp
public async Task<List<ChatMessageContent>> LoadHistoryAsync(Session session)
{
    var messages = new List<ChatMessageContent>();
    var msgPath = Path.Combine(_sessionDir, $"{session.Id}.jsonl");
    
    if (!File.Exists(msgPath))
        return messages;
    
    var lines = await File.ReadAllLinesAsync(msgPath);
    foreach (var line in lines)
    {
        var msg = JsonSerializer.Deserialize<SessionMessage>(line);
        if (msg != null)
        {
            messages.Add(new ChatMessageContent
            {
                Role = msg.Role,
                Content = msg.Content,
                ToolCalls = msg.ToolCalls,
                ToolResults = msg.ToolResults
            });
        }
    }
    
    return messages;
}
```

### 11.5 会话分支 (Fork)
```csharp
public async Task<Session> ForkAsync(Session session, string fromMessageId)
{
    var forked = await CreateSessionAsync(session.WorkingDirectory);
    
    // 复制消息直到指定消息
    var msgPath = Path.Combine(_sessionDir, $"{session.Id}.jsonl");
    var forkedMsgPath = Path.Combine(_sessionDir, $"{forked.Id}.jsonl");
    
    var lines = await File.ReadAllLinesAsync(msgPath);
    var writer = new StreamWriter(forkedMsgPath);
    
    foreach (var line in lines)
    {
        var msg = JsonSerializer.Deserialize<SessionMessage>(line);
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
```

## 会话文件格式

### 元信息文件 (.meta.json)
```json
{
  "id": "session_xxx",
  "workingDirectory": "/home/david/project",
  "createdAt": 1234567890,
  "lastMessageId": "msg_xxx"
}
```

### 消息文件 (.jsonl)
```json
{"id": "msg_xxx", "parentId": "msg_yyy", "role": "user", "content": "...", "timestamp": 1234567890}
{"id": "msg_yyy", "parentId": "msg_xxx", "role": "assistant", "content": "...", "toolCalls": [...]}
```

## 相关文件
- tasks/10-coding-agent.md
- design/dotnet-coding-agent.md (3.3 会话管理)
