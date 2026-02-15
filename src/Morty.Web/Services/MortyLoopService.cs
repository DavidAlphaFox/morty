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
/// 使用两个独立信号量实现 Planning 与 Execution 的并行调度
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

    /// <summary>Planning 队列信号量 - 保证同一时刻只有一个 Planning 在运行</summary>
    private readonly SemaphoreSlim _planningSemaphore = new(1, 1);
    /// <summary>Execution 队列信号量 - 保证同一时刻只有一个 Coding/Testing/Acceptance 在运行</summary>
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);

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

        // 创建两个并行任务：Planning 循环和 Execution 循环
        var planningTask = RunPlanningLoopAsync(stoppingToken);
        var executionTask = RunExecutionLoopAsync(stoppingToken);

        await Task.WhenAll(planningTask, executionTask);

        _isRunning = false;
        _logger.Information("MortyLoopService 已停止");
    }

    /// <summary>
    /// Planning 循环 - 处理 RequirementsPlanning 和 AcceptancePlanning 阶段
    /// </summary>
    private async Task RunPlanningLoopAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Planning 循环启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_circuitBreaker.CanExecute())
                {
                    _logger.Warning("断路器已打开，Planning 循环等待中...");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                await _planningSemaphore.WaitAsync(stoppingToken);
                try
                {
                    await ProcessNextStoryAsync(StoryQueueType.Planning, stoppingToken);
                }
                finally
                {
                    _planningSemaphore.Release();
                }

                await Task.Delay(_delayBetweenIterations, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.Information("Planning 循环停止中...");
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Planning 循环错误");
                _circuitBreaker.RecordFailure();
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.Information("Planning 循环已停止");
    }

    /// <summary>
    /// Execution 循环 - 处理 Coding、Testing 和 Acceptance 阶段
    /// </summary>
    private async Task RunExecutionLoopAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Execution 循环启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_circuitBreaker.CanExecute())
                {
                    _logger.Warning("断路器已打开，Execution 循环等待中...");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                await _executionSemaphore.WaitAsync(stoppingToken);
                try
                {
                    await ProcessNextStoryAsync(StoryQueueType.Execution, stoppingToken);
                }
                finally
                {
                    _executionSemaphore.Release();
                }

                await Task.Delay(_delayBetweenIterations, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.Information("Execution 循环停止中...");
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Execution 循环错误");
                _circuitBreaker.RecordFailure();
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.Information("Execution 循环已停止");
    }

    /// <summary>
    /// 处理下一个待处理的故事
    /// </summary>
    /// <param name="queueType">队列类型（Planning 或 Execution）</param>
    private async Task ProcessNextStoryAsync(StoryQueueType queueType, CancellationToken stoppingToken)
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

        // 获取下一个待处理的故事（按队列类型筛选）
        var story = await storyRepo.GetNextPendingByQueueTypeAsync(queueType, stoppingToken);

        if (story == null)
        {
            _logger.Debug("队列 {QueueType} 没有待处理的故事", queueType);
            return;
        }

        // 检查依赖是否已满足
        var dependenciesSatisfied = await storyDependencyRepo.AreDependenciesSatisfiedAsync(story.Id, stoppingToken);
        if (!dependenciesSatisfied)
        {
            _logger.Debug("故事 {StoryId} 的依赖尚未满足，跳过处理", story.StoryId);
            return;
        }

        // 如果故事还没有设置阶段但处于 Pending 状态，根据队列类型自动进入对应阶段
        if (story.Phase == StoryPhase.Pending && story.Status == "Pending")
        {
            if (queueType == StoryQueueType.Planning)
            {
                story.Phase = StoryPhase.RequirementsPlanning;
                story.Status = "Planning";
            }
            else
            {
                // Execution 队列不应处理 Pending 故事，跳过
                _logger.Debug("故事 {StoryId} 处于 Pending 状态，不在 Execution 队列处理范围", story.StoryId);
                return;
            }
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

        // 从 Plan 表获取之前阶段生成的内容
        string? detailedPlan = null;
        string? acceptanceCriteria = null;

        if (story.Phase != StoryPhase.RequirementsPlanning)
        {
            var detailedPlanRecord = await planRepo.GetLatestByStoryIdAndTypeAsync(
                story.Id, PlanType.DetailedPlan, stoppingToken);
            detailedPlan = detailedPlanRecord?.PlanContent;
        }

        if (story.Phase != StoryPhase.RequirementsPlanning && story.Phase != StoryPhase.AcceptancePlanning)
        {
            var acceptanceCriteriaRecord = await planRepo.GetLatestByStoryIdAndTypeAsync(
                story.Id, PlanType.AcceptanceCriteria, stoppingToken);
            acceptanceCriteria = acceptanceCriteriaRecord?.PlanContent;
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
            var (prompt, usePlanMode) = BuildPhasePrompt(
                project.PrdJson, story, detailedPlan, acceptanceCriteria);

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

            // 根据阶段处理结果并保存到 Plan 表
            var phaseSuccess = await HandlePhaseResultAsync(
                story, response.Content, project.PrdJson, analysis,
                planRepo, storyRepo, storyDependencyRepo, detailedPlan, acceptanceCriteria, stoppingToken);

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

            // 通过 SignalR 广播故事更新（包含从 Plan 表获取的内容）
            var latestDetailedPlan = await planRepo.GetLatestByStoryIdAndTypeAsync(
                story.Id, PlanType.DetailedPlan, stoppingToken);
            var latestAcceptanceCriteria = await planRepo.GetLatestByStoryIdAndTypeAsync(
                story.Id, PlanType.AcceptanceCriteria, stoppingToken);

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
                DetailedPlan = latestDetailedPlan?.PlanContent,
                UserAcceptanceCriteria = story.UserAcceptanceCriteria,
                AcceptanceCriteria = latestAcceptanceCriteria?.PlanContent,
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
    private static (string prompt, bool usePlanMode) BuildPhasePrompt(
        string prdJson,
        Story story,
        string? detailedPlan,
        string? acceptanceCriteria)
    {
        var usePlanMode = story.Phase switch
        {
            StoryPhase.RequirementsPlanning => true,
            StoryPhase.AcceptancePlanning => true,
            StoryPhase.Acceptance => false,
            _ => false
        };

        var prompt = story.Phase switch
        {
            StoryPhase.RequirementsPlanning => $$"""
                请分析以下用户故事需求，并生成详细的实施计划。

                用户故事: {{story.Title}}
                故事 ID: {{story.StoryId}}

                原始需求:
                {{story.Requirements}}

                项目 PRD:
                {{prdJson}}

                请生成详细的实施计划，包括：
                1. 需要实现的功能
                2. 需要修改或创建的文件
                3. 实现步骤
                4. 潜在的技术难点

                同时，请分析是否存在当前 backlog（待办列表）中遗漏的任务。如果发现遗漏的任务，请在回复最后以以下 JSON 格式输出：

                ```json
                {
                    "discoveredTasks": [
                        {
                            "title": "任务标题",
                            "requirements": "详细需求描述",
                            "priority": "High|Medium|Low",
                            "reason": "为什么需要这个任务"
                        }
                    ]
                }
                ```

                如果没有发现遗漏任务，请输出 "discoveredTasks": []
                """,

            StoryPhase.AcceptancePlanning => $"""
                请根据以下用户故事、需求和原始验收标准，细化验收标准。

                用户故事: {story.Title}
                故事 ID: {story.StoryId}

                详细实施计划:
                {detailedPlan ?? "(暂无)"}

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
                {detailedPlan ?? "(暂无)"}

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
                {detailedPlan ?? "(暂无)"}

                验收标准:
                {acceptanceCriteria ?? "(暂无)"}

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
                {acceptanceCriteria ?? "(暂无)"}

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
        IStoryRepository storyRepo,
        IStoryDependencyRepository storyDependencyRepo,
        string? existingDetailedPlan,
        string? existingAcceptanceCriteria,
        CancellationToken stoppingToken)
    {
        // 根据阶段类型处理结果并保存到 Plan 表
        return story.Phase switch
        {
            StoryPhase.RequirementsPlanning =>
                await HandleRequirementsPlanningResultAsync(
                    story, responseContent, planRepo, storyRepo, storyDependencyRepo, stoppingToken),

            StoryPhase.AcceptancePlanning =>
                await HandleAcceptancePlanningResultAsync(
                    story, responseContent, planRepo, stoppingToken),

            StoryPhase.Coding =>
                await SaveExecutionPlanAsync(story, responseContent, "Coding", planRepo, stoppingToken),

            StoryPhase.Testing =>
                await SaveExecutionPlanAsync(story, responseContent, "Testing", planRepo, stoppingToken),

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
        IPlanRepository planRepo,
        IStoryRepository storyRepo,
        IStoryDependencyRepository storyDependencyRepo,
        CancellationToken stoppingToken)
    {
        // 从响应中提取计划
        var planResult = _responseAnalyzer.ExtractPlan(responseContent);
        var planContent = planResult?.Plan ?? responseContent;

        // 保存到 Plan 表
        var plan = new Plan
        {
            StoryId = story.Id,
            PlanContent = planContent,
            Type = PlanType.DetailedPlan,
            Output = responseContent,
            CreatedAt = DateTime.UtcNow
        };
        await planRepo.AddAsync(plan, stoppingToken);

        // 解析并创建发现的任务
        await ParseAndCreateDiscoveredTasksAsync(story, responseContent, storyRepo, storyDependencyRepo, stoppingToken);

        return !string.IsNullOrEmpty(planContent);
    }

    /// <summary>
    /// 解析 AI 输出中发现的遗漏任务并创建 Story
    /// </summary>
    private async Task ParseAndCreateDiscoveredTasksAsync(
        Story parentStory,
        string responseContent,
        IStoryRepository storyRepo,
        IStoryDependencyRepository storyDependencyRepo,
        CancellationToken stoppingToken)
    {
        try
        {
            // 尝试从响应中提取 JSON
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(
                responseContent,
                @"```json\s*\{[\s\S]*?""discoveredTasks""[\s\S]*?\}\s*```",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!jsonMatch.Success)
            {
                // 尝试更宽松的匹配
                jsonMatch = System.Text.RegularExpressions.Regex.Match(
                    responseContent,
                    @"\{[\s\S]*?""discoveredTasks""\s*:\s*\[([\s\S]*?)\]\s*\}",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            if (!jsonMatch.Success)
            {
                _logger.Debug("未在响应中发现遗漏任务格式");
                return;
            }

            var jsonStr = jsonMatch.Value;
            // 清理 JSON 字符串
            jsonStr = System.Text.RegularExpressions.Regex.Replace(jsonStr, @"```json\s*", "");
            jsonStr = System.Text.RegularExpressions.Regex.Replace(jsonStr, @"\s*```", "");

            using var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            if (!root.TryGetProperty("discoveredTasks", out var tasksElement))
            {
                return;
            }

            var tasks = tasksElement.EnumerateArray().ToList();
            if (tasks.Count == 0)
            {
                _logger.Debug("未发现遗漏任务");
                return;
            }

            _logger.Information("发现 {Count} 个遗漏任务", tasks.Count);

            // 获取当前项目的最大 Story 编号
            var existingStories = await storyRepo.GetByProjectIdAsync(parentStory.ProjectId, stoppingToken);
            var maxStoryNum = 0;
            foreach (var s in existingStories)
            {
                var numMatch = System.Text.RegularExpressions.Regex.Match(s.StoryId, @"-(\d+)$");
                if (numMatch.Success && int.TryParse(numMatch.Groups[1].Value, out var num))
                {
                    maxStoryNum = Math.Max(maxStoryNum, num);
                }
            }

            foreach (var task in tasks)
            {
                var title = task.GetProperty("title").GetString() ?? "未命名任务";
                var requirements = task.TryGetProperty("requirements", out var req) ? req.GetString() ?? "" : "";
                var priority = task.TryGetProperty("priority", out var pri) ? pri.GetString() ?? "Medium" : "Medium";

                maxStoryNum++;
                var newStoryId = $"S-{maxStoryNum:D4}";

                var newStory = new Story
                {
                    ProjectId = parentStory.ProjectId,
                    StoryId = newStoryId,
                    Title = title,
                    Priority = priority,
                    Status = "Pending",
                    Phase = StoryPhase.Pending,
                    Requirements = requirements,
                    Source = StorySource.AutoDiscovered,
                    IsPaused = true, // 自动发现的任务默认暂停，等待人工确认
                    CreatedAt = DateTime.UtcNow
                };

                await storyRepo.AddAsync(newStory, stoppingToken);
                _logger.Information("创建自动发现任务: {StoryId} - {Title}", newStoryId, title);

                // 建立依赖关系：新任务依赖当前任务
                var dependency = new StoryDependency
                {
                    StoryId = newStory.Id,
                    DependsOnStoryId = parentStory.Id,
                    CreatedAt = DateTime.UtcNow
                };
                await storyDependencyRepo.AddAsync(dependency, stoppingToken);
                _logger.Information("建立依赖关系: {NewStory} 依赖 {ParentStory}", newStoryId, parentStory.StoryId);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "解析或创建发现的任务时出错");
        }
    }

    /// <summary>
    /// 处理验收标准计划阶段结果
    /// </summary>
    private async Task<bool> HandleAcceptancePlanningResultAsync(
        Story story,
        string responseContent,
        IPlanRepository planRepo,
        CancellationToken stoppingToken)
    {
        // 从响应中提取验收标准
        var planResult = _responseAnalyzer.ExtractPlan(responseContent);
        var planContent = planResult?.Plan ?? responseContent;

        // 保存到 Plan 表
        var plan = new Plan
        {
            StoryId = story.Id,
            PlanContent = planContent,
            Type = PlanType.AcceptanceCriteria,
            Output = responseContent,
            CreatedAt = DateTime.UtcNow
        };
        await planRepo.AddAsync(plan, stoppingToken);

        return !string.IsNullOrEmpty(planContent);
    }

    /// <summary>
    /// 保存执行阶段的输出到 Plan 表
    /// </summary>
    private async Task<bool> SaveExecutionPlanAsync(
        Story story,
        string responseContent,
        string phaseName,
        IPlanRepository planRepo,
        CancellationToken stoppingToken)
    {
        var plan = new Plan
        {
            StoryId = story.Id,
            PlanContent = $"{phaseName} 阶段执行完成",
            Type = PlanType.DetailedPlan, // 使用 DetailedPlan 存储执行记录
            Output = responseContent,
            CreatedAt = DateTime.UtcNow
        };
        await planRepo.AddAsync(plan, stoppingToken);

        return true;
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
