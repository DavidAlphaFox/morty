using Morty.Core.Interfaces;
using System.Collections.Concurrent;

namespace Morty.Core.Services;

public class RateLimiter : IRateLimiter
{
    private readonly int _maxRequestsPerMinute;
    private readonly ConcurrentQueue<DateTime> _requestTimestamps = new();

    public RateLimiter(int maxRequestsPerMinute = 10)
    {
        _maxRequestsPerMinute = maxRequestsPerMinute;
    }

    public async Task WaitForAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddMinutes(-1);

            // Remove old timestamps
            while (_requestTimestamps.TryPeek(out var oldest) && oldest < cutoff)
            {
                _requestTimestamps.TryDequeue(out _);
            }

            if (_requestTimestamps.Count < _maxRequestsPerMinute)
            {
                return;
            }

            // Calculate wait time - get oldest from the queue
            if (_requestTimestamps.TryPeek(out var oldestTimestamp))
            {
                var waitTime = oldestTimestamp.AddMinutes(1) - now;

                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);
                }
            }
            else
            {
                // No items, just return
                return;
            }
        }
    }

    public void RecordRequest()
    {
        _requestTimestamps.Enqueue(DateTime.UtcNow);
    }

    public int GetRemainingRequests()
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-1);

        // Remove old timestamps
        while (_requestTimestamps.TryPeek(out var oldest) && oldest < cutoff)
        {
            _requestTimestamps.TryDequeue(out _);
        }

        return Math.Max(0, _maxRequestsPerMinute - _requestTimestamps.Count);
    }
}
