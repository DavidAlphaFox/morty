using Morty.Core.Interfaces;

namespace Morty.Core.Services;

/// <summary>
/// 断路器 - 防止无限重试循环
/// </summary>
public class CircuitBreaker : ICircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private int _failureCount;
    private DateTime? _lastFailureTime;
    private CircuitBreakerState _state = CircuitBreakerState.Closed;

    public CircuitBreakerState State => _state;

    public CircuitBreaker(int failureThreshold = 5, int resetMinutes = 5)
    {
        _failureThreshold = failureThreshold;
        _resetTimeout = TimeSpan.FromMinutes(resetMinutes);
    }

    /// <summary>
    /// 检查是否可以执行请求
    /// </summary>
    public bool CanExecute()
    {
        if (_state == CircuitBreakerState.Closed)
            return true;

        if (_state == CircuitBreakerState.Open)
        {
            // 检查是否已超过重置时间
            if (_lastFailureTime.HasValue &&
                DateTime.UtcNow - _lastFailureTime.Value > _resetTimeout)
            {
                _state = CircuitBreakerState.HalfOpen;
                return true;
            }
            return false;
        }

        // 半开状态 - 允许一个请求
        return true;
    }

    /// <summary>
    /// 记录成功
    /// </summary>
    public void RecordSuccess()
    {
        _failureCount = 0;
        _state = CircuitBreakerState.Closed;
    }

    /// <summary>
    /// 记录失败
    /// </summary>
    public void RecordFailure()
    {
        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;

        if (_failureCount >= _failureThreshold)
        {
            _state = CircuitBreakerState.Open;
        }
    }

    /// <summary>
    /// 重置断路器
    /// </summary>
    public void Reset()
    {
        _failureCount = 0;
        _state = CircuitBreakerState.Closed;
        _lastFailureTime = null;
    }
}
