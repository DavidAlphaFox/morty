using Morty.Core.Interfaces;

namespace Morty.Core.Services;

/// <summary>
/// 断路器模式实现
/// 用于防止系统因连续失败而导致的级联故障
/// 保护外部服务调用（如 AI API）免受重复失败的影响
///
/// 工作原理：
/// - 初始状态为 Closed（关闭），允许正常请求
/// - 当失败次数超过阈值时，进入 Open（打开）状态，拒绝请求
/// - 经过一段冷却时间后，进入 HalfOpen（半开）状态，允许一个测试请求
/// - 如果测试请求成功，恢复 Closed 状态；否则继续保持 Open 状态
/// </summary>
public class CircuitBreaker : ICircuitBreaker
{
    /// <summary>触发断路器的失败次数阈值</summary>
    private readonly int _failureThreshold;
    /// <summary>断路器自动重置的超时时间</summary>
    private readonly TimeSpan _resetTimeout;
    /// <summary>当前连续失败计数</summary>
    private int _failureCount;
    /// <summary>最后一次失败的时间</summary>
    private DateTime? _lastFailureTime;
    /// <summary>当前断路器状态</summary>
    private CircuitBreakerState _state = CircuitBreakerState.Closed;

    /// <summary>获取当前断路器状态</summary>
    public CircuitBreakerState State => _state;

    /// <summary>
    /// 创建断路器实例
    /// </summary>
    /// <param name="failureThreshold">触发打开状态的失败次数阈值，默认 5 次</param>
    /// <param name="resetMinutes">自动重置的等待分钟数，默认 5 分钟</param>
    public CircuitBreaker(int failureThreshold = 5, int resetMinutes = 5)
    {
        _failureThreshold = failureThreshold;
        _resetTimeout = TimeSpan.FromMinutes(resetMinutes);
    }

    /// <summary>
    /// 检查是否可以执行请求
    /// 根据当前状态决定是否允许请求通过
    /// </summary>
    /// <returns>如果允许执行返回 true，否则返回 false</returns>
    public bool CanExecute()
    {
        // Closed 状态：正常运行，允许请求
        if (_state == CircuitBreakerState.Closed)
            return true;

        // Open 状态：检查是否已超过重置时间
        if (_state == CircuitBreakerState.Open)
        {
            // 检查是否已超过冷却时间
            if (_lastFailureTime.HasValue &&
                DateTime.UtcNow - _lastFailureTime.Value > _resetTimeout)
            {
                // 进入 HalfOpen 状态，允许测试请求
                _state = CircuitBreakerState.HalfOpen;
                return true;
            }
            return false;
        }

        // HalfOpen 状态：允许一个请求来测试服务是否恢复
        return true;
    }

    /// <summary>
    /// 记录请求成功
    /// 成功时重置失败计数，关闭断路器
    /// </summary>
    public void RecordSuccess()
    {
        _failureCount = 0;
        _state = CircuitBreakerState.Closed;
    }

    /// <summary>
    /// 记录请求失败
    /// 失败时增加计数，达到阈值则打开断路器
    /// </summary>
    public void RecordFailure()
    {
        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;

        // 达到失败阈值，打开断路器
        if (_failureCount >= _failureThreshold)
        {
            _state = CircuitBreakerState.Open;
        }
    }

    /// <summary>
    /// 手动重置断路器
    /// 将所有状态恢复到初始值
    /// </summary>
    public void Reset()
    {
        _failureCount = 0;
        _state = CircuitBreakerState.Closed;
        _lastFailureTime = null;
    }
}
