# 任务: 实现 Agent 核心

## 阶段
Phase 4: Agent 核心

## 描述
基于 Microsoft.AgentFramework 实现 CodingAgent 核心类，包括消息管理、会话历史、干预机制、上下文压缩。

## 验收标准
- [ ] 可发送消息并获取回复
- [ ] 支持工具调用
- [ ] 支持干预机制 (steer/followUp)
- [ ] 支持上下文压缩

## 实现步骤

### 10.1 创建 CodingAgent 类
创建 `src/agent/CodingAgent.cs`:
```csharp
public class CodingAgent
{
    private readonly IAgent _agent;
    private readonly MessagePipeline _pipeline;
    private readonly SessionManager _sessionManager;
    private readonly ContextCompactor _compactor;
    private readonly List<AgentTool> _tools;
    private readonly ConcurrentQueue<ChatMessageContent> _steeringQueue;
    private readonly ConcurrentQueue<ChatMessageContent> _followUpQueue;
    
    public CodingAgent(ILlmProvider provider, SessionManager sessionManager)
    {
        _sessionManager = sessionManager;
        _compactor = new ContextCompactor();
        _tools = new List<AgentTool>();
        
        // 初始化 Microsoft.AgentFramework
        _agent = AgentBuilder.Create(provider);
        _pipeline = new MessagePipeline(_agent);
    }
}
```

### 10.2 实现消息发送
```csharp
public async Task<ChatMessageContent> PromptAsync(
    string message, 
    CancellationToken ct = default)
{
    var userMessage = new ChatMessageContent
    {
        Role = "user",
        Content = message
    };
    
    // 添加到会话历史
    var session = _sessionManager.GetCurrentSession();
    await _sessionManager.AddMessageAsync(session, userMessage);
    
    // 发送给 Agent
    var response = await _pipeline.SendAsync(
        session.Messages,
        _tools,
        ct);
    
    // 保存回复
    await _sessionManager.AddMessageAsync(session, response);
    
    return response;
}
```

### 10.3 实现干预机制
```csharp
// 干预消息 - 在当前工具执行完成后送达
public void Steer(string message)
{
    _steeringQueue.Enqueue(new ChatMessageContent
    {
        Role = "system",
        Content = message
    });
}

// 跟进消息 - 在 Agent 完成后送达
public void FollowUp(string message)
{
    _followUpQueue.Enqueue(new ChatMessageContent
    {
        Role = "user",
        Content = message
    });
}
```

### 10.4 实现继续执行
```csharp
public async Task ContinueAsync(CancellationToken ct = default)
{
    // 处理干预队列
    while (_steeringQueue.TryDequeue(out var steerMsg))
    {
        // 在工具执行后注入消息
        await _pipeline.InjectAsync(steerMsg, ct);
    }
    
    // 处理跟进队列
    while (_followUpQueue.TryDequeue(out var followUpMsg))
    {
        await PromptAsync(followUpMsg.Content, ct);
    }
}
```

### 10.5 实现中止
```csharp
public void Abort()
{
    _pipeline.Cancel();
}
```

### 10.6 实现事件订阅
```csharp
public IDisposable Subscribe(EventHandler<AgentEventArgs> handler)
{
    return _pipeline.OnEvent += handler;
}
```

## 相关文件
- tasks/07-zhipu-provider.md
- tasks/10-session-manager.md
- design/dotnet-coding-agent.md (3.2 Agent 核心)
