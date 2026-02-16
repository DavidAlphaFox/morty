using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Morty.Entities;
using Morty.Interfaces;
using Morty.Repositories;
using Morty.DTOs;
using Morty.Hubs;
using Serilog;

namespace Morty.Services;

/// <summary>
/// Morty 循环服务 - 后台服务，编排整个开发循环
/// 支持多阶段处理：计划分析、验收标准、编码、测试、验收
/// 使用两个独立信号量实现 Planning 与 Execution 的并行调度
///
/// 信号量语义：
/// - _planningSemaphore / _executionSemaphore：互斥锁，保证同一时刻各队列只有一个故事在处理
/// - _planningNotify / _executionNotify：通知信号量（初始值 0），Release 表示有新任务到达
///   WhenAny(Delay, Notify) 实现"延迟或新任务到达时立即唤醒"
/// </summary>
public class MortyLoopService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClaudeProvider _provider;
    private readonly IResponseAnalyzer _responseAnalyzer;
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly IRateLimiter _rateLimiter;
    private readonly PhasePromptBuilder _promptBuilder;
    private readonly PhaseResultHandler _resultHandler;
    private readonly StoryRecoveryService _recoveryService;
    private readonly Serilog.ILogger _logger;
    private readonly TimeSpan _delayBetweenIterations;
    private readonly int _maxIterationsPerPhase;

    /// <summary>Planning 队列信号量 - 保证同一时刻只有一个 Planning 在运行</summary>
    private readonly SemaphoreSlim _planningSemaphore = new(1, 1);
    /// <summary>Execution 队列信号量 - 保证同一时刻只有一个 Executing/Testing/Acceptance 在运行</summary>
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);

    /// <summary>通知 Planning 循环立即检查新任务（初始值 0，Release = 新任务到达）</summary>
    private readonly SemaphoreSlim _planningNotify = new(0, 1);
    /// <summary>通知 Execution 循环立即检查新任务（初始值 0，Release = 新任务到达）</summary>
    private readonly SemaphoreSlim _executionNotify = new(0, 1);

    private bool _isRunning;

    public MortyLoopService(
        IServiceScopeFactory scopeFactory,
        IClaudeProvider provider,
        IResponseAnalyzer responseAnalyzer,
        ICircuitBreaker circuitBreaker,
        IRateLimiter rateLimiter,
        PhasePromptBuilder promptBuilder,
        PhaseResultHandler resultHandler,
        StoryRecoveryService recoveryService,
        Serilog.ILogger? logger = null,
        int maxIterationsPerPhase = 10,
        int delaySeconds = 5)
    {
        _scopeFactory = scopeFactory;
        _provider = provider;
        _responseAnalyzer = responseAnalyzer;
        _circuitBreaker = circuitBreaker;
        _rateLimiter = rateLimiter;
        _promptBuilder = promptBuilder;
        _resultHandler = resultHandler;
        _recoveryService = recoveryService;
        _logger = logger ?? Log.Logger;
        _maxIterationsPerPhase = maxIterationsPerPhase;
        _delayBetweenIterations = TimeSpan.FromSeconds(delaySeconds);
    }

    public bool IsRunning => _isRunning;

    /// <summary>
    /// 通知循环立即检查新任务（由外部调用，如 Start/Pause 接口）
    /// </summary>
    public void NotifyNewWork()
    {
        try { _planningNotify.Release(); } catch (SemaphoreFullException) { }
        try { _executionNotify.Release(); } catch (SemaphoreFullException) { }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _isRunning = true;
        _logger.Information("MortyLoopService 启动中...");

        // 启动时恢复中断的故事
        await _recoveryService.RecoverAsync(stoppingToken);

        // 创建两个并行任务：Planning 循环和 Execution 循环（共用 RunQueueLoopAsync）
        var planningTask = RunQueueLoopAsync(
            StoryQueueType.Planning, _planningSemaphore, _planningNotify, "Planning", stoppingToken);
        var executionTask = RunQueueLoopAsync(
            StoryQueueType.Execution, _executionSemaphore, _executionNotify, "Execution", stoppingToken);

        await Task.WhenAll(planningTask, executionTask);

        _isRunning = false;
        _logger.Information("MortyLoopService 已停止");
    }

    /// <summary>
    /// 通用队列处理循环 - Planning 和 Execution 共用同一循环结构
    /// 流程：检查断路器 → 获取信号量 → 处理故事 → 释放信号量 → 等待通知或延迟
    /// </summary>
    private async Task RunQueueLoopAsync(
        StoryQueueType queueType,
        SemaphoreSlim semaphore,
        SemaphoreSlim notifySemaphore,
        string loopName,
        CancellationToken ct)
    {
        _logger.Information("{LoopName} 循环启动", loopName);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!_circuitBreaker.CanExecute())
                {
                    _logger.Warning("断路器已打开，{LoopName} 循环等待中...", loopName);
                    await Task.Delay(TimeSpan.FromMinutes(1), ct);
                    continue;
                }

                await semaphore.WaitAsync(ct);
                try
                {
                    await ProcessNextStoryAsync(queueType, ct);
                }
                finally
                {
                    semaphore.Release();
                }

                // 等待延迟或新任务通知（哪个先到就立即继续）
                await Task.WhenAny(
                    Task.Delay(_delayBetweenIterations, ct),
                    notifySemaphore.WaitAsync(ct)
                );
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.Information("{LoopName} 循环停止中...", loopName);
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "{LoopName} 循环错误", loopName);
                _circuitBreaker.RecordFailure();
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }

        _logger.Information("{LoopName} 循环已停止", loopName);
    }

    /// <summary>
    /// 处理下一个待处理的故事
    /// 流程分段：
    /// 1. 获取候选故事（按优先级，检查依赖）
    /// 2. 初始化阶段（Pending → RequirementsPlanning）
    /// 3. 获取上下文（DetailedPlan、AcceptanceCriteria）
    /// 4. 构建提示词并调用 AI
    /// 5. 保存结果并处理阶段转换
    /// 6. 广播 SignalR 通知
    /// </summary>
    private async Task ProcessNextStoryAsync(StoryQueueType queueType, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var storyRepo = scope.ServiceProvider.GetRequiredService<IStoryRepository>();
        var projectRepo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var iterationRepo = scope.ServiceProvider.GetRequiredService<IIterationRepository>();
        var planRepo = scope.ServiceProvider.GetRequiredService<IPlanRepository>();
        var verificationRepo = scope.ServiceProvider.GetRequiredService<IVerificationRepository>();
        var storyEventRepo = scope.ServiceProvider.GetRequiredService<IStoryEventRepository>();
        var executionOutputRepo = scope.ServiceProvider.GetRequiredService<IExecutionOutputRepository>();
        var phaseHistoryRepo = scope.ServiceProvider.GetRequiredService<IPhaseHistoryRepository>();
        var storyDependencyRepo = scope.ServiceProvider.GetRequiredService<IStoryDependencyRepository>();

        // === 1. 获取候选故事（按优先级排序），遍历找到第一个依赖已满足的 ===
        var candidates = await storyRepo.GetPendingByQueueTypeAsync(queueType, ct);

        if (candidates.Count == 0)
        {
            _logger.Debug("队列 {QueueType} 没有待处理的故事", queueType);
            return;
        }

        Story? story = null;
        foreach (var candidate in candidates)
        {
            var satisfied = await storyDependencyRepo.AreDependenciesSatisfiedAsync(candidate.Id, ct);
            if (satisfied)
            {
                story = candidate;
                break;
            }
            _logger.Debug("故事 {StoryId} 的依赖尚未满足，尝试下一个", candidate.StoryId);
        }

        if (story == null)
        {
            _logger.Debug("队列 {QueueType} 所有候选故事的依赖均未满足", queueType);
            return;
        }

        // === 2. 初始化阶段（Pending 状态自动进入 RequirementsPlanning） ===
        if (story.Phase == StoryPhase.Pending && story.Status == "Pending")
        {
            if (queueType == StoryQueueType.Planning)
            {
                story.Phase = StoryPhase.RequirementsPlanning;
                story.Status = "Planning";
            }
            else
            {
                _logger.Debug("故事 {StoryId} 处于 Pending 状态，不在 Execution 队列处理范围", story.StoryId);
                return;
            }
            await storyRepo.UpdateAsync(story, ct);
        }

        // 设置故事为运行中状态
        story.RunningStatus = RunningStatus.Running;
        await storyRepo.UpdateAsync(story, ct);

        _logger.Information("正在处理故事 {StoryId}: {Title}, 阶段: {Phase}",
            story.StoryId, story.Title, story.Phase);

        // === 3. 获取项目信息和上下文 ===
        var project = await projectRepo.GetByIdAsync(story.ProjectId, ct);
        if (project == null)
        {
            _logger.Error("故事 {StoryId} 对应的项目未找到", story.StoryId);
            story.Status = "Failed";
            story.Phase = StoryPhase.Failed;
            await storyRepo.UpdateAsync(story, ct);
            return;
        }

        if (!string.IsNullOrEmpty(project.WorkingDirectory) && !Directory.Exists(project.WorkingDirectory))
        {
            _logger.Information("创建工作目录: {WorkingDirectory}", project.WorkingDirectory);
            Directory.CreateDirectory(project.WorkingDirectory);
        }

        if (story.CurrentIteration >= _maxIterationsPerPhase)
        {
            _logger.Warning("故事 {StoryId} 阶段 {Phase} 达到最大迭代次数",
                story.StoryId, story.Phase);
            story.Status = "Failed";
            story.Phase = StoryPhase.Failed;
            await storyRepo.UpdateAsync(story, ct);
            return;
        }

        // 从 Plan 表获取之前阶段生成的内容
        string? detailedPlan = null;
        string? acceptanceCriteria = null;

        if (story.Phase != StoryPhase.RequirementsPlanning)
        {
            var detailedPlanRecord = await planRepo.GetLatestByStoryIdAndTypeAsync(
                story.Id, PlanType.DetailedPlan, ct);
            detailedPlan = detailedPlanRecord?.PlanContent;
        }

        if (story.Phase != StoryPhase.RequirementsPlanning && story.Phase != StoryPhase.AcceptancePlanning)
        {
            var acceptanceCriteriaRecord = await planRepo.GetLatestByStoryIdAndTypeAsync(
                story.Id, PlanType.AcceptanceCriteria, ct);
            acceptanceCriteria = acceptanceCriteriaRecord?.PlanContent;
        }

        // === 4. 构建提示词并调用 AI ===
        await _rateLimiter.WaitForAvailabilityAsync(ct);
        _rateLimiter.RecordRequest();

        var phaseHistory = new PhaseHistory
        {
            StoryId = story.Id,
            Round = story.Round,
            Phase = story.Phase,
            StartedAt = DateTime.UtcNow
        };
        await phaseHistoryRepo.AddAsync(phaseHistory, ct);

        var iterationCount = await storyRepo.GetIterationCountAsync(story.Id, ct);
        var iteration = new Iteration
        {
            StoryId = story.Id,
            IterationNum = iterationCount + 1,
            Round = story.Round,
            StartedAt = DateTime.UtcNow
        };
        await iterationRepo.AddAsync(iteration, ct);

        try
        {
            var (prompt, usePlanMode) = _promptBuilder.Build(
                project.PrdJson, story, detailedPlan, acceptanceCriteria);

            _logger.Information("使用 {Provider} 处理阶段 {Phase}, PlanMode: {UsePlanMode}, 工作目录: {WorkingDir}",
                _provider.Name, story.Phase, usePlanMode, project.WorkingDirectory);

            var systemPrompt = $"""
                注意：你只能在当前目录 ({project.WorkingDirectory}) 及其子目录下工作，不能访问上级目录或目录外的任何文件。
                如果需要访问文件，只能使用相对路径或此目录下的绝对路径。
                """;

            var request = new ProviderRequest(
                prompt,
                SystemPrompt: systemPrompt,
                UsePlanMode: usePlanMode,
                WorkingDirectory: project.WorkingDirectory
            );
            var response = await _provider.SendMessageAsync(request, ct);

            // === 5. 保存结果并处理阶段转换 ===
            iteration.CompletedAt = DateTime.UtcNow;
            iteration.DurationMs = (long)(iteration.CompletedAt.Value - iteration.StartedAt).TotalMilliseconds;
            iteration.Output = response.Content;
            iteration.CostUsd = response.CostUsd;
            await iterationRepo.UpdateAsync(iteration, ct);

            var executionOutput = new ExecutionOutput
            {
                IterationId = iteration.Id,
                Prompt = prompt,
                Response = response.Content,
                ParsedOutput = response.Content,
                DurationMs = iteration.DurationMs.HasValue ? (int)iteration.DurationMs.Value : null,
                CostUsd = response.CostUsd,
                CreatedAt = DateTime.UtcNow
            };
            await executionOutputRepo.AddAsync(executionOutput, ct);

            // === 6. 广播 SignalR 通知 ===
            var iterationDto = new IterationDto
            {
                Id = iteration.Id,
                StoryId = iteration.StoryId,
                IterationNum = iteration.IterationNum,
                Round = iteration.Round,
                StartedAt = iteration.StartedAt,
                CompletedAt = iteration.CompletedAt,
                DurationMs = iteration.DurationMs,
                Output = iteration.Output
            };
            await MortyHub.Broadcaster.NotifyIterationComplete(iterationDto);

            var analysis = _responseAnalyzer.Analyze(response.Content, response.Success);

            var verification = new Verification
            {
                IterationId = iteration.Id,
                Type = "build",
                Passed = !analysis.HasErrors,
                Output = response.Error ?? response.Content,
                CreatedAt = DateTime.UtcNow
            };
            await verificationRepo.AddAsync(verification, ct);

            var phaseSuccess = await _resultHandler.HandleAsync(
                story, response.Content, response.Success, analysis,
                planRepo, storyRepo, storyDependencyRepo, ct);

            phaseHistory.CompletedAt = DateTime.UtcNow;
            phaseHistory.Output = response.Content;
            phaseHistory.Success = phaseSuccess;
            await phaseHistoryRepo.UpdateAsync(phaseHistory, ct);

            _logger.Information("阶段处理完成: phaseSuccess={PhaseSuccess}, phase={Phase}", phaseSuccess, story.Phase);

            if (phaseSuccess)
            {
                _logger.Information("阶段成功，转换到下一阶段，当前阶段: {Phase}", story.Phase);
                await _resultHandler.TransitionToNextPhaseAsync(story, storyRepo, ct);
            }
            else
            {
                _logger.Information("阶段失败，增加迭代次数，当前阶段: {Phase}", story.Phase);
                story.CurrentIteration++;
                story.RunningStatus = RunningStatus.Pending;
                _circuitBreaker.RecordFailure();
                await storyRepo.UpdateAsync(story, ct);
            }

            var storyEvent = new StoryEvent
            {
                StoryId = story.Id,
                Round = story.Round,
                EventType = "iteration_complete",
                Timestamp = DateTime.UtcNow,
                DataJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    iteration = iteration.IterationNum,
                    phase = story.Phase,
                    complete = analysis.IsComplete,
                    errors = analysis.HasErrors,
                    provider = _provider.Name,
                    success = phaseSuccess
                })
            };
            await storyEventRepo.AddAsync(storyEvent, ct);

            // 广播故事更新（包含从 Plan 表获取的最新内容）
            var latestDetailedPlan = await planRepo.GetLatestByStoryIdAndTypeAsync(
                story.Id, PlanType.DetailedPlan, ct);
            var latestAcceptanceCriteria = await planRepo.GetLatestByStoryIdAndTypeAsync(
                story.Id, PlanType.AcceptanceCriteria, ct);

            var storyDto = new StoryDto
            {
                Id = story.Id,
                ProjectId = story.ProjectId,
                StoryId = story.StoryId,
                Title = story.Title,
                Priority = story.Priority,
                Status = story.Status,
                CreatedAt = story.CreatedAt,
                CompletedAt = story.CompletedAt,
                RunningStatus = story.RunningStatus,
                Source = story.Source,
                Phase = story.Phase,
                Requirements = story.Requirements,
                DetailedPlan = latestDetailedPlan?.PlanContent,
                UserAcceptanceCriteria = story.UserAcceptanceCriteria,
                AcceptanceCriteria = latestAcceptanceCriteria?.PlanContent,
                CurrentIteration = story.CurrentIteration,
                Round = story.Round
            };
            await MortyHub.Broadcaster.NotifyStoryUpdated(storyDto);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "处理故事 {StoryId} 阶段 {Phase} 时出错", story.StoryId, story.Phase);
            iteration.Output = ex.Message;
            await iterationRepo.UpdateAsync(iteration, ct);

            phaseHistory.CompletedAt = DateTime.UtcNow;
            phaseHistory.Output = ex.Message;
            phaseHistory.Success = false;
            await phaseHistoryRepo.UpdateAsync(phaseHistory, ct);

            story.Status = "Failed";
            story.Phase = StoryPhase.Failed;
            _circuitBreaker.RecordFailure();
            await storyRepo.UpdateAsync(story, ct);
        }
    }
}
