# 任务 3.6: 事件总线

## 阶段
Phase 3 — 高级特性

## 目标
实现 pub/sub 事件系统，解耦 Agent 核心和 UI/持久化层。

## 设计方案

### 事件总线

```csharp
// src/agent/EventBus.cs

public class EventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

    public void Publish<T>(T evt) where T : AgentEvent
    {
        if (_handlers.TryGetValue(typeof(T), out var handlers))
            foreach (var handler in handlers)
                ((Action<T>)handler)(evt);

        // 也发布到全局 AgentEvent 订阅
        if (_handlers.TryGetValue(typeof(AgentEvent), out var globalHandlers))
            foreach (var handler in globalHandlers)
                ((Action<AgentEvent>)handler)(evt);
    }

    public IDisposable Subscribe<T>(Action<T> handler) where T : AgentEvent
    {
        var handlers = _handlers.GetOrAdd(typeof(T), _ => new());
        handlers.Add(handler);
        return new Subscription(() => handlers.Remove(handler));
    }
}
```

### 集成到 CodingAgent

```csharp
// 替换 OnEvent 事件为 EventBus
public EventBus Events { get; } = new();

// Emit 改为
private void Emit(AgentEvent evt) => Events.Publish(evt);
```

## 实现步骤

1. [ ] 创建 `src/agent/EventBus.cs`
2. [ ] `CodingAgent.cs` — 用 EventBus 替换 `OnEvent` 事件
3. [ ] `Program.cs` — 通过 Subscribe 注册处理器
4. [ ] 支持类型安全的事件订阅

## 验收标准
- [ ] 可订阅特定事件类型
- [ ] 可订阅所有事件
- [ ] 订阅可取消 (IDisposable)

## 参考
- opencode: `src/bus/index.ts`, `src/bus/bus-event.ts`

## 相关文件
- `src/agent/EventBus.cs` — 新建
- `src/agent/CodingAgent.cs` — 改造
