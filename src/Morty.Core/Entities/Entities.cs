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

    public Project Project { get; set; } = null!;
    public ICollection<Iteration> Iterations { get; set; } = new List<Iteration>();
    public ICollection<Plan> Plans { get; set; } = new List<Plan>();
    public ICollection<StoryEvent> Events { get; set; } = new List<StoryEvent>();
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

    /// <summary>关联的供应商</summary>
    public int? ProviderId { get; set; }

    public Story Story { get; set; } = null!;
    public Provider? Provider { get; set; }
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
    /// <summary>关联的供应商</summary>
    public int? ProviderId { get; set; }
    /// <summary>完整输出内容</summary>
    public string Output { get; set; } = string.Empty;

    public Story Story { get; set; } = null!;
    public Provider? Provider { get; set; }
}

/// <summary>
/// 计划类型枚举
/// </summary>
public enum PlanType
{
    /// <summary>规划阶段</summary>
    Planning,
    /// <summary>执行阶段</summary>
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
/// 供应商实体
/// </summary>
public class Provider
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>供应商类型 (anthropic, openai, azure, cli)</summary>
    public string Type { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    /// <summary>API 令牌（生产环境应加密）</summary>
    public string Token { get; set; } = string.Empty;
    /// <summary>额外配置 (temperature 等)</summary>
    public string ConfigJson { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Iteration> Iterations { get; set; } = new List<Iteration>();
    public ICollection<Plan> Plans { get; set; } = new List<Plan>();
    public ICollection<ExecutionOutput> ExecutionOutputs { get; set; } = new List<ExecutionOutput>();
}

/// <summary>
/// 执行输出实体
/// </summary>
public class ExecutionOutput
{
    public int Id { get; set; }
    public int IterationId { get; set; }
    public int? ProviderId { get; set; }

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
    public Provider? Provider { get; set; }
}
