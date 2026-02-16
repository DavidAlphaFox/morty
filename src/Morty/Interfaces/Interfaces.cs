namespace Morty.Interfaces;

/// <summary>
/// 响应分析器接口
/// </summary>
public interface IResponseAnalyzer
{
    /// <summary>基于 exit code 和输出内容分析结果</summary>
    AnalysisResult Analyze(string output, bool processSuccess);
    /// <summary>提取计划</summary>
    PlanResult? ExtractPlan(string output);
    /// <summary>提取修改的文件</summary>
    IEnumerable<string> ExtractChangedFiles(string output);
}

/// <summary>
/// 分析结果
/// </summary>
public record AnalysisResult(
    bool IsComplete,
    bool TestsPassing,
    bool HasErrors,
    IEnumerable<string> ChangedFiles,
    string? ErrorMessage
);

/// <summary>
/// 计划结果
/// </summary>
public record PlanResult(
    string Plan,
    IEnumerable<string> Tasks
);

/// <summary>
/// 断路器接口
/// </summary>
public interface ICircuitBreaker
{
    /// <summary>当前状态</summary>
    CircuitBreakerState State { get; }
    /// <summary>是否可以执行</summary>
    bool CanExecute();
    /// <summary>记录成功</summary>
    void RecordSuccess();
    /// <summary>记录失败</summary>
    void RecordFailure();
    /// <summary>重置</summary>
    void Reset();
}

/// <summary>
/// 断路器状态
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>关闭 - 正常操作</summary>
    Closed,
    /// <summary>打开 - 失败次数过多</summary>
    Open,
    /// <summary>半开 - 测试恢复</summary>
    HalfOpen
}

/// <summary>
/// 速率限制器接口
/// </summary>
public interface IRateLimiter
{
    /// <summary>等待可用配额</summary>
    Task WaitForAvailabilityAsync(CancellationToken cancellationToken = default);
    /// <summary>记录请求</summary>
    void RecordRequest();
    /// <summary>获取剩余请求数</summary>
    int GetRemainingRequests();
}
