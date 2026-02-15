using System.Text.Json.Serialization;
using Morty.Core.Entities;

namespace Morty.Web.DTOs;

public class StoryDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string StoryId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // 调度控制字段
    public bool IsPaused { get; set; } = true;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StorySource Source { get; set; } = StorySource.UserAdded;

    // 多阶段处理相关字段
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StoryPhase Phase { get; set; } = StoryPhase.Pending;
    /// <summary>用户需求（用户输入）</summary>
    public string Requirements { get; set; } = string.Empty;
    /// <summary>用户验收标准（用户输入）</summary>
    public string UserAcceptanceCriteria { get; set; } = string.Empty;
    public int CurrentIteration { get; set; } = 0;

    // AI生成的计划（从Plans表获取最新版本）
    /// <summary>详细实施计划（AI生成，可能为空）</summary>
    public string? DetailedPlan { get; set; }
    /// <summary>细化验收标准（AI生成，可能为空）</summary>
    public string? AcceptanceCriteria { get; set; }

    // 依赖关系
    public List<int> Dependencies { get; set; } = new();
}

public class CreateStoryDto
{
    public int ProjectId { get; set; }
    public string StoryId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StorySource Source { get; set; } = StorySource.UserAdded;

    // 创建时可以直接填写需求和验收标准
    public string? Requirements { get; set; }
    public string? UserAcceptanceCriteria { get; set; }

    // 依赖的任务ID列表（父任务）
    public List<int>? Dependencies { get; set; }
}

public class UpdateStoryDto
{
    public string? Status { get; set; }
    public string? Priority { get; set; }
}

public class UpdateRequirementsDto
{
    public string Requirements { get; set; } = string.Empty;
}

public class UpdateUserAcceptanceCriteriaDto
{
    public string UserAcceptanceCriteria { get; set; } = string.Empty;
}

public class StartPhaseDto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StoryPhase Phase { get; set; }
}

public class AddDependencyDto
{
    public int DependsOnStoryId { get; set; }
}

public class RemoveDependencyDto
{
    public int DependsOnStoryId { get; set; }
}

public class PlanDto
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlanType Type { get; set; }
    public int Version { get; set; }
    public string PlanContent { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
