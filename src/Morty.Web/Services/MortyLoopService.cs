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
/// </summary>
public class MortyLoopService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClaudeProviderFactory _providerFactory;
    private readonly IResponseAnalyzer _responseAnalyzer;
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly IRateLimiter _rateLimiter;
    private readonly Serilog.ILogger _logger;
    private readonly TimeSpan _delayBetweenIterations;
    private readonly int _maxIterationsPerStory;

    private bool _isRunning;

    public MortyLoopService(
        IServiceScopeFactory scopeFactory,
        IClaudeProviderFactory providerFactory,
        IResponseAnalyzer responseAnalyzer,
        ICircuitBreaker circuitBreaker,
        IRateLimiter rateLimiter,
        Serilog.ILogger? logger = null,
        int maxIterationsPerStory = 10,
        int delaySeconds = 5)
    {
        _scopeFactory = scopeFactory;
        _providerFactory = providerFactory;
        _responseAnalyzer = responseAnalyzer;
        _circuitBreaker = circuitBreaker;
        _rateLimiter = rateLimiter;
        _logger = logger ?? Log.Logger;
        _maxIterationsPerStory = maxIterationsPerStory;
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
        var providerRepo = scope.ServiceProvider.GetRequiredService<IProviderRepository>();

        // 获取下一个待处理的故事
        var story = await storyRepo.GetNextPendingAsync(stoppingToken);

        if (story == null)
        {
            _logger.Debug("没有待处理的故事");
            return;
        }

        _logger.Information("正在处理故事 {StoryId}: {Title}", story.StoryId, story.Title);

        // 确定是规划迭代还是执行迭代
        var iterationCount = await storyRepo.GetIterationCountAsync(story.Id, stoppingToken);
        var isPlanningPhase = iterationCount == 0;

        // 根据阶段获取供应商
        var provider = isPlanningPhase
            ? _providerFactory.GetProviderForPlanType(PlanUsageType.Planning)
            : _providerFactory.GetProviderForPlanType(PlanUsageType.Execution);

        if (provider == null)
        {
            _logger.Error("故事 {StoryId} 没有可用的供应商", story.StoryId);
            story.Status = "Failed";
            await storyRepo.UpdateAsync(story, stoppingToken);
            return;
        }

        // 获取或创建数据库中的供应商记录
        var dbProvider = await providerRepo.GetAllAsync(stoppingToken)
            .ContinueWith(t => t.Result.FirstOrDefault(p => p.Name == provider.Name), stoppingToken);

        // 更新故事状态
        story.Status = isPlanningPhase ? "Planning" : "InProgress";
        await storyRepo.UpdateAsync(story, stoppingToken);

        // 获取项目 PRD
        var project = await projectRepo.GetByIdAsync(story.ProjectId, stoppingToken);
        if (project == null)
        {
            _logger.Error("故事 {StoryId} 对应的项目未找到", story.StoryId);
            story.Status = "Failed";
            await storyRepo.UpdateAsync(story, stoppingToken);
            return;
        }

        // 确保工作目录存在
        if (!string.IsNullOrEmpty(project.WorkingDirectory) && !Directory.Exists(project.WorkingDirectory))
        {
            _logger.Information("创建工作目录: {WorkingDirectory}", project.WorkingDirectory);
            Directory.CreateDirectory(project.WorkingDirectory);
        }

        if (iterationCount >= _maxIterationsPerStory)
        {
            _logger.Warning("故事 {StoryId} 达到最大迭代次数", story.StoryId);
            story.Status = "Failed";
            await storyRepo.UpdateAsync(story, stoppingToken);
            return;
        }

        // 等待速率限制器
        await _rateLimiter.WaitForAvailabilityAsync(stoppingToken);
        _rateLimiter.RecordRequest();

        // 创建迭代
        var iteration = new Iteration
        {
            StoryId = story.Id,
            IterationNum = iterationCount + 1,
            StartedAt = DateTime.UtcNow,
            ProviderId = dbProvider?.Id
        };
        await iterationRepo.AddAsync(iteration, stoppingToken);

        try
        {
            // 构建提示词
            var prompt = BuildPrompt(project.PrdJson, story, iterationCount);

            // 调用供应商
            _logger.Information("使用供应商: {Provider} 用于 {Phase}", provider.Name, isPlanningPhase ? "规划" : "执行");
            var request = new ProviderRequest(prompt);
            var response = await provider.SendMessageAsync(request, stoppingToken);

            iteration.CompletedAt = DateTime.UtcNow;
            iteration.DurationMs = (long)(iteration.CompletedAt.Value - iteration.StartedAt).TotalMilliseconds;
            iteration.Output = response.Content;
            iteration.CostUsd = response.CostUsd;
            await iterationRepo.UpdateAsync(iteration, stoppingToken);

            // 保存执行输出
            var executionOutput = new ExecutionOutput
            {
                IterationId = iteration.Id,
                ProviderId = dbProvider?.Id,
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

            // 如果是第一次迭代（规划阶段），提取并保存计划
            if (isPlanningPhase)
            {
                var planResult = _responseAnalyzer.ExtractPlan(response.Content);
                if (planResult != null)
                {
                    var plan = new Plan
                    {
                        StoryId = story.Id,
                        PlanContent = planResult.Plan,
                        Type = PlanType.Planning,
                        ProviderId = dbProvider?.Id,
                        Output = response.Content,
                        CreatedAt = DateTime.UtcNow
                    };
                    await planRepo.AddAsync(plan, stoppingToken);
                }
            }
            else
            {
                // 保存执行计划
                var plan = new Plan
                {
                    StoryId = story.Id,
                    PlanContent = $"迭代 {iteration.IterationNum} 执行",
                    Type = PlanType.Execution,
                    ProviderId = dbProvider?.Id,
                    Output = response.Content,
                    CreatedAt = DateTime.UtcNow
                };
                await planRepo.AddAsync(plan, stoppingToken);
            }

            // 根据分析结果更新故事状态
            if (analysis.IsComplete)
            {
                story.Status = "Completed";
                story.CompletedAt = DateTime.UtcNow;
                _circuitBreaker.RecordSuccess();
                _logger.Information("故事 {StoryId} 成功完成", story.StoryId);
            }
            else
            {
                story.Status = "InProgress";
                _circuitBreaker.RecordFailure();
                _logger.Information("故事 {StoryId} 迭代 {Iteration} 完成，尚未完成",
                    story.StoryId, iteration.IterationNum);
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
                    complete = analysis.IsComplete,
                    errors = analysis.HasErrors,
                    provider = provider.Name,
                    isPlanning = isPlanningPhase
                })
            };
            await storyEventRepo.AddAsync(storyEvent, stoppingToken);

            await storyRepo.UpdateAsync(story, stoppingToken);

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
                CompletedAt = story.CompletedAt
            };
            await MortyHub.Broadcaster.NotifyStoryUpdated(storyDto);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "处理故事 {StoryId} 时出错", story.StoryId);
            iteration.Output = ex.Message;
            await iterationRepo.UpdateAsync(iteration, stoppingToken);
            story.Status = "Failed";
            _circuitBreaker.RecordFailure();
            await storyRepo.UpdateAsync(story, stoppingToken);
        }
    }

    /// <summary>
    /// 构建提示词
    /// </summary>
    private static string BuildPrompt(string prdJson, Story story, int iterationCount)
    {
        var iterationContext = iterationCount == 0
            ? "这是第一次迭代。请分析需求并创建详细的实施计划。"
            : $"这是第 {iterationCount + 1} 次迭代。请根据计划继续实施。";

        return $"""
            您正在实现一个用户故事。

            用户故事: {story.Title}
            故事 ID: {story.StoryId}

            {iterationContext}

            需求 (PRD):
            {prdJson}

            请实现更改并回复:
            1. 您做了什么
            2. 修改了哪些文件
            3. 实现是否完成或还有什么待完成
            """;
    }
}
