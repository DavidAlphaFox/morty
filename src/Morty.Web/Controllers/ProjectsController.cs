using Microsoft.AspNetCore.Mvc;
using Morty.Core.Repositories;
using Morty.Web.DTOs;

namespace Morty.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectRepository _projectRepository;
    private readonly IStoryRepository _storyRepository;

    public ProjectsController(IProjectRepository projectRepository, IStoryRepository storyRepository)
    {
        _projectRepository = projectRepository;
        _storyRepository = storyRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetProjects()
    {
        var projects = await _projectRepository.GetAllAsync();
        return Ok(projects.Select(p => new ProjectDto
        {
            Id = p.Id,
            Name = p.Name,
            WorkingDirectory = p.WorkingDirectory,
            PrdJson = p.PrdJson,
            CreatedAt = p.CreatedAt
        }));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectDto>> GetProject(int id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
            return NotFound();

        return Ok(new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            WorkingDirectory = project.WorkingDirectory,
            PrdJson = project.PrdJson,
            CreatedAt = project.CreatedAt
        });
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> CreateProject([FromBody] CreateProjectDto dto)
    {
        var project = new Core.Entities.Project
        {
            Name = dto.Name,
            WorkingDirectory = dto.WorkingDirectory,
            PrdJson = dto.PrdJson,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _projectRepository.AddAsync(project);

        return CreatedAtAction(nameof(GetProject), new { id = created.Id }, new ProjectDto
        {
            Id = created.Id,
            Name = created.Name,
            WorkingDirectory = created.WorkingDirectory,
            PrdJson = created.PrdJson,
            CreatedAt = created.CreatedAt
        });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProjectDto>> UpdateProject(int id, [FromBody] UpdateProjectDto dto)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
            return NotFound();

        if (dto.Name != null) project.Name = dto.Name;
        if (dto.WorkingDirectory != null) project.WorkingDirectory = dto.WorkingDirectory;
        if (dto.PrdJson != null) project.PrdJson = dto.PrdJson;

        await _projectRepository.UpdateAsync(project);

        return Ok(new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            WorkingDirectory = project.WorkingDirectory,
            PrdJson = project.PrdJson,
            CreatedAt = project.CreatedAt
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
            return NotFound();

        await _projectRepository.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("{id}/stories")]
    public async Task<ActionResult<IEnumerable<StoryDto>>> GetProjectStories(int id)
    {
        var stories = await _storyRepository.GetByProjectIdAsync(id);
        return Ok(stories.Select(s => new StoryDto
        {
            Id = s.Id,
            ProjectId = s.ProjectId,
            StoryId = s.StoryId,
            Title = s.Title,
            Priority = s.Priority,
            Status = s.Status,
            CreatedAt = s.CreatedAt,
            CompletedAt = s.CompletedAt
        }));
    }
}
