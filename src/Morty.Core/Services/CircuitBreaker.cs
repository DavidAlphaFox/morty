using Morty.Core.Interfaces;

namespace Morty.Core.Services;

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

    public bool CanExecute()
    {
        if (_state == CircuitBreakerState.Closed)
            return true;

        if (_state == CircuitBreakerState.Open)
        {
            if (_lastFailureTime.HasValue &&
                DateTime.UtcNow - _lastFailureTime.Value > _resetTimeout)
            {
                _state = CircuitBreakerState.HalfOpen;
                return true;
            }
            return false;
        }

        // HalfOpen - allow one request
        return true;
    }

    public void RecordSuccess()
    {
        _failureCount = 0;
        _state = CircuitBreakerState.Closed;
    }

    public void RecordFailure()
    {
        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;

        if (_failureCount >= _failureThreshold)
        {
            _state = CircuitBreakerState.Open;
        }
    }

    public void Reset()
    {
        _failureCount = 0;
        _state = CircuitBreakerState.Closed;
        _lastFailureTime = null;
    }
}
