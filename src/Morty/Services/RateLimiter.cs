using Morty.Interfaces;
using System.Collections.Concurrent;

namespace Morty.Services;

/// <summary>
/// 速率限制器实现
/// 使用滑动窗口算法实现每分钟请求数限制
/// 防止超过 API 的速率限制导致请求被拒绝
/// </summary>
public class RateLimiter : IRateLimiter
{
    /// <summary>每分钟允许的最大请求数</summary>
    private readonly int _maxRequestsPerMinute;
    /// <summary>请求时间戳队列，用于追踪滑动窗口内的请求</summary>
    private readonly ConcurrentQueue<DateTime> _requestTimestamps = new();

    /// <summary>
    /// 创建速率限制器实例
    /// </summary>
    /// <param name="maxRequestsPerMinute">每分钟允许的最大请求数，默认 10</param>
    public RateLimiter(int maxRequestsPerMinute = 10)
    {
        _maxRequestsPerMinute = maxRequestsPerMinute;
    }

    /// <summary>
    /// 等待直到有可用配额
    /// 如果当前窗口内请求数已满，则等待直到最旧的请求超过 1 分钟
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task WaitForAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var now = DateTime.UtcNow;
            // 计算 1 分钟前的时间点
            var cutoff = now.AddMinutes(-1);

            // 移除超过 1 分钟的旧时间戳
            while (_requestTimestamps.TryPeek(out var oldest) && oldest < cutoff)
            {
                _requestTimestamps.TryDequeue(out _);
            }

            // 如果当前窗口内请求数未满，可以执行
            if (_requestTimestamps.Count < _maxRequestsPerMinute)
            {
                return;
            }

            // 计算需要等待的时间 - 基于窗口内最旧的请求
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
                // 队列为空，直接返回
                return;
            }
        }
    }

    /// <summary>
    /// 记录新请求
    /// 将当前时间戳添加到队列中
    /// </summary>
    public void RecordRequest()
    {
        _requestTimestamps.Enqueue(DateTime.UtcNow);
    }

    /// <summary>
    /// 获取剩余可用请求数
    /// 计算当前滑动窗口内剩余的请求配额
    /// </summary>
    /// <returns>剩余可用的请求数量</returns>
    public int GetRemainingRequests()
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-1);

        // 移除超过 1 分钟的旧时间戳
        while (_requestTimestamps.TryPeek(out var oldest) && oldest < cutoff)
        {
            _requestTimestamps.TryDequeue(out _);
        }

        return Math.Max(0, _maxRequestsPerMinute - _requestTimestamps.Count);
    }
}
