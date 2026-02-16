using Microsoft.AspNetCore.Mvc;
using Morty.Entities;
using Morty.Repositories;
using Morty.DTOs;
using Morty.Services;

namespace Morty.Controllers;

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
    private readonly IPlanRepository _planRepository;
    private readonly MortyLoopService _loopService;

    public StoriesController(
        IStoryRepository storyRepository,
        IProjectRepository projectRepository,
        IPlanRepository planRepository,
        MortyLoopService loopService)
    {
        _storyRepository = storyRepository;
        _projectRepository = projectRepository;
        _planRepository = planRepository;
        _loopService = loopService;
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

        return Ok(await MapToDtoWithPlans(story));
    }

    /// <summary>
    /// 创建新故事
    /// </summary>
    /// <param name="dto">故事创建数据</param>
    /// <returns>创建成功的故事，包含 201 Created 状态</returns>
    [HttpPost]
    public async Task<ActionResult<StoryDto>> CreateStory([FromBody] CreateStoryDto dto)
    {
        var isAutoDiscovered = dto.Source == StorySource.AutoDiscovered;

        var story = new Story
        {
            ProjectId = dto.ProjectId,
            StoryId = dto.StoryId,
            Title = dto.Title,
            Priority = dto.Priority,
            Status = isAutoDiscovered ? "Pending" : "Pending",
            Phase = StoryPhase.Pending,
            Source = dto.Source,
            // 用户添加的任务默认暂停，自动发现的任务默认排队等待
            RunningStatus = isAutoDiscovered ? RunningStatus.Pending : RunningStatus.Paused,
            CreatedAt = DateTime.UtcNow,
            // 支持创建时直接填写需求和验收标准
            Requirements = dto.Requirements ?? string.Empty,
            UserAcceptanceCriteria = dto.UserAcceptanceCriteria ?? string.Empty,
        };

        var created = await _storyRepository.AddAsync(story);

        // 添加依赖关系
        if (dto.Dependencies != null && dto.Dependencies.Count > 0)
        {
            foreach (var dependsOnId in dto.Dependencies)
            {
                var dependency = new StoryDependency
                {
                    StoryId = created.Id,
                    DependsOnStoryId = dependsOnId,
                    CreatedAt = DateTime.UtcNow
                };
                // 添加依赖关系（需要Repository支持）
                // await _storyRepository.AddDependencyAsync(dependency);
            }
        }

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
    [HttpPatch("{id}/user-acceptance")]
    public async Task<ActionResult<StoryDto>> UpdateUserAcceptanceCriteria(int id, [FromBody] UpdateUserAcceptanceCriteriaDto dto)
    {
        var story = await _storyRepository.GetByIdAsync(id);
        if (story == null)
            return NotFound();

        story.UserAcceptanceCriteria = dto.UserAcceptanceCriteria;
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
            case StoryPhase.Executing:
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
    /// 重新生成详细实施计划
    /// 将故事重置到 RequirementsPlanning 阶段，重新生成 DetailedPlan
    /// </summary>
    /// <param name="id">故事 ID</param>
    /// <returns>更新后的故事</returns>
    [HttpPost("{id}/regenerate-plan")]
    public async Task<ActionResult<StoryDto>> RegeneratePlan(int id)
    {
        var story = await _storyRepository.GetByIdAsync(id);
        if (story == null)
            return NotFound();

        if (string.IsNullOrEmpty(story.Requirements))
        {
            return BadRequest("请先设置用户需求 (Requirements)");
        }

        // 重置到 RequirementsPlanning 阶段，重新生成计划
        story.Phase = StoryPhase.RequirementsPlanning;
        story.CurrentIteration = 0;
        story.Status = "Planning";

        await _storyRepository.UpdateAsync(story);

        return Ok(await MapToDtoWithPlans(story));
    }

    /// <summary>
    /// 重新生成验收标准
    /// 将故事重置到 AcceptancePlanning 阶段，重新生成 AcceptanceCriteria
    /// </summary>
    /// <param name="id">故事 ID</param>
    /// <returns>更新后的故事</returns>
    [HttpPost("{id}/regenerate-acceptance")]
    public async Task<ActionResult<StoryDto>> RegenerateAcceptance(int id)
    {
        var story = await _storyRepository.GetByIdAsync(id);
        if (story == null)
            return NotFound();

        // 检查是否已有详细计划（从 Plan 表获取）
        var detailedPlan = await _planRepository.GetLatestByStoryIdAndTypeAsync(
            id, PlanType.DetailedPlan);
        if (detailedPlan == null)
        {
            return BadRequest("请先生成详细实施计划 (DetailedPlan)");
        }

        // 重置到 AcceptancePlanning 阶段，重新生成验收标准
        story.Phase = StoryPhase.AcceptancePlanning;
        story.CurrentIteration = 0;
        story.Status = "Planning";

        await _storyRepository.UpdateAsync(story);

        return Ok(await MapToDtoWithPlans(story));
    }

    /// <summary>
    /// 开始/恢复故事
    /// 将暂停的故事设置为可调度状态
    /// </summary>
    /// <param name="id">故事 ID</param>
    /// <returns>更新后的故事</returns>
    [HttpPost("{id}/start")]
    public async Task<ActionResult<StoryDto>> StartStory(int id)
    {
        var story = await _storyRepository.GetByIdAsync(id);
        if (story == null)
            return NotFound();

        if (story.Phase == StoryPhase.Completed || story.Phase == StoryPhase.Failed)
        {
            return BadRequest("已完成或失败的故事不能重新开始");
        }

        story.RunningStatus = RunningStatus.Pending;
        await _storyRepository.UpdateAsync(story);

        // 通知循环立即检查新任务
        _loopService.NotifyNewWork();

        return Ok(MapToDto(story));
    }

    /// <summary>
    /// 暂停故事
    /// 将故事设置为暂停状态，不再参与调度
    /// </summary>
    /// <param name="id">故事 ID</param>
    /// <returns>更新后的故事</returns>
    [HttpPost("{id}/pause")]
    public async Task<ActionResult<StoryDto>> PauseStory(int id)
    {
        var story = await _storyRepository.GetByIdAsync(id);
        if (story == null)
            return NotFound();

        if (story.Phase == StoryPhase.Completed || story.Phase == StoryPhase.Failed)
        {
            return BadRequest("已完成或失败的故事不能暂停");
        }

        story.RunningStatus = RunningStatus.Paused;
        await _storyRepository.UpdateAsync(story);

        return Ok(MapToDto(story));
    }

    /// <summary>
    /// 重新调度故事（从 Completed/Failed 回到 Pending）
    /// </summary>
    [HttpPost("{id}/reschedule")]
    public async Task<ActionResult<StoryDto>> RescheduleStory(int id)
    {
        var story = await _storyRepository.GetByIdAsync(id);
        if (story == null)
            return NotFound();

        if (story.Phase != StoryPhase.Completed && story.Phase != StoryPhase.Failed)
        {
            return BadRequest("只有已完成或失败的故事可以重新调度");
        }

        story.Round++;
        story.CurrentIteration = 0;
        story.Phase = StoryPhase.Pending;
        story.Status = "Pending";
        story.RunningStatus = RunningStatus.Paused;
        story.CompletedAt = null;

        await _storyRepository.UpdateAsync(story);

        return Ok(await MapToDtoWithPlans(story));
    }

    /// <summary>
    /// 获取故事的轮次摘要
    /// </summary>
    [HttpGet("{id}/rounds")]
    public async Task<ActionResult<IEnumerable<RoundSummaryDto>>> GetStoryRounds(int id)
    {
        var iterations = await _storyRepository.GetIterationsByStoryIdAsync(id);
        var roundSummaries = iterations
            .GroupBy(i => i.Round)
            .OrderBy(g => g.Key)
            .Select(g => new RoundSummaryDto
            {
                Round = g.Key,
                IterationCount = g.Count(),
                StartedAt = g.Min(i => i.StartedAt),
                CompletedAt = g.All(i => i.CompletedAt.HasValue) ? g.Max(i => i.CompletedAt) : null
            })
            .ToList();

        return Ok(roundSummaries);
    }

    /// <summary>
    /// 获取故事的所有迭代记录
    /// </summary>
    /// <param name="id">故事 ID</param>
    /// <param name="round">可选的轮次过滤</param>
    /// <returns>该故事的所有迭代列表</returns>
    [HttpGet("{id}/iterations")]
    public async Task<ActionResult<IEnumerable<IterationDto>>> GetStoryIterations(int id, [FromQuery] int? round = null)
    {
        var iterations = await _storyRepository.GetIterationsByStoryIdAsync(id);
        if (round.HasValue)
        {
            iterations = iterations.Where(i => i.Round == round.Value).ToList();
        }
        return Ok(iterations.Select(i => new IterationDto
        {
            Id = i.Id,
            StoryId = i.StoryId,
            IterationNum = i.IterationNum,
            Round = i.Round,
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
    /// 将 Story 实体映射为 StoryDto（同步版本，基本字段）
    /// </summary>
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
        RunningStatus = story.RunningStatus,
        Source = story.Source,
        Phase = story.Phase,
        Requirements = story.Requirements,
        UserAcceptanceCriteria = story.UserAcceptanceCriteria,
        CurrentIteration = story.CurrentIteration,
        Round = story.Round
    };

    /// <summary>
    /// 将 Story 实体映射为 StoryDto（包含从 Plan 表获取的 AI 生成内容）
    /// </summary>
    private async Task<StoryDto> MapToDtoWithPlans(Story story)
    {
        // 从 Plan 表获取 AI 生成的内容
        var detailedPlanRecord = await _planRepository.GetLatestByStoryIdAndTypeAsync(
            story.Id, PlanType.DetailedPlan);
        var acceptanceCriteriaRecord = await _planRepository.GetLatestByStoryIdAndTypeAsync(
            story.Id, PlanType.AcceptanceCriteria);

        var dto = MapToDto(story);
        dto.DetailedPlan = detailedPlanRecord?.PlanContent;
        dto.AcceptanceCriteria = acceptanceCriteriaRecord?.PlanContent;
        return dto;
    }
}
