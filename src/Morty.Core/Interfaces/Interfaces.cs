namespace Morty.Core.Interfaces;

public interface IClaudeClient
{
    Task<ClaudeResponse> SendMessageAsync(string message, CancellationToken cancellationToken = default);
    Task<ClaudeResponse> SendMessageWithContextAsync(string message, string projectPath, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> StreamMessageAsync(string message, CancellationToken cancellationToken = default);
}

public record ClaudeResponse(
    string Content,
    string? Error,
    int ExitCode,
    bool Success
);

public interface IResponseAnalyzer
{
    AnalysisResult Analyze(string output);
    PlanResult? ExtractPlan(string output);
    bool IsImplementationComplete(string output);
    bool AreTestsPassing(string output);
    IEnumerable<string> ExtractChangedFiles(string output);
}

public record AnalysisResult(
    bool IsComplete,
    bool TestsPassing,
    bool HasErrors,
    IEnumerable<string> ChangedFiles,
    string? ErrorMessage
);

public record PlanResult(
    string Plan,
    IEnumerable<string> Tasks
);

public interface ICircuitBreaker
{
    CircuitBreakerState State { get; }
    bool CanExecute();
    void RecordSuccess();
    void RecordFailure();
    void Reset();
}

public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

public interface IRateLimiter
{
    Task WaitForAvailabilityAsync(CancellationToken cancellationToken = default);
    void RecordRequest();
    int GetRemainingRequests();
}
