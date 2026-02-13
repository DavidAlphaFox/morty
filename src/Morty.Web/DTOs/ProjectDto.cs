namespace Morty.Web.DTOs;

public class ProjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string PrdJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateProjectDto
{
    public string Name { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string PrdJson { get; set; } = string.Empty;
}

public class UpdateProjectDto
{
    public string? Name { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? PrdJson { get; set; }
}
