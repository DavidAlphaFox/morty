using Morty.Core.Interfaces;
using System.Collections.Concurrent;

namespace Morty.Core.Services;

/// <summary>
/// 速率限制器 - 遵守 API 速率限制
/// </summary>
public class RateLimiter : IRateLimiter
{
    private readonly int _maxRequestsPerMinute;
    private readonly ConcurrentQueue<DateTime> _requestTimestamps = new();

    public RateLimiter(int maxRequestsPerMinute = 10)
    {
        _maxRequestsPerMinute = maxRequestsPerMinute;
    }

    /// <summary>
    /// 等待直到有可用配额
    /// </summary>
    public async Task WaitForAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddMinutes(-1);

            // 移除旧的时间戳
            while (_requestTimestamps.TryPeek(out var oldest) && oldest < cutoff)
            {
                _requestTimestamps.TryDequeue(out _);
            }

            if (_requestTimestamps.Count < _maxRequestsPerMinute)
            {
                return;
            }

            // 计算等待时间 - 获取队列中最旧的
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
                // 没有项目，直接返回
                return;
            }
        }
    }

    /// <summary>
    /// 记录请求
    /// </summary>
    public void RecordRequest()
    {
        _requestTimestamps.Enqueue(DateTime.UtcNow);
    }

    /// <summary>
    /// 获取剩余请求数
    /// </summary>
    public int GetRemainingRequests()
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-1);

        // 移除旧的时间戳
        while (_requestTimestamps.TryPeek(out var oldest) && oldest < cutoff)
        {
            _requestTimestamps.TryDequeue(out _);
        }

        return Math.Max(0, _maxRequestsPerMinute - _requestTimestamps.Count);
    }
}
