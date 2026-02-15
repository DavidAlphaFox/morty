using Microsoft.AspNetCore.Mvc;
using Morty.Core.Entities;
using Morty.Core.Repositories;
using Morty.Web.DTOs;

namespace Morty.Web.Controllers;

/// <summary>
/// 故事控制器
/// 处理用户故事相关的 HTTP 请求
/// 提供故事的 CRUD 操作和迭代/计划查询
/// </summary>
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

    /// <summary>
    /// 获取指定故事详情
    /// </summary>
    /// <param name="id">故事 ID</param>
    /// <returns>故事详情，如果不存在则返回 404</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<StoryDto>> GetStory(int id)
    {
        var story = await _storyRepository.GetByIdAsync(id);
        if (story == null)
            return NotFound();

        return Ok(MapToDto(story));
    }

    /// <summary>
    /// 创建新故事
    /// </summary>
    /// <param name="dto">故事创建数据</param>
    /// <returns>创建成功的故事，包含 201 Created 状态</returns>
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
            Phase = StoryPhase.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _storyRepository.AddAsync(story);

        return CreatedAtAction(nameof(GetStory), new { id = created.Id }, MapToDto(created));
    }

    /// <summary>
    /// 更新故事信息（部分更新）
    /// </summary>
    /// <param name="id">故事 ID</param>
    /// <param name="dto">更新数据</param>
    /// <returns>更新后的故事，如果不存在则返回 404</returns>
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

    /// <summary>
    /// 设置用户需求
    /// </summary>
    /// <param name="id">故事 ID</param>
    /// <param name="dto">需求数据</param>
    /// <returns>更新后的故事</returns>
    [HttpPatch("{id}/requirements")]
    public async Task<ActionResult<StoryDto>> UpdateRequirements(int id, [FromBody] UpdateRequirementsDto dto)
    {
        var story = await _storyRepository.GetByIdAsync(id);
        if (story == null)
            return NotFound();

        story.Requirements = dto.Requirements;
        await _storyRepository.UpdateAsync(story);

        return Ok(MapToDto(story));
    }

    /// <summary>
    /// 设置验收标准
    /// </summary>
    /// <param name="id">故事 ID</param>
    /// <param name="dto">验收标准数据</param>
    /// <returns>更新后的故事</returns>
    [HttpPatch("{id}/acceptance")]
    public async Task<ActionResult<StoryDto>> UpdateAcceptanceCriteria(int id, [FromBody] UpdateAcceptanceCriteriaDto dto)
    {
        var story = await _storyRepository.GetByIdAsync(id);
        if (story == null)
            return NotFound();

        story.AcceptanceCriteria = dto.AcceptanceCriteria;
        await _storyRepository.UpdateAsync(story);

        return Ok(MapToDto(story));
    }

    /// <summary>
    /// 开始指定阶段
    /// </summary>
    /// <param name="id">故事 ID</param>
    /// <param name="dto">阶段数据</param>
    /// <returns>更新后的故事</returns>
    [HttpPost("{id}/start-phase")]
    public async Task<ActionResult<StoryDto>> StartPhase(int id, [FromBody] StartPhaseDto dto)
    {
        var story = await _storyRepository.GetByIdAsync(id);
        if (story == null)
            return NotFound();

        story.Phase = dto.Phase;
        story.CurrentIteration = 0;
        story.Status = "InProgress";

        // 根据阶段设置相应的 Status
        switch (dto.Phase)
        {
            case StoryPhase.RequirementsPlanning:
            case StoryPhase.AcceptancePlanning:
                story.Status = "Planning";
                break;
            case StoryPhase.Coding:
            case StoryPhase.Testing:
                story.Status = "InProgress";
                break;
            case StoryPhase.Acceptance:
                story.Status = "Verifying";
                break;
        }

        await _storyRepository.UpdateAsync(story);

        return Ok(MapToDto(story));
    }

    /// <summary>
    /// 获取故事的所有迭代记录
    /// </summary>
    /// <param name="id">故事 ID</param>
    /// <returns>该故事的所有迭代列表</returns>
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

    /// <summary>
    /// 获取故事的最新计划
    /// </summary>
    /// <param name="id">故事 ID</param>
    /// <returns>计划详情，如果不存在则返回 404</returns>
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

    /// <summary>
    /// 将 Story 实体映射为 StoryDto
    /// </summary>
    /// <param name="story">Story 实体</param>
    /// <returns>StoryDto 数据传输对象</returns>
    private static StoryDto MapToDto(Story story) => new()
    {
        Id = story.Id,
        ProjectId = story.ProjectId,
        StoryId = story.StoryId,
        Title = story.Title,
        Priority = story.Priority,
        Status = story.Status,
        CreatedAt = story.CreatedAt,
        CompletedAt = story.CompletedAt,
        Phase = story.Phase,
        Requirements = story.Requirements,
        DetailedPlan = story.DetailedPlan,
        AcceptanceCriteria = story.AcceptanceCriteria,
        CurrentIteration = story.CurrentIteration
    };
}
