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

    public ICollection<Story> Stories { get; set; } = new List<Story>();
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
    /// <summary>用户需求（原始 PRD）</summary>
    public string Requirements { get; set; } = string.Empty;
    /// <summary>详细实施计划</summary>
    public string DetailedPlan { get; set; } = string.Empty;
    /// <summary>细化后的验收标准</summary>
    public string AcceptanceCriteria { get; set; } = string.Empty;
    /// <summary>当前阶段内的迭代次数</summary>
    public int CurrentIteration { get; set; } = 0;

    public Project Project { get; set; } = null!;
    public ICollection<Iteration> Iterations { get; set; } = new List<Iteration>();
    public ICollection<Plan> Plans { get; set; } = new List<Plan>();
    public ICollection<StoryEvent> Events { get; set; } = new List<StoryEvent>();
    public ICollection<PhaseHistory> PhaseHistories { get; set; } = new List<PhaseHistory>();
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
/// </summary>
public class Plan
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public string PlanContent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>计划类型（规划/执行）</summary>
    public PlanType Type { get; set; } = PlanType.Planning;
    /// <summary>完整输出内容</summary>
    public string Output { get; set; } = string.Empty;

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
    /// <summary>编码阶段</summary>
    Coding,
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
/// 计划类型枚举
/// </summary>
public enum PlanType
{
    /// <summary>需求计划阶段</summary>
    RequirementsPlanning,
    /// <summary>验收标准阶段</summary>
    AcceptancePlanning,
    /// <summary>编码阶段</summary>
    Coding,
    /// <summary>测试阶段</summary>
    Testing,
    /// <summary>验收阶段</summary>
    Acceptance,
    /// <summary>规划阶段（兼容旧数据）</summary>
    Planning,
    /// <summary>执行阶段（兼容旧数据）</summary>
    Execution
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
