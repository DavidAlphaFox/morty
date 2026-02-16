using Morty.Core.Entities;

namespace Morty.Core.Repositories;

/// <summary>
/// 项目仓储接口
/// 定义项目数据的持久化操作
/// </summary>
public interface IProjectRepository
{
    /// <summary>根据 ID 获取项目</summary>
    Task<Project?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>获取所有项目</summary>
    Task<List<Project>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>添加新项目</summary>
    Task<Project> AddAsync(Project project, CancellationToken cancellationToken = default);
    /// <summary>更新项目</summary>
    Task UpdateAsync(Project project, CancellationToken cancellationToken = default);
    /// <summary>删除项目</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// 故事仓储接口
/// 定义用户故事数据的持久化操作
/// </summary>
public interface IStoryRepository
{
    /// <summary>根据 ID 获取故事</summary>
    Task<Story?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>获取指定项目的所有故事</summary>
    Task<List<Story>> GetByProjectIdAsync(int projectId, CancellationToken cancellationToken = default);
    /// <summary>获取下一个待处理的故事（按优先级排序）</summary>
    Task<Story?> GetNextPendingAsync(CancellationToken cancellationToken = default);
    /// <summary>获取指定队列的待处理故事列表（按优先级排序）</summary>
    Task<List<Story>> GetPendingByQueueTypeAsync(StoryQueueType queueType, CancellationToken cancellationToken = default);
    /// <summary>添加新故事</summary>
    Task<Story> AddAsync(Story story, CancellationToken cancellationToken = default);
    /// <summary>更新故事</summary>
    Task UpdateAsync(Story story, CancellationToken cancellationToken = default);
    /// <summary>获取故事的迭代次数</summary>
    Task<int> GetIterationCountAsync(int storyId, CancellationToken cancellationToken = default);
    /// <summary>获取故事的所有迭代</summary>
    Task<List<Iteration>> GetIterationsByStoryIdAsync(int storyId, CancellationToken cancellationToken = default);
    /// <summary>获取故事的最新计划</summary>
    Task<Plan?> GetPlanByStoryIdAsync(int storyId, CancellationToken cancellationToken = default);
    /// <summary>获取指定运行状态的所有故事</summary>
    Task<List<Story>> GetByRunningStatusAsync(RunningStatus status, CancellationToken cancellationToken = default);
    /// <summary>获取指定运行状态和阶段的所有故事</summary>
    Task<List<Story>> GetByRunningStatusAndPhasesAsync(RunningStatus status, StoryPhase[] phases, CancellationToken cancellationToken = default);
}

/// <summary>
/// 迭代仓储接口
/// 定义迭代数据的持久化操作
/// </summary>
public interface IIterationRepository
{
    /// <summary>添加新迭代</summary>
    Task<Iteration> AddAsync(Iteration iteration, CancellationToken cancellationToken = default);
    /// <summary>获取故事的所有迭代</summary>
    Task<List<Iteration>> GetByStoryIdAsync(int storyId, CancellationToken cancellationToken = default);
    /// <summary>更新迭代</summary>
    Task UpdateAsync(Iteration iteration, CancellationToken cancellationToken = default);
}

/// <summary>
/// 计划仓储接口
/// 定义计划数据的持久化操作
/// </summary>
public interface IPlanRepository
{
    /// <summary>获取故事的最新计划</summary>
    Task<Plan?> GetLatestByStoryIdAsync(int storyId, CancellationToken cancellationToken = default);
    /// <summary>根据类型获取故事的最新计划</summary>
    Task<Plan?> GetLatestByStoryIdAndTypeAsync(int storyId, PlanType type, CancellationToken cancellationToken = default);
    /// <summary>添加新计划</summary>
    Task<Plan> AddAsync(Plan plan, CancellationToken cancellationToken = default);
}

/// <summary>
/// 验证仓储接口
/// 定义验证数据的持久化操作
/// </summary>
public interface IVerificationRepository
{
    /// <summary>添加新验证记录</summary>
    Task<Verification> AddAsync(Verification verification, CancellationToken cancellationToken = default);
    /// <summary>获取迭代的所有验证记录</summary>
    Task<List<Verification>> GetByIterationIdAsync(int iterationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 故事事件仓储接口
/// 定义故事事件数据的持久化操作
/// </summary>
public interface IStoryEventRepository
{
    /// <summary>添加新故事事件</summary>
    Task<StoryEvent> AddAsync(StoryEvent storyEvent, CancellationToken cancellationToken = default);
    /// <summary>获取故事的所有事件（按时间倒序）</summary>
    Task<List<StoryEvent>> GetByStoryIdAsync(int storyId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 执行输出仓储接口
/// 定义执行输出数据的持久化操作
/// </summary>
public interface IExecutionOutputRepository
{
    /// <summary>添加新执行输出</summary>
    Task<ExecutionOutput> AddAsync(ExecutionOutput output, CancellationToken cancellationToken = default);
    /// <summary>获取迭代的所有执行输出（按时间倒序）</summary>
    Task<List<ExecutionOutput>> GetByIterationIdAsync(int iterationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 阶段历史仓储接口
/// 定义阶段历史数据的持久化操作
/// </summary>
public interface IPhaseHistoryRepository
{
    /// <summary>添加新阶段历史</summary>
    Task<PhaseHistory> AddAsync(PhaseHistory phaseHistory, CancellationToken cancellationToken = default);
    /// <summary>获取故事的最新阶段历史</summary>
    Task<PhaseHistory?> GetLatestByStoryIdAsync(int storyId, CancellationToken cancellationToken = default);
    /// <summary>获取故事的所有阶段历史</summary>
    Task<List<PhaseHistory>> GetByStoryIdAsync(int storyId, CancellationToken cancellationToken = default);
    /// <summary>更新阶段历史</summary>
    Task UpdateAsync(PhaseHistory phaseHistory, CancellationToken cancellationToken = default);
}

/// <summary>
/// 故事依赖关系仓储接口
/// 定义故事依赖关系数据的持久化操作
/// </summary>
public interface IStoryDependencyRepository
{
    /// <summary>添加依赖关系</summary>
    Task<StoryDependency> AddAsync(StoryDependency dependency, CancellationToken cancellationToken = default);
    /// <summary>删除依赖关系</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>获取故事的所有依赖（当前故事依赖的其他故事）</summary>
    Task<List<Story>> GetDependenciesAsync(int storyId, CancellationToken cancellationToken = default);
    /// <summary>获取依赖当前故事的所有故事</summary>
    Task<List<Story>> GetDependentsAsync(int storyId, CancellationToken cancellationToken = default);
    /// <summary>检查依赖是否已满足（所有依赖的故事都已完成）</summary>
    Task<bool> AreDependenciesSatisfiedAsync(int storyId, CancellationToken cancellationToken = default);
    /// <summary>获取所有依赖当前故事都已完成的待处理故事</summary>
    Task<List<Story>> GetReadyStoriesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 环境配置组仓储接口
/// </summary>
public interface IEnvConfigGroupRepository
{
    /// <summary>根据 ID 获取环境配置组</summary>
    Task<EnvConfigGroup?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>获取所有环境配置组</summary>
    Task<List<EnvConfigGroup>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>添加新环境配置组</summary>
    Task<EnvConfigGroup> AddAsync(EnvConfigGroup group, CancellationToken cancellationToken = default);
    /// <summary>更新环境配置组</summary>
    Task UpdateAsync(EnvConfigGroup group, CancellationToken cancellationToken = default);
    /// <summary>删除环境配置组</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// 环境变量规则仓储接口
/// </summary>
public interface IEnvConfigRuleRepository
{
    /// <summary>根据 ID 获取规则</summary>
    Task<EnvConfigRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>获取项目的所有规则</summary>
    Task<List<EnvConfigRule>> GetByProjectIdAsync(int projectId, CancellationToken cancellationToken = default);
    /// <summary>添加新规则</summary>
    Task<EnvConfigRule> AddAsync(EnvConfigRule rule, CancellationToken cancellationToken = default);
    /// <summary>更新规则</summary>
    Task UpdateAsync(EnvConfigRule rule, CancellationToken cancellationToken = default);
    /// <summary>删除规则</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>查找匹配的规则（按优先级倒序）</summary>
    Task<EnvConfigRule?> FindMatchingRuleAsync(int projectId, StoryPhase? fromPhase, StoryPhase? toPhase, string[] tags, CancellationToken cancellationToken = default);
}
