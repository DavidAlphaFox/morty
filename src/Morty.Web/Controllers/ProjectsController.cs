using Microsoft.AspNetCore.Mvc;
using Morty.Core.Entities;
using Morty.Core.Repositories;
using Morty.Web.DTOs;

namespace Morty.Web.Controllers;

/// <summary>
/// 项目控制器
/// 处理项目相关的 HTTP 请求
/// 提供项目的 CRUD 操作和关联故事查询
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectRepository _projectRepository;
    private readonly IStoryRepository _storyRepository;
    private readonly IPlanRepository _planRepository;
    private readonly string _projectsRootDirectory;

    public ProjectsController(IProjectRepository projectRepository, IStoryRepository storyRepository, IPlanRepository planRepository, IConfiguration configuration)
    {
        _projectRepository = projectRepository;
        _storyRepository = storyRepository;
        _planRepository = planRepository;
        _projectsRootDirectory = configuration["ProjectsRootDirectory"] ?? "/home/david/workspace/morty-projects";
    }

    /// <summary>
    /// 获取所有项目列表
    /// </summary>
    /// <returns>所有项目的列表</returns>
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

    /// <summary>
    /// 获取指定项目详情
    /// </summary>
    /// <param name="id">项目 ID</param>
    /// <returns>项目详情，如果不存在则返回 404</returns>
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

    /// <summary>
    /// 创建新项目
    /// </summary>
    /// <param name="dto">项目创建数据</param>
    /// <returns>创建成功的项目，包含 201 Created 状态</returns>
    [HttpPost]
    public async Task<ActionResult<ProjectDto>> CreateProject([FromBody] CreateProjectDto dto)
    {
        // 自动生成工作目录：根目录 + 项目名称
        var sanitizedName = string.Join("-", dto.Name.Split(Path.GetInvalidFileNameChars()));
        var workingDirectory = Path.Combine(_projectsRootDirectory, sanitizedName);

        var project = new Core.Entities.Project
        {
            Name = dto.Name,
            WorkingDirectory = workingDirectory,
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

    /// <summary>
    /// 更新项目信息
    /// </summary>
    /// <param name="id">项目 ID</param>
    /// <param name="dto">更新数据</param>
    /// <returns>更新后的项目，如果不存在则返回 404</returns>
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

    /// <summary>
    /// 删除项目
    /// </summary>
    /// <param name="id">项目 ID</param>
    /// <returns>成功返回 204 No Content，不存在返回 404</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
            return NotFound();

        await _projectRepository.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// 获取项目的所有故事
    /// </summary>
    /// <param name="id">项目 ID</param>
    /// <returns>该项目下的所有故事列表</returns>
    [HttpGet("{id}/stories")]
    public async Task<ActionResult<IEnumerable<StoryDto>>> GetProjectStories(int id)
    {
        var stories = await _storyRepository.GetByProjectIdAsync(id);
        var dtos = new List<StoryDto>();
        foreach (var s in stories)
        {
            var detailedPlan = await _planRepository.GetLatestByStoryIdAndTypeAsync(s.Id, PlanType.DetailedPlan);
            var acceptanceCriteria = await _planRepository.GetLatestByStoryIdAndTypeAsync(s.Id, PlanType.AcceptanceCriteria);

            dtos.Add(new StoryDto
            {
                Id = s.Id,
                ProjectId = s.ProjectId,
                StoryId = s.StoryId,
                Title = s.Title,
                Priority = s.Priority,
                Status = s.Status,
                CreatedAt = s.CreatedAt,
                CompletedAt = s.CompletedAt,
                RunningStatus = s.RunningStatus,
                Source = s.Source,
                Phase = s.Phase,
                Requirements = s.Requirements,
                UserAcceptanceCriteria = s.UserAcceptanceCriteria,
                DetailedPlan = detailedPlan?.PlanContent,
                AcceptanceCriteria = acceptanceCriteria?.PlanContent,
                CurrentIteration = s.CurrentIteration
            });
        }
        return Ok(dtos);
    }
}
