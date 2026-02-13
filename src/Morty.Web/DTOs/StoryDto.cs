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
