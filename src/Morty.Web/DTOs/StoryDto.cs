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

    // 多阶段处理相关字段
    public StoryPhase Phase { get; set; } = StoryPhase.Pending;
    public string Requirements { get; set; } = string.Empty;
    public string DetailedPlan { get; set; } = string.Empty;
    public string AcceptanceCriteria { get; set; } = string.Empty;
    public int CurrentIteration { get; set; } = 0;
}

public class CreateStoryDto
{
    public int ProjectId { get; set; }
    public string StoryId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
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

public class UpdateAcceptanceCriteriaDto
{
    public string AcceptanceCriteria { get; set; } = string.Empty;
}

public class StartPhaseDto
{
    public StoryPhase Phase { get; set; }
}
