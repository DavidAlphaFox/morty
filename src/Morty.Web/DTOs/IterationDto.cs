namespace Morty.Web.DTOs;

public class IterationDto
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public int IterationNum { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? DurationMs { get; set; }
    public string Output { get; set; } = string.Empty;
}

public class VerificationDto
{
    public int Id { get; set; }
    public int IterationId { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Output { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class PlanDto
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public string PlanContent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
