namespace Morty.Core.Entities;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string PrdJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Story> Stories { get; set; } = new List<Story>();
}

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
}

public class Plan
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public string PlanContent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Story Story { get; set; } = null!;
}

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

public class StoryEvent
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string DataJson { get; set; } = string.Empty;

    public Story Story { get; set; } = null!;
}
