using Morty.Entities;
using Morty.Interfaces;
using Morty.Repositories;
using Serilog;

namespace Morty.Services;

/// <summary>
/// 阶段结果处理器 - 处理各阶段 AI 输出的后续逻辑
/// 职责：保存计划到 Plan 表、解析发现的遗漏任务、建立依赖关系、阶段转换
/// 注册为 Singleton（通过方法参数接收 scoped 仓储）
/// </summary>
public class PhaseResultHandler
{
    private readonly Serilog.ILogger _logger;

    public PhaseResultHandler(Serilog.ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;
    }

    /// <summary>
    /// 处理阶段执行结果，根据阶段类型执行对应后处理
    /// 成功判断主要依赖 responseSuccess（exit code），不依赖正则匹配
    /// </summary>
    /// <returns>阶段是否成功完成</returns>
    public async Task<bool> HandleAsync(
        Story story,
        string responseContent,
        bool responseSuccess,
        AnalysisResult analysis,
        IPlanRepository planRepo,
        IStoryRepository storyRepo,
        IStoryDependencyRepository depRepo,
        CancellationToken ct)
    {
        switch (story.Phase)
        {
            case StoryPhase.RequirementsPlanning:
                // Planning: exit code 成功 + 非空输出
                if (responseSuccess && !string.IsNullOrWhiteSpace(responseContent))
                {
                    await HandleRequirementsPlanningResultAsync(
                        story, responseContent, planRepo, storyRepo, depRepo, ct);
                    return true;
                }
                return false;

            case StoryPhase.AcceptancePlanning:
                // AcceptancePlanning: exit code 成功 + 非空输出
                if (responseSuccess && !string.IsNullOrWhiteSpace(responseContent))
                {
                    await HandleAcceptancePlanningResultAsync(story, responseContent, planRepo, ct);
                    return true;
                }
                return false;

            case StoryPhase.Executing:
                // Coding: exit code 成功即可
                if (responseSuccess)
                    await SaveExecutionPlanAsync(story, responseContent, "Executing", planRepo, ct);
                return responseSuccess;

            case StoryPhase.Testing:
                // Testing: exit code 成功即可
                if (responseSuccess)
                    await SaveExecutionPlanAsync(story, responseContent, "Testing", planRepo, ct);
                return responseSuccess;

            case StoryPhase.Acceptance:
                // Acceptance: exit code 成功即可（不依赖 analysis.IsComplete）
                return responseSuccess;

            default:
                return responseSuccess;
        }
    }

    /// <summary>
    /// 转换到下一阶段
    /// 无用户验收标准时跳过 AcceptancePlanning，直接进入 Executing
    /// </summary>
    public async Task TransitionToNextPhaseAsync(
        Story story,
        IStoryRepository storyRepo,
        CancellationToken ct)
    {
        var nextPhase = story.Phase switch
        {
            // 需求规划完成后：有用户验收标准则进入验收规划，否则直接进入执行
            StoryPhase.RequirementsPlanning => string.IsNullOrWhiteSpace(story.UserAcceptanceCriteria)
                ? StoryPhase.Executing
                : StoryPhase.AcceptancePlanning,
            StoryPhase.AcceptancePlanning => StoryPhase.Executing,
            StoryPhase.Executing => StoryPhase.Testing,
            StoryPhase.Testing => StoryPhase.Acceptance,
            StoryPhase.Acceptance => StoryPhase.Completed,
            _ => story.Phase
        };

        story.Phase = nextPhase;
        story.CurrentIteration = 0;

        // 根据目标阶段设置 Status 和 RunningStatus
        switch (nextPhase)
        {
            case StoryPhase.RequirementsPlanning:
            case StoryPhase.AcceptancePlanning:
                story.Status = "Planning";
                story.RunningStatus = RunningStatus.Pending;
                break;
            case StoryPhase.Executing:
            case StoryPhase.Testing:
                story.Status = "InProgress";
                story.RunningStatus = RunningStatus.Pending;
                break;
            case StoryPhase.Acceptance:
                story.Status = "Verifying";
                story.RunningStatus = RunningStatus.Pending;
                break;
            case StoryPhase.Completed:
                story.Status = "Completed";
                story.CompletedAt = DateTime.UtcNow;
                story.RunningStatus = RunningStatus.Paused;
                _logger.Information("故事 {StoryId} 成功完成所有阶段", story.StoryId);
                break;
            default:
                story.RunningStatus = RunningStatus.Pending;
                break;
        }

        await storyRepo.UpdateAsync(story, ct);
        _logger.Information("故事 {StoryId} 阶段转换: {From} -> {To}",
            story.StoryId, story.Phase, nextPhase);
    }

    /// <summary>
    /// 处理需求计划阶段结果 - 保存 DetailedPlan 并解析发现的遗漏任务
    /// </summary>
    private async Task HandleRequirementsPlanningResultAsync(
        Story story,
        string responseContent,
        IPlanRepository planRepo,
        IStoryRepository storyRepo,
        IStoryDependencyRepository depRepo,
        CancellationToken ct)
    {
        var plan = new Plan
        {
            StoryId = story.Id,
            PlanContent = responseContent,
            Type = PlanType.DetailedPlan,
            Output = responseContent,
            CreatedAt = DateTime.UtcNow
        };
        await planRepo.AddAsync(plan, ct);

        await ParseAndCreateDiscoveredTasksAsync(story, responseContent, storyRepo, depRepo, ct);
    }

    /// <summary>
    /// 处理验收标准计划阶段结果 - 保存 AcceptanceCriteria
    /// </summary>
    private async Task HandleAcceptancePlanningResultAsync(
        Story story,
        string responseContent,
        IPlanRepository planRepo,
        CancellationToken ct)
    {
        var plan = new Plan
        {
            StoryId = story.Id,
            PlanContent = responseContent,
            Type = PlanType.AcceptanceCriteria,
            Output = responseContent,
            CreatedAt = DateTime.UtcNow
        };
        await planRepo.AddAsync(plan, ct);
    }

    /// <summary>
    /// 保存执行阶段的输出到 Plan 表（使用 ExecutionLog 类型，避免覆盖原始 DetailedPlan）
    /// </summary>
    private async Task SaveExecutionPlanAsync(
        Story story,
        string responseContent,
        string phaseName,
        IPlanRepository planRepo,
        CancellationToken ct)
    {
        var plan = new Plan
        {
            StoryId = story.Id,
            PlanContent = $"{phaseName} 阶段执行完成",
            Type = PlanType.ExecutionLog,
            Output = responseContent,
            CreatedAt = DateTime.UtcNow
        };
        await planRepo.AddAsync(plan, ct);
    }

    /// <summary>
    /// 解析 AI 输出中发现的遗漏任务并创建 Story
    /// 期望的 JSON 格式（在 ```json 代码块中）：
    /// { "discoveredTasks": [{ "title": "...", "requirements": "...", "priority": "High|Medium|Low", "reason": "..." }] }
    /// 新建的任务自动依赖当前任务，确保执行顺序正确
    /// </summary>
    private async Task ParseAndCreateDiscoveredTasksAsync(
        Story parentStory,
        string responseContent,
        IStoryRepository storyRepo,
        IStoryDependencyRepository depRepo,
        CancellationToken ct)
    {
        try
        {
            // 尝试从响应中提取 JSON（优先匹配 ```json 代码块）
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(
                responseContent,
                @"```json\s*\{[\s\S]*?""discoveredTasks""[\s\S]*?\}\s*```",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!jsonMatch.Success)
            {
                // 回退：更宽松的匹配（无代码块包裹）
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
            jsonStr = System.Text.RegularExpressions.Regex.Replace(jsonStr, @"```json\s*", "");
            jsonStr = System.Text.RegularExpressions.Regex.Replace(jsonStr, @"\s*```", "");

            using var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            if (!root.TryGetProperty("discoveredTasks", out var tasksElement))
                return;

            var tasks = tasksElement.EnumerateArray().ToList();
            if (tasks.Count == 0)
            {
                _logger.Debug("未发现遗漏任务");
                return;
            }

            _logger.Information("发现 {Count} 个遗漏任务", tasks.Count);

            foreach (var task in tasks)
            {
                var title = task.GetProperty("title").GetString() ?? "未命名任务";
                var requirements = task.TryGetProperty("requirements", out var req) ? req.GetString() ?? "" : "";
                var priority = task.TryGetProperty("priority", out var pri) ? pri.GetString() ?? "Medium" : "Medium";

                var newStoryId = GenerateStoryId();

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
                    RunningStatus = RunningStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                await storyRepo.AddAsync(newStory, ct);
                _logger.Information("创建自动发现任务: {StoryId} - {Title}", newStoryId, title);

                // 建立依赖关系：新任务依赖当前任务（确保父任务完成后才执行）
                var dependency = new StoryDependency
                {
                    StoryId = newStory.Id,
                    DependsOnStoryId = parentStory.Id,
                    CreatedAt = DateTime.UtcNow
                };
                await depRepo.AddAsync(dependency, ct);
                _logger.Information("建立依赖关系: {NewStory} 依赖 {ParentStory}", newStoryId, parentStory.StoryId);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "解析或创建发现的任务时出错");
        }
    }

    /// <summary>
    /// 生成故事 ID，格式为 S-{timestamp_base36}，与前端保持一致
    /// </summary>
    private static string GenerateStoryId()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return $"S-{ConvertToBase36(timestamp).ToUpperInvariant()}";
    }

    /// <summary>
    /// 将长整型转换为 Base36 字符串
    /// </summary>
    private static string ConvertToBase36(long value)
    {
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (value == 0) return "0";
        var result = new System.Text.StringBuilder();
        while (value > 0)
        {
            result.Insert(0, chars[(int)(value % 36)]);
            value /= 36;
        }
        return result.ToString();
    }
}
