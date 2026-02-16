namespace Morty.Core.Entities;

/// <summary>
/// 项目实体
/// </summary>
public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string PrdJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>默认环境配置组 ID（可选）</summary>
    public int? DefaultEnvConfigGroupId { get; set; }

    public ICollection<Story> Stories { get; set; } = new List<Story>();
    public ICollection<EnvConfigRule> EnvConfigRules { get; set; } = new List<EnvConfigRule>();
    public EnvConfigGroup? DefaultEnvConfigGroup { get; set; }
}

/// <summary>
/// 用户故事实体
/// </summary>
public class Story
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string StoryId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // 多阶段处理相关字段
    /// <summary>当前阶段</summary>
    public StoryPhase Phase { get; set; } = StoryPhase.Pending;
    /// <summary>用户需求（用户输入）</summary>
    public string Requirements { get; set; } = string.Empty;
    /// <summary>用户验收标准（用户输入）</summary>
    public string UserAcceptanceCriteria { get; set; } = string.Empty;
    /// <summary>当前阶段内的迭代次数</summary>
    public int CurrentIteration { get; set; } = 0;
    /// <summary>运行状态：Paused-暂停, Pending-排队等待, Running-正在运行</summary>
    public RunningStatus RunningStatus { get; set; } = RunningStatus.Paused;
    /// <summary>故事来源</summary>
    public StorySource Source { get; set; } = StorySource.UserAdded;
    /// <summary>标签（JSON 数组，如 ["frontend", "api", "urgent"]）</summary>
    public string Tags { get; set; } = "[]";

    public Project Project { get; set; } = null!;
    public ICollection<Iteration> Iterations { get; set; } = new List<Iteration>();
    public ICollection<Plan> Plans { get; set; } = new List<Plan>();
    public ICollection<StoryEvent> Events { get; set; } = new List<StoryEvent>();
    public ICollection<PhaseHistory> PhaseHistories { get; set; } = new List<PhaseHistory>();

    // 依赖关系 - 当前故事依赖的其他故事
    public ICollection<StoryDependency> Dependencies { get; set; } = new List<StoryDependency>();
    // 依赖关系 - 依赖当前故事的其他故事
    public ICollection<StoryDependency> Dependents { get; set; } = new List<StoryDependency>();
}

/// <summary>
/// 故事依赖关系实体
/// 表示 Story A 依赖于 Story B（必须等 B 完成后才能开始 A）
/// </summary>
public class StoryDependency
{
    public int Id { get; set; }
    /// <summary>被阻塞的故事 ID</summary>
    public int StoryId { get; set; }
    /// <summary>依赖的故事 ID</summary>
    public int DependsOnStoryId { get; set; }
    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>被阻塞的故事</summary>
    public Story Story { get; set; } = null!;
    /// <summary>依赖的故事</summary>
    public Story DependsOnStory { get; set; } = null!;
}

/// <summary>
/// 迭代实体
/// </summary>
public class Iteration
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public int IterationNum { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public long? DurationMs { get; set; }
    public decimal? CostUsd { get; set; }
    public string Output { get; set; } = string.Empty;

    public Story Story { get; set; } = null!;
    public ICollection<Verification> Verifications { get; set; } = new List<Verification>();
    public ICollection<ExecutionOutput> ExecutionOutputs { get; set; } = new List<ExecutionOutput>();
}

/// <summary>
/// 计划实体
/// 存储AI生成的各类计划（需求计划、验收标准等）
/// </summary>
public class Plan
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    /// <summary>计划类型</summary>
    public PlanType Type { get; set; } = PlanType.DetailedPlan;
    /// <summary>版本号（同一类型可以有多个版本）</summary>
    public int Version { get; set; } = 1;
    /// <summary>计划内容（解析后的结构化内容）</summary>
    public string PlanContent { get; set; } = string.Empty;
    /// <summary>完整输出内容（AI原始输出）</summary>
    public string Output { get; set; } = string.Empty;
    /// <summary>是否为当前活跃版本</summary>
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Story Story { get; set; } = null!;
}

/// <summary>
/// 故事阶段枚举
/// </summary>
public enum StoryPhase
{
    /// <summary>初始状态</summary>
    Pending,
    /// <summary>计划分析阶段 - 生成详细计划</summary>
    RequirementsPlanning,
    /// <summary>验收标准阶段 - 细化验收标准</summary>
    AcceptancePlanning,
    /// <summary>执行阶段</summary>
    Executing,
    /// <summary>测试阶段</summary>
    Testing,
    /// <summary>验收阶段</summary>
    Acceptance,
    /// <summary>完成</summary>
    Completed,
    /// <summary>失败</summary>
    Failed
}

/// <summary>
/// 故事运行状态枚举
/// </summary>
public enum RunningStatus
{
    /// <summary>暂停</summary>
    Paused,
    /// <summary>排队等待中</summary>
    Pending,
    /// <summary>正在运行</summary>
    Running
}

/// <summary>
/// 计划类型枚举
/// </summary>
public enum PlanType
{
    /// <summary>详细实施计划（RequirementsPlanning 阶段生成）</summary>
    DetailedPlan,
    /// <summary>细化验收标准（AcceptancePlanning 阶段生成）</summary>
    AcceptanceCriteria,
    /// <summary>执行阶段日志（Executing/Testing 阶段生成，不覆盖 DetailedPlan）</summary>
    ExecutionLog,
}

/// <summary>
/// 故事队列类型
/// </summary>
public enum StoryQueueType
{
    /// <summary>计划队列 - RequirementsPlanning, AcceptancePlanning</summary>
    Planning,
    /// <summary>执行队列 - Executing, Testing, Acceptance</summary>
    Execution
}

/// <summary>
/// 故事来源枚举
/// </summary>
public enum StorySource
{
    /// <summary>用户手动添加</summary>
    UserAdded,
    /// <summary>自动发现</summary>
    AutoDiscovered
}

/// <summary>
/// 环境配置组实体（全局）
/// 包含一组环境变量配置，可被多个项目复用
/// </summary>
public class EnvConfigGroup
{
    public int Id { get; set; }
    /// <summary>配置组名称</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>配置组描述</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>环境变量列表</summary>
    public ICollection<EnvVariable> Variables { get; set; } = new List<EnvVariable>();
}

/// <summary>
/// 环境变量实体
/// 属于某个环境配置组
/// </summary>
public class EnvVariable
{
    public int Id { get; set; }
    /// <summary>所属环境配置组 ID</summary>
    public int EnvConfigGroupId { get; set; }
    /// <summary>环境变量名</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>环境变量值</summary>
    public string Value { get; set; } = string.Empty;
    /// <summary>是否必需（必需但不存在时报错）</summary>
    public bool IsRequired { get; set; } = false;
    /// <summary>默认值（可选变量不存在时使用）</summary>
    public string? DefaultValue { get; set; }

    public EnvConfigGroup EnvConfigGroup { get; set; } = null!;
}

/// <summary>
/// 环境变量规则实体
/// 定义在特定条件下使用哪个环境配置组
/// </summary>
public class EnvConfigRule
{
    public int Id { get; set; }
    /// <summary>所属项目 ID</summary>
    public int ProjectId { get; set; }
    /// <summary>要使用的环境配置组 ID</summary>
    public int EnvConfigGroupId { get; set; }
    /// <summary>起始阶段（如 "Pending"）</summary>
    public StoryPhase? FromPhase { get; set; }
    /// <summary>目标阶段（如 "Planning"）</summary>
    public StoryPhase? ToPhase { get; set; }
    /// <summary>标签匹配（JSON 数组，如 ["frontend", "api"]，空表示匹配所有）</summary>
    public string Tags { get; set; } = "[]";
    /// <summary>优先级（数字越大优先级越高）</summary>
    public int Priority { get; set; } = 0;
    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project Project { get; set; } = null!;
    public EnvConfigGroup EnvConfigGroup { get; set; } = null!;
}

/// <summary>
/// 验证实体
/// </summary>
public class Verification
{
    public int Id { get; set; }
    public int IterationId { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Output { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Iteration Iteration { get; set; } = null!;
}

/// <summary>
/// 故事事件实体
/// </summary>
public class StoryEvent
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string DataJson { get; set; } = string.Empty;

    public Story Story { get; set; } = null!;
}

/// <summary>
/// 执行输出实体
/// </summary>
public class ExecutionOutput
{
    public int Id { get; set; }
    public int IterationId { get; set; }

    /// <summary>发送的提示词</summary>
    public string Prompt { get; set; } = string.Empty;
    /// <summary>原始响应</summary>
    public string Response { get; set; } = string.Empty;
    /// <summary>解析后的输出</summary>
    public string ParsedOutput { get; set; } = string.Empty;

    public int? DurationMs { get; set; }
    public decimal? CostUsd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Iteration Iteration { get; set; } = null!;
}

/// <summary>
/// 阶段历史实体
/// 记录每个阶段的开始和结束时间，以及该阶段的输出
/// </summary>
public class PhaseHistory
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    /// <summary>阶段类型</summary>
    public StoryPhase Phase { get; set; }
    /// <summary>阶段开始时间</summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    /// <summary>阶段完成时间</summary>
    public DateTime? CompletedAt { get; set; }
    /// <summary>该阶段的原始输出</summary>
    public string Output { get; set; } = string.Empty;
    /// <summary>是否成功</summary>
    public bool Success { get; set; }

    public Story Story { get; set; } = null!;
}
