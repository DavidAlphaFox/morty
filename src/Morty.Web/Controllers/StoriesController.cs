using Microsoft.AspNetCore.Mvc;
using Morty.Core.Entities;
using Morty.Core.Repositories;
using Morty.Web.DTOs;

namespace Morty.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoriesController : ControllerBase
{
    private readonly IStoryRepository _storyRepository;
    private readonly IProjectRepository _projectRepository;

    public StoriesController(IStoryRepository storyRepository, IProjectRepository projectRepository)
    {
        _storyRepository = storyRepository;
        _projectRepository = projectRepository;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StoryDto>> GetStory(int id)
    {
        var story = await _storyRepository.GetByIdAsync(id);
        if (story == null)
            return NotFound();

        return Ok(MapToDto(story));
    }

    [HttpPost]
    public async Task<ActionResult<StoryDto>> CreateStory([FromBody] CreateStoryDto dto)
    {
        var story = new Story
        {
            ProjectId = dto.ProjectId,
            StoryId = dto.StoryId,
            Title = dto.Title,
            Priority = dto.Priority,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        var created = await _storyRepository.AddAsync(story);

        return CreatedAtAction(nameof(GetStory), new { id = created.Id }, MapToDto(created));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<StoryDto>> UpdateStory(int id, [FromBody] UpdateStoryDto dto)
    {
        var story = await _storyRepository.GetByIdAsync(id);
        if (story == null)
            return NotFound();

        if (dto.Status != null) story.Status = dto.Status;
        if (dto.Priority != null) story.Priority = dto.Priority;

        await _storyRepository.UpdateAsync(story);

        return Ok(MapToDto(story));
    }

    [HttpGet("{id}/iterations")]
    public async Task<ActionResult<IEnumerable<IterationDto>>> GetStoryIterations(int id)
    {
        var iterations = await _storyRepository.GetIterationsByStoryIdAsync(id);
        return Ok(iterations.Select(i => new IterationDto
        {
            Id = i.Id,
            StoryId = i.StoryId,
            IterationNum = i.IterationNum,
            StartedAt = i.StartedAt,
            CompletedAt = i.CompletedAt,
            DurationMs = i.DurationMs,
            Output = i.Output
        }));
    }

    [HttpGet("{id}/plan")]
    public async Task<ActionResult<PlanDto>> GetStoryPlan(int id)
    {
        var plan = await _storyRepository.GetPlanByStoryIdAsync(id);
        if (plan == null)
            return NotFound();

        return Ok(new PlanDto
        {
            Id = plan.Id,
            StoryId = plan.StoryId,
            PlanContent = plan.PlanContent,
            CreatedAt = plan.CreatedAt
        });
    }

    private static StoryDto MapToDto(Story story) => new()
    {
        Id = story.Id,
        ProjectId = story.ProjectId,
        StoryId = story.StoryId,
        Title = story.Title,
        Priority = story.Priority,
        Status = story.Status,
        CreatedAt = story.CreatedAt,
        CompletedAt = story.CompletedAt
    };
}
