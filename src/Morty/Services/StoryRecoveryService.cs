using Microsoft.Extensions.DependencyInjection;
using Morty.Entities;
using Morty.Repositories;
using Serilog;

namespace Morty.Services;

/// <summary>
/// 故事恢复服务 - 系统启动时恢复中断的故事
/// 决策树：
/// 1. 缺少 DetailedPlan → 退回 RequirementsPlanning
/// 2. 有 DetailedPlan 但缺少 AcceptanceCriteria（且需要）→ 退回 AcceptancePlanning
/// 3. 规划完成但仍在规划阶段 → 推进到 Executing
/// 4. Running 状态 → 重置为 Pending（允许重新排队处理）
/// </summary>
public class StoryRecoveryService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Serilog.ILogger _logger;

    public StoryRecoveryService(IServiceScopeFactory scopeFactory, Serilog.ILogger? logger = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger ?? Log.Logger;
    }

    /// <summary>
    /// 恢复中断的故事
    /// 检查 Running 和 Pending 状态的故事，根据规划完成情况修正阶段
    /// </summary>
    public async Task RecoverAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var storyRepo = scope.ServiceProvider.GetRequiredService<IStoryRepository>();
            var planRepo = scope.ServiceProvider.GetRequiredService<IPlanRepository>();

            // 恢复 Running 状态的故事（服务崩溃时中断的）
            var runningStories = await storyRepo.GetByRunningStatusAsync(RunningStatus.Running, ct);
            // 也检查 Pending 状态中处于规划阶段的故事（可能部分完成了规划）
            var pendingPlanningStories = await storyRepo.GetByRunningStatusAndPhasesAsync(
                RunningStatus.Pending,
                new[] { StoryPhase.RequirementsPlanning, StoryPhase.AcceptancePlanning },
                ct);

            var allStoriesToCheck = runningStories.Concat(pendingPlanningStories).ToList();

            if (!allStoriesToCheck.Any())
            {
                _logger.Information("无中断的故事需要恢复");
                return;
            }

            _logger.Information("发现 {Count} 个需要检查的故事，开始恢复...", allStoriesToCheck.Count);

            foreach (var story in allStoriesToCheck)
            {
                var wasRunning = story.RunningStatus == RunningStatus.Running;
                _logger.Information("检查故事 {StoryId}，阶段: {Phase}，RunningStatus: {Status}",
                    story.StoryId, story.Phase, story.RunningStatus);

                var detailedPlan = await planRepo.GetLatestByStoryIdAndTypeAsync(
                    story.Id, PlanType.DetailedPlan, ct);
                var acceptanceCriteria = await planRepo.GetLatestByStoryIdAndTypeAsync(
                    story.Id, PlanType.AcceptanceCriteria, ct);

                var hasDetailedPlan = detailedPlan != null;
                var hasAcceptanceCriteria = acceptanceCriteria != null;
                var needsAcceptancePlanning = !string.IsNullOrWhiteSpace(story.UserAcceptanceCriteria);

                // 决策树：根据实际的计划完成情况确定正确的阶段
                if (!hasDetailedPlan)
                {
                    // 缺少详细计划，必须从需求规划开始
                    if (story.Phase != StoryPhase.RequirementsPlanning)
                    {
                        _logger.Warning("故事 {StoryId} 缺少 DetailedPlan，退回 RequirementsPlanning", story.StoryId);
                        story.Phase = StoryPhase.RequirementsPlanning;
                        story.Status = "Planning";
                        story.CurrentIteration = 0;
                    }
                }
                else if (needsAcceptancePlanning && !hasAcceptanceCriteria)
                {
                    // 有详细计划，需要验收规划但尚未完成
                    if (story.Phase != StoryPhase.AcceptancePlanning)
                    {
                        _logger.Warning("故事 {StoryId} 有 DetailedPlan 但缺少 AcceptanceCriteria，设为 AcceptancePlanning",
                            story.StoryId);
                        story.Phase = StoryPhase.AcceptancePlanning;
                        story.Status = "Planning";
                        story.CurrentIteration = 0;
                    }
                }
                else if (hasDetailedPlan && (!needsAcceptancePlanning || hasAcceptanceCriteria))
                {
                    // 规划已完成，如果还在规划阶段则推进到执行
                    if (story.Phase == StoryPhase.RequirementsPlanning || story.Phase == StoryPhase.AcceptancePlanning)
                    {
                        _logger.Information("故事 {StoryId} 规划已完成，推进到 Executing", story.StoryId);
                        story.Phase = StoryPhase.Executing;
                        story.Status = "InProgress";
                        story.CurrentIteration = 0;
                    }
                }

                // Running 状态的故事重置为 Pending（允许重新排队处理）
                if (wasRunning)
                {
                    story.RunningStatus = RunningStatus.Pending;
                }

                await storyRepo.UpdateAsync(story, ct);
                _logger.Information("故事 {StoryId} 恢复完成，阶段: {Phase}，RunningStatus: {RunningStatus}",
                    story.StoryId, story.Phase, story.RunningStatus);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "恢复中断故事时出错");
        }
    }
}
