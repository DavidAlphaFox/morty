// =============================================================================
// 事件总线
// =============================================================================
// Pub/Sub 事件系统，解耦 Agent 核心和 UI/持久化层
// 支持按类型订阅、全局订阅、取消订阅
// =============================================================================

using System.Collections.Concurrent;

namespace Morty.Agent;

/// <summary>
/// 事件总线 — 类型安全的 pub/sub 事件系统
/// </summary>
public class EventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    /// <summary>
    /// 发布事件
    /// </summary>
    public void Publish<T>(T evt) where T : AgentEvent
    {
        // 发布到具体类型订阅
        InvokeHandlers(typeof(T), evt);

        // 也发布到全局 AgentEvent 订阅
        if (typeof(T) != typeof(AgentEvent))
            InvokeHandlers(typeof(AgentEvent), evt);
    }

    /// <summary>
    /// 订阅特定类型的事件
    /// </summary>
    public IDisposable Subscribe<T>(Action<T> handler) where T : AgentEvent
    {
        lock (_lock)
        {
            var handlers = _handlers.GetOrAdd(typeof(T), _ => new List<Delegate>());
            handlers.Add(handler);
        }
        return new Subscription(() =>
        {
            lock (_lock)
            {
                if (_handlers.TryGetValue(typeof(T), out var handlers))
                    handlers.Remove(handler);
            }
        });
    }

    /// <summary>
    /// 订阅所有事件
    /// </summary>
    public IDisposable SubscribeAll(Action<AgentEvent> handler)
    {
        return Subscribe(handler);
    }

    private void InvokeHandlers<T>(Type type, T evt)
    {
        List<Delegate>? handlers;
        lock (_lock)
        {
            if (!_handlers.TryGetValue(type, out handlers)) return;
            handlers = new List<Delegate>(handlers); // snapshot
        }

        foreach (var handler in handlers)
        {
            try
            {
                if (handler is Action<T> typed)
                    typed(evt);
                else if (handler is Action<AgentEvent> global && evt is AgentEvent agentEvt)
                    global(agentEvt);
            }
            catch
            {
                // 事件处理器异常不应中断发布
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose() => _unsubscribe();
    }
}
