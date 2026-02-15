using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Morty.Core.Entities;
using Morty.Core.Interfaces;
using Morty.Core.Repositories;
using Morty.Web.DTOs;
using Morty.Web.Hubs;
using Serilog;

namespace Morty.Web.Services;

/// <summary>
/// Morty 循环服务 - 后台服务，编排整个开发循环
/// 支持多阶段处理：计划分析、验收标准、编码、测试、验收
/// </summary>
public class MortyLoopService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClaudeProvider _provider;
    private readonly IResponseAnalyzer _responseAnalyzer;
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly IRateLimiter _rateLimiter;
    private readonly Serilog.ILogger _logger;
    private readonly TimeSpan _delayBetweenIterations;
    private readonly int _maxIterationsPerPhase;

    private bool _isRunning;

    public MortyLoopService(
        IServiceScopeFactory scopeFactory,
        IClaudeProvider provider,
        IResponseAnalyzer responseAnalyzer,
        ICircuitBreaker circuitBreaker,
        IRateLimiter rateLimiter,
        Serilog.ILogger? logger = null,
        int maxIterationsPerPhase = 10,
        int delaySeconds = 5)
    {
        _scopeFactory = scopeFactory;
        _provider = provider;
        _responseAnalyzer = responseAnalyzer;
        _circuitBreaker = circuitBreaker;
        _rateLimiter = rateLimiter;
        _logger = logger ?? Log.Logger;
        _maxIterationsPerPhase = maxIterationsPerPhase;
        _delayBetweenIterations = TimeSpan.FromSeconds(delaySeconds);
    }

    public bool IsRunning => _isRunning;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _isRunning = true;
        _logger.Information("MortyLoopService 启动中...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_circuitBreaker.CanExecute())
                {
                    _logger.Warning("断路器已打开，等待中...");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                await ProcessNextStoryAsync(stoppingToken);

                await Task.Delay(_delayBetweenIterations, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.Information("MortyLoopService 停止中...");
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MortyLoopService 错误");
                _circuitBreaker.RecordFailure();
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _isRunning = false;
    }

    /// <summary>
    /// 处理下一个待处理的故事
    /// </summary>
    private async Task ProcessNextStoryAsync(CancellationToken stoppingToken)
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

        // 获取下一个待处理的故事（支持多阶段），并检查依赖关系
        var story = await storyRepo.GetNextPendingAsync(stoppingToken);

        if (story == null)
        {
            _logger.Debug("没有待处理的故事");
            return;
        }

        // 检查依赖是否已满足
        var dependenciesSatisfied = await storyDependencyRepo.AreDependenciesSatisfiedAsync(story.Id, stoppingToken);
        if (!dependenciesSatisfied)
        {
            _logger.Debug("故事 {StoryId} 的依赖尚未满足，跳过处理", story.StoryId);
            return;
        }

        // 如果故事还没有设置阶段但处于 Pending 状态，自动进入 RequirementsPlanning
        if (story.Phase == StoryPhase.Pending && story.Status == "Pending")
        {
            story.Phase = StoryPhase.RequirementsPlanning;
            story.Status = "Planning";
            await storyRepo.UpdateAsync(story, stoppingToken);
        }

        _logger.Information("正在处理故事 {StoryId}: {Title}, 阶段: {Phase}",
            story.StoryId, story.Title, story.Phase);

        // 获取项目信息
        var project = await projectRepo.GetByIdAsync(story.ProjectId, stoppingToken);
        if (project == null)
        {
            _logger.Error("故事 {StoryId} 对应的项目未找到", story.StoryId);
            story.Status = "Failed";
            story.Phase = StoryPhase.Failed;
            await storyRepo.UpdateAsync(story, stoppingToken);
            return;
        }

        // 确保工作目录存在
        if (!string.IsNullOrEmpty(project.WorkingDirectory) && !Directory.Exists(project.WorkingDirectory))
        {
            _logger.Information("创建工作目录: {WorkingDirectory}", project.WorkingDirectory);
            Directory.CreateDirectory(project.WorkingDirectory);
        }

        // 检查阶段迭代次数
        if (story.CurrentIteration >= _maxIterationsPerPhase)
        {
            _logger.Warning("故事 {StoryId} 阶段 {Phase} 达到最大迭代次数",
                story.StoryId, story.Phase);
            story.Status = "Failed";
            story.Phase = StoryPhase.Failed;
            await storyRepo.UpdateAsync(story, stoppingToken);
            return;
        }

        // 等待速率限制器
        await _rateLimiter.WaitForAvailabilityAsync(stoppingToken);
        _rateLimiter.RecordRequest();

        // 创建阶段历史记录
        var phaseHistory = new PhaseHistory
        {
            StoryId = story.Id,
            Phase = story.Phase,
            StartedAt = DateTime.UtcNow
        };
        await phaseHistoryRepo.AddAsync(phaseHistory, stoppingToken);

        // 创建迭代
        var iterationCount = await storyRepo.GetIterationCountAsync(story.Id, stoppingToken);
        var iteration = new Iteration
        {
            StoryId = story.Id,
            IterationNum = iterationCount + 1,
            StartedAt = DateTime.UtcNow
        };
        await iterationRepo.AddAsync(iteration, stoppingToken);

        try
        {
            // 根据当前阶段构建提示词并调用 Claude
            var (prompt, usePlanMode) = BuildPhasePrompt(project.PrdJson, story);

            _logger.Information("使用 {Provider} 处理阶段 {Phase}, PlanMode: {UsePlanMode}",
                _provider.Name, story.Phase, usePlanMode);

            var request = new ProviderRequest(prompt, UsePlanMode: usePlanMode);
            var response = await _provider.SendMessageAsync(request, stoppingToken);

            iteration.CompletedAt = DateTime.UtcNow;
            iteration.DurationMs = (long)(iteration.CompletedAt.Value - iteration.StartedAt).TotalMilliseconds;
            iteration.Output = response.Content;
            iteration.CostUsd = response.CostUsd;
            await iterationRepo.UpdateAsync(iteration, stoppingToken);

            // 保存执行输出
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
            await executionOutputRepo.AddAsync(executionOutput, stoppingToken);

            // 通过 SignalR 广播迭代完成
            var iterationDto = new IterationDto
            {
                Id = iteration.Id,
                StoryId = iteration.StoryId,
                IterationNum = iteration.IterationNum,
                StartedAt = iteration.StartedAt,
                CompletedAt = iteration.CompletedAt,
                DurationMs = iteration.DurationMs,
                Output = iteration.Output
            };
            await MortyHub.Broadcaster.NotifyIterationComplete(iterationDto);

            // 分析响应
            var analysis = _responseAnalyzer.Analyze(response.Content);

            // 记录验证
            var verification = new Verification
            {
                IterationId = iteration.Id,
                Type = "build",
                Passed = !analysis.HasErrors,
                Output = response.Error ?? response.Content,
                CreatedAt = DateTime.UtcNow
            };
            await verificationRepo.AddAsync(verification, stoppingToken);

            // 根据阶段处理结果
            var phaseSuccess = await HandlePhaseResultAsync(
                story, response.Content, project.PrdJson, analysis,
                planRepo, stoppingToken);

            // 更新阶段历史
            phaseHistory.CompletedAt = DateTime.UtcNow;
            phaseHistory.Output = response.Content;
            phaseHistory.Success = phaseSuccess;
            await phaseHistoryRepo.UpdateAsync(phaseHistory, stoppingToken);

            // 根据阶段结果更新故事状态
            if (phaseSuccess)
            {
                await TransitionToNextPhaseAsync(story, storyRepo, stoppingToken);
            }
            else
            {
                story.CurrentIteration++;
                story.Status = analysis.IsComplete ? "Completed" : "InProgress";
                _circuitBreaker.RecordFailure();
                await storyRepo.UpdateAsync(story, stoppingToken);
            }

            // 记录事件
            var storyEvent = new StoryEvent
            {
                StoryId = story.Id,
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
            await storyEventRepo.AddAsync(storyEvent, stoppingToken);

            // 通过 SignalR 广播故事更新
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
                IsPaused = story.IsPaused,
                Source = story.Source,
                Phase = story.Phase,
                Requirements = story.Requirements,
                DetailedPlan = story.DetailedPlan,
                UserAcceptanceCriteria = story.UserAcceptanceCriteria,
                AcceptanceCriteria = story.AcceptanceCriteria,
                CurrentIteration = story.CurrentIteration
            };
            await MortyHub.Broadcaster.NotifyStoryUpdated(storyDto);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "处理故事 {StoryId} 阶段 {Phase} 时出错", story.StoryId, story.Phase);
            iteration.Output = ex.Message;
            await iterationRepo.UpdateAsync(iteration, stoppingToken);

            // 更新阶段历史为失败
            phaseHistory.CompletedAt = DateTime.UtcNow;
            phaseHistory.Output = ex.Message;
            phaseHistory.Success = false;
            await phaseHistoryRepo.UpdateAsync(phaseHistory, stoppingToken);

            story.Status = "Failed";
            story.Phase = StoryPhase.Failed;
            _circuitBreaker.RecordFailure();
            await storyRepo.UpdateAsync(story, stoppingToken);
        }
    }

    /// <summary>
    /// 根据当前阶段构建提示词
    /// </summary>
    private static (string prompt, bool usePlanMode) BuildPhasePrompt(string prdJson, Story story)
    {
        var usePlanMode = story.Phase switch
        {
            StoryPhase.RequirementsPlanning => true,
            StoryPhase.AcceptancePlanning => true,
            StoryPhase.Acceptance => false, // 验收阶段使用普通模式
            _ => false
        };

        var prompt = story.Phase switch
        {
            StoryPhase.RequirementsPlanning => $"""
                请分析以下用户故事需求，并生成详细的实施计划。

                用户故事: {story.Title}
                故事 ID: {story.StoryId}

                原始需求:
                {story.Requirements}

                项目 PRD:
                {prdJson}

                请生成详细的实施计划，包括：
                1. 需要实现的功能
                2. 需要修改或创建的文件
                3. 实现步骤
                4. 潜在的技术难点
                """,

            StoryPhase.AcceptancePlanning => $"""
                请根据以下用户故事、需求和原始验收标准，细化验收标准。

                用户故事: {story.Title}
                故事 ID: {story.StoryId}

                详细实施计划:
                {story.DetailedPlan}

                用户提供的验收标准:
                {story.UserAcceptanceCriteria}

                请基于以上信息生成细化的验收标准，包括：
                1. 功能验收标准（具体、可测试）
                2. 非功能验收标准（如性能、安全等）
                3. 边界条件和异常情况
                """,

            StoryPhase.Coding => $"""
                请根据以下详细计划实施代码。

                用户故事: {story.Title}
                故事 ID: {story.StoryId}

                详细实施计划:
                {story.DetailedPlan}

                请实现更改并回复:
                1. 您做了什么
                2. 修改了哪些文件
                3. 实现是否完成或还有什么待完成
                """,

            StoryPhase.Testing => $"""
                请为以下已实现的代码生成单元测试。

                用户故事: {story.Title}
                故事 ID: {story.StoryId}

                详细实施计划:
                {story.DetailedPlan}

                验收标准:
                {story.AcceptanceCriteria}

                请生成单元测试代码，确保：
                1. 测试覆盖验收标准中的所有功能点
                2. 测试代码能够编译和运行
                3. 包含边界条件的测试用例
                """,

            StoryPhase.Acceptance => $"""
                请根据以下验收标准验证实现是否满足要求。

                用户故事: {story.Title}
                故事 ID: {story.StoryId}

                验收标准:
                {story.AcceptanceCriteria}

                请验证实现并回复：
                1. 每个验收标准是否满足
                2. 如不满足，说明原因
                3. 总体评估：是否通过验收
                """,

            _ => $"""
                用户故事: {story.Title}
                故事 ID: {story.StoryId}

                需求 (PRD):
                {prdJson}
                """
        };

        return (prompt, usePlanMode);
    }

    /// <summary>
    /// 处理阶段结果
    /// </summary>
    private async Task<bool> HandlePhaseResultAsync(
        Story story,
        string responseContent,
        string prdJson,
        AnalysisResult analysis,
        IPlanRepository planRepo,
        CancellationToken stoppingToken)
    {
        var planType = story.Phase switch
        {
            StoryPhase.RequirementsPlanning => PlanType.RequirementsPlanning,
            StoryPhase.AcceptancePlanning => PlanType.AcceptancePlanning,
            StoryPhase.Coding => PlanType.Coding,
            StoryPhase.Testing => PlanType.Testing,
            StoryPhase.Acceptance => PlanType.Acceptance,
            _ => PlanType.Execution
        };

        // 保存计划记录
        var plan = new Plan
        {
            StoryId = story.Id,
            PlanContent = story.Phase switch
            {
                StoryPhase.RequirementsPlanning => story.DetailedPlan,
                StoryPhase.AcceptancePlanning => story.AcceptanceCriteria,
                _ => $"阶段 {story.Phase} 执行"
            },
            Type = planType,
            Output = responseContent,
            CreatedAt = DateTime.UtcNow
        };
        await planRepo.AddAsync(plan, stoppingToken);

        // 根据阶段类型处理结果
        return story.Phase switch
        {
            StoryPhase.RequirementsPlanning =>
                await HandleRequirementsPlanningResultAsync(story, responseContent, stoppingToken),

            StoryPhase.AcceptancePlanning =>
                await HandleAcceptancePlanningResultAsync(story, responseContent, stoppingToken),

            StoryPhase.Coding => !analysis.HasErrors,

            StoryPhase.Testing => !analysis.HasErrors,

            StoryPhase.Acceptance => analysis.IsComplete,

            _ => analysis.IsComplete
        };
    }

    /// <summary>
    /// 处理需求计划阶段结果
    /// </summary>
    private async Task<bool> HandleRequirementsPlanningResultAsync(
        Story story,
        string responseContent,
        CancellationToken stoppingToken)
    {
        // 从响应中提取计划
        var planResult = _responseAnalyzer.ExtractPlan(responseContent);
        if (planResult != null)
        {
            story.DetailedPlan = planResult.Plan;
        }
        else
        {
            // 如果无法提取完整计划，使用原始响应
            story.DetailedPlan = responseContent;
        }

        return !string.IsNullOrEmpty(story.DetailedPlan);
    }

    /// <summary>
    /// 处理验收标准计划阶段结果
    /// </summary>
    private async Task<bool> HandleAcceptancePlanningResultAsync(
        Story story,
        string responseContent,
        CancellationToken stoppingToken)
    {
        // 从响应中提取验收标准
        var planResult = _responseAnalyzer.ExtractPlan(responseContent);
        if (planResult != null)
        {
            story.AcceptanceCriteria = planResult.Plan;
        }
        else
        {
            // 如果无法提取完整标准，使用原始响应
            story.AcceptanceCriteria = responseContent;
        }

        return !string.IsNullOrEmpty(story.AcceptanceCriteria);
    }

    /// <summary>
    /// 转换到下一阶段
    /// </summary>
    private async Task TransitionToNextPhaseAsync(
        Story story,
        IStoryRepository storyRepo,
        CancellationToken stoppingToken)
    {
        var nextPhase = story.Phase switch
        {
            StoryPhase.RequirementsPlanning => StoryPhase.AcceptancePlanning,
            StoryPhase.AcceptancePlanning => StoryPhase.Coding,
            StoryPhase.Coding => StoryPhase.Testing,
            StoryPhase.Testing => StoryPhase.Acceptance,
            StoryPhase.Acceptance => StoryPhase.Completed,
            _ => story.Phase
        };

        story.Phase = nextPhase;
        story.CurrentIteration = 0;

        // 根据阶段设置状态
        switch (nextPhase)
        {
            case StoryPhase.RequirementsPlanning:
            case StoryPhase.AcceptancePlanning:
                story.Status = "Planning";
                break;
            case StoryPhase.Coding:
            case StoryPhase.Testing:
                story.Status = "InProgress";
                break;
            case StoryPhase.Acceptance:
                story.Status = "Verifying";
                break;
            case StoryPhase.Completed:
                story.Status = "Completed";
                story.CompletedAt = DateTime.UtcNow;
                _logger.Information("故事 {StoryId} 成功完成所有阶段", story.StoryId);
                break;
        }

        await storyRepo.UpdateAsync(story, stoppingToken);
        _logger.Information("故事 {StoryId} 阶段转换: {From} -> {To}",
            story.StoryId, story.Phase, nextPhase);
    }
}
