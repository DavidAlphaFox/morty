using Microsoft.EntityFrameworkCore;
using Morty.Core.Entities;
using Morty.Core.Repositories;
using Morty.Infrastructure.Data;

namespace Morty.Infrastructure.Repositories;

/// <summary>
/// 项目仓储实现
/// 负责项目数据的数据库操作
/// </summary>
public class ProjectRepository : IProjectRepository
{
    private readonly MortyDbContext _context;

    public ProjectRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<Project?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Projects.FindAsync([id], cancellationToken);
    }

    public async Task<List<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Projects.ToListAsync(cancellationToken);
    }

    public async Task<Project> AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        _context.Projects.Add(project);
        await _context.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        _context.Projects.Update(project);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects.FindAsync([id], cancellationToken);
        if (project != null)
        {
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>
/// 故事仓储实现
/// 负责用户故事数据的数据库操作
/// 支持按优先级和创建时间排序
/// </summary>
public class StoryRepository : IStoryRepository
{
    private readonly MortyDbContext _context;

    public StoryRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<Story?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Stories.FindAsync([id], cancellationToken);
    }

    public async Task<List<Story>> GetByProjectIdAsync(int projectId, CancellationToken cancellationToken = default)
    {
        return await _context.Stories
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.Priority == "High" ? 0 : s.Priority == "Medium" ? 1 : 2)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Story?> GetNextPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Stories
            .Where(s => s.RunningStatus == RunningStatus.Pending
                && (s.Status == "Pending" || s.Status == "InProgress"
                || s.Phase == StoryPhase.RequirementsPlanning
                || s.Phase == StoryPhase.AcceptancePlanning
                || s.Phase == StoryPhase.Executing
                || s.Phase == StoryPhase.Testing
                || s.Phase == StoryPhase.Acceptance))
            .OrderBy(s => s.Priority == "High" ? 0 : s.Priority == "Medium" ? 1 : 2)
            .ThenBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<Story>> GetPendingByQueueTypeAsync(StoryQueueType queueType, CancellationToken cancellationToken = default)
    {
        // 定义各队列类型对应的阶段（包含 Pending 以便从待处理状态开始）
        var planningPhases = new[]
        {
            StoryPhase.Pending,
            StoryPhase.RequirementsPlanning,
            StoryPhase.AcceptancePlanning
        };

        var executionPhases = new[]
        {
            StoryPhase.Pending,
            StoryPhase.Executing,
            StoryPhase.Testing,
            StoryPhase.Acceptance
        };

        var activePhases = queueType == StoryQueueType.Planning
            ? planningPhases
            : executionPhases;

        return await _context.Stories
            .Where(s => s.RunningStatus == RunningStatus.Pending
                && (s.Status == "Pending" || s.Status == "InProgress" || s.Status == "Planning" || s.Status == "Verifying")
                && activePhases.Contains(s.Phase))
            .OrderBy(s => s.Priority == "High" ? 0 : s.Priority == "Medium" ? 1 : 2)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Story> AddAsync(Story story, CancellationToken cancellationToken = default)
    {
        _context.Stories.Add(story);
        await _context.SaveChangesAsync(cancellationToken);
        return story;
    }

    public async Task UpdateAsync(Story story, CancellationToken cancellationToken = default)
    {
        _context.Stories.Update(story);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Story>> GetByRunningStatusAsync(RunningStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Stories
            .Where(s => s.RunningStatus == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Story>> GetByRunningStatusAndPhasesAsync(RunningStatus status, StoryPhase[] phases, CancellationToken cancellationToken = default)
    {
        return await _context.Stories
            .Where(s => s.RunningStatus == status && phases.Contains(s.Phase))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetIterationCountAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.Iterations.CountAsync(i => i.StoryId == storyId, cancellationToken);
    }

    public async Task<List<Iteration>> GetIterationsByStoryIdAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.Iterations
            .Where(i => i.StoryId == storyId)
            .OrderBy(i => i.IterationNum)
            .ToListAsync(cancellationToken);
    }

    public async Task<Plan?> GetPlanByStoryIdAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.Plans
            .Where(p => p.StoryId == storyId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

/// <summary>
/// 迭代仓储实现
/// 负责迭代数据的数据库操作
/// </summary>
public class IterationRepository : IIterationRepository
{
    private readonly MortyDbContext _context;

    public IterationRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<Iteration> AddAsync(Iteration iteration, CancellationToken cancellationToken = default)
    {
        _context.Iterations.Add(iteration);
        await _context.SaveChangesAsync(cancellationToken);
        return iteration;
    }

    public async Task<List<Iteration>> GetByStoryIdAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.Iterations
            .Where(i => i.StoryId == storyId)
            .OrderBy(i => i.IterationNum)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(Iteration iteration, CancellationToken cancellationToken = default)
    {
        _context.Iterations.Update(iteration);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// 计划仓储实现
/// 负责计划数据的数据库操作
/// </summary>
public class PlanRepository : IPlanRepository
{
    private readonly MortyDbContext _context;

    public PlanRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<Plan?> GetLatestByStoryIdAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.Plans
            .Where(p => p.StoryId == storyId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Plan?> GetLatestByStoryIdAndTypeAsync(int storyId, PlanType type, CancellationToken cancellationToken = default)
    {
        return await _context.Plans
            .Where(p => p.StoryId == storyId && p.Type == type)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Plan> AddAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        // 自动递增 Version，避免唯一约束冲突 (StoryId, Type, Version)
        var maxVersion = await _context.Plans
            .Where(p => p.StoryId == plan.StoryId && p.Type == plan.Type)
            .MaxAsync(p => (int?)p.Version, cancellationToken) ?? 0;
        plan.Version = maxVersion + 1;

        _context.Plans.Add(plan);
        await _context.SaveChangesAsync(cancellationToken);
        return plan;
    }
}

/// <summary>
/// 验证仓储实现
/// 负责验证数据的数据库操作
/// </summary>
public class VerificationRepository : IVerificationRepository
{
    private readonly MortyDbContext _context;

    public VerificationRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<Verification> AddAsync(Verification verification, CancellationToken cancellationToken = default)
    {
        _context.Verifications.Add(verification);
        await _context.SaveChangesAsync(cancellationToken);
        return verification;
    }

    public async Task<List<Verification>> GetByIterationIdAsync(int iterationId, CancellationToken cancellationToken = default)
    {
        return await _context.Verifications
            .Where(v => v.IterationId == iterationId)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// 故事事件仓储实现
/// 负责故事事件数据的数据库操作
/// </summary>
public class StoryEventRepository : IStoryEventRepository
{
    private readonly MortyDbContext _context;

    public StoryEventRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<StoryEvent> AddAsync(StoryEvent storyEvent, CancellationToken cancellationToken = default)
    {
        _context.StoryEvents.Add(storyEvent);
        await _context.SaveChangesAsync(cancellationToken);
        return storyEvent;
    }

    public async Task<List<StoryEvent>> GetByStoryIdAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.StoryEvents
            .Where(e => e.StoryId == storyId)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// 执行输出仓储实现
/// 负责执行输出数据的数据库操作
/// </summary>
public class ExecutionOutputRepository : IExecutionOutputRepository
{
    private readonly MortyDbContext _context;

    public ExecutionOutputRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<ExecutionOutput> AddAsync(ExecutionOutput output, CancellationToken cancellationToken = default)
    {
        _context.ExecutionOutputs.Add(output);
        await _context.SaveChangesAsync(cancellationToken);
        return output;
    }

    public async Task<List<ExecutionOutput>> GetByIterationIdAsync(int iterationId, CancellationToken cancellationToken = default)
    {
        return await _context.ExecutionOutputs
            .Where(e => e.IterationId == iterationId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// 阶段历史仓储实现
/// 负责阶段历史数据的数据库操作
/// </summary>
public class PhaseHistoryRepository : IPhaseHistoryRepository
{
    private readonly MortyDbContext _context;

    public PhaseHistoryRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<PhaseHistory> AddAsync(PhaseHistory phaseHistory, CancellationToken cancellationToken = default)
    {
        _context.PhaseHistories.Add(phaseHistory);
        await _context.SaveChangesAsync(cancellationToken);
        return phaseHistory;
    }

    public async Task<PhaseHistory?> GetLatestByStoryIdAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.PhaseHistories
            .Where(p => p.StoryId == storyId)
            .OrderByDescending(p => p.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<PhaseHistory>> GetByStoryIdAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.PhaseHistories
            .Where(p => p.StoryId == storyId)
            .OrderBy(p => p.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(PhaseHistory phaseHistory, CancellationToken cancellationToken = default)
    {
        _context.PhaseHistories.Update(phaseHistory);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// 故事依赖关系仓储实现
/// 负责故事依赖关系数据的数据库操作
/// </summary>
public class StoryDependencyRepository : IStoryDependencyRepository
{
    private readonly MortyDbContext _context;

    public StoryDependencyRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<StoryDependency> AddAsync(StoryDependency dependency, CancellationToken cancellationToken = default)
    {
        _context.StoryDependencies.Add(dependency);
        await _context.SaveChangesAsync(cancellationToken);
        return dependency;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var dependency = await _context.StoryDependencies.FindAsync([id], cancellationToken);
        if (dependency != null)
        {
            _context.StoryDependencies.Remove(dependency);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<List<Story>> GetDependenciesAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.StoryDependencies
            .Where(d => d.StoryId == storyId)
            .Select(d => d.DependsOnStory)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Story>> GetDependentsAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.StoryDependencies
            .Where(d => d.DependsOnStoryId == storyId)
            .Select(d => d.Story)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AreDependenciesSatisfiedAsync(int storyId, CancellationToken cancellationToken = default)
    {
        // 获取所有依赖的故事
        var dependencies = await _context.StoryDependencies
            .Where(d => d.StoryId == storyId)
            .Select(d => d.DependsOnStory)
            .ToListAsync(cancellationToken);

        // 如果没有依赖，直接返回 true
        if (!dependencies.Any())
            return true;

        // 检查所有依赖的故事是否都已完成
        return dependencies.All(s => s.Phase == StoryPhase.Completed);
    }

    public async Task<List<Story>> GetReadyStoriesAsync(CancellationToken cancellationToken = default)
    {
        // 获取所有待处理的 story（排除暂停的）
        var pendingStories = await _context.Stories
            .Where(s => s.RunningStatus == RunningStatus.Pending
                && (s.Status == "Pending" || s.Status == "InProgress"
                || s.Phase == StoryPhase.RequirementsPlanning
                || s.Phase == StoryPhase.AcceptancePlanning
                || s.Phase == StoryPhase.Executing
                || s.Phase == StoryPhase.Testing
                || s.Phase == StoryPhase.Acceptance))
            .ToListAsync(cancellationToken);

        // 过滤出依赖已满足的故事
        var readyStories = new List<Story>();
        foreach (var story in pendingStories)
        {
            if (await AreDependenciesSatisfiedAsync(story.Id, cancellationToken))
            {
                readyStories.Add(story);
            }
        }

        return readyStories;
    }
}

/// <summary>
/// 环境配置组仓储实现
/// 负责环境配置组数据的数据库操作
/// </summary>
public class EnvConfigGroupRepository : IEnvConfigGroupRepository
{
    private readonly MortyDbContext _context;

    public EnvConfigGroupRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<EnvConfigGroup?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.EnvConfigGroups
            .Include(g => g.Variables)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<List<EnvConfigGroup>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.EnvConfigGroups
            .Include(g => g.Variables)
            .ToListAsync(cancellationToken);
    }

    public async Task<EnvConfigGroup> AddAsync(EnvConfigGroup group, CancellationToken cancellationToken = default)
    {
        _context.EnvConfigGroups.Add(group);
        await _context.SaveChangesAsync(cancellationToken);
        return group;
    }

    public async Task UpdateAsync(EnvConfigGroup group, CancellationToken cancellationToken = default)
    {
        _context.EnvConfigGroups.Update(group);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var group = await _context.EnvConfigGroups.FindAsync([id], cancellationToken);
        if (group != null)
        {
            _context.EnvConfigGroups.Remove(group);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>
/// 环境变量规则仓储实现
/// 负责环境变量规则数据的数据库操作
/// </summary>
public class EnvConfigRuleRepository : IEnvConfigRuleRepository
{
    private readonly MortyDbContext _context;

    public EnvConfigRuleRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<EnvConfigRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.EnvConfigRules
            .Include(r => r.EnvConfigGroup)
            .ThenInclude(g => g.Variables)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<List<EnvConfigRule>> GetByProjectIdAsync(int projectId, CancellationToken cancellationToken = default)
    {
        return await _context.EnvConfigRules
            .Where(r => r.ProjectId == projectId)
            .Include(r => r.EnvConfigGroup)
            .ThenInclude(g => g.Variables)
            .OrderByDescending(r => r.Priority)
            .ToListAsync(cancellationToken);
    }

    public async Task<EnvConfigRule> AddAsync(EnvConfigRule rule, CancellationToken cancellationToken = default)
    {
        _context.EnvConfigRules.Add(rule);
        await _context.SaveChangesAsync(cancellationToken);
        return rule;
    }

    public async Task UpdateAsync(EnvConfigRule rule, CancellationToken cancellationToken = default)
    {
        _context.EnvConfigRules.Update(rule);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var rule = await _context.EnvConfigRules.FindAsync([id], cancellationToken);
        if (rule != null)
        {
            _context.EnvConfigRules.Remove(rule);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<EnvConfigRule?> FindMatchingRuleAsync(int projectId, StoryPhase? fromPhase, StoryPhase? toPhase, string[] tags, CancellationToken cancellationToken = default)
    {
        var rules = await GetByProjectIdAsync(projectId, cancellationToken);

        // 按优先级倒序查找匹配的规则
        foreach (var rule in rules)
        {
            // 检查阶段是否匹配
            bool phaseMatches = (rule.FromPhase == null || rule.FromPhase == fromPhase) &&
                               (rule.ToPhase == null || rule.ToPhase == toPhase);

            if (!phaseMatches)
                continue;

            // 检查标签是否匹配
            var ruleTags = System.Text.Json.JsonSerializer.Deserialize<string[]>(rule.Tags) ?? [];

            // 如果规则没有标签，匹配所有
            if (ruleTags.Length == 0)
                return rule;

            // 如果规则有标签，检查故事标签是否包含规则的所有标签
            if (ruleTags.All(t => tags.Contains(t)))
                return rule;
        }

        return null;
    }
}
