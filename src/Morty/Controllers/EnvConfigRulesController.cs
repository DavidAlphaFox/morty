using Microsoft.AspNetCore.Mvc;
using Morty.Entities;
using Morty.Repositories;
using Morty.DTOs;
using System.Text.Json;

namespace Morty.Controllers;

/// <summary>
/// 环境变量规则控制器
/// 管理项目的环境变量规则
/// </summary>
[ApiController]
[Route("api/projects/{projectId}/[controller]")]
public class EnvConfigRulesController : ControllerBase
{
    private readonly IEnvConfigRuleRepository _ruleRepository;
    private readonly IEnvConfigGroupRepository _groupRepository;
    private readonly IProjectRepository _projectRepository;

    public EnvConfigRulesController(
        IEnvConfigRuleRepository ruleRepository,
        IEnvConfigGroupRepository groupRepository,
        IProjectRepository projectRepository)
    {
        _ruleRepository = ruleRepository;
        _groupRepository = groupRepository;
        _projectRepository = projectRepository;
    }

    /// <summary>
    /// 获取项目的所有环境变量规则
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<EnvConfigRuleDto>>> GetByProject(int projectId)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            return NotFound("Project not found");

        var rules = await _ruleRepository.GetByProjectIdAsync(projectId);
        return Ok(rules.Select(MapToDto).ToList());
    }

    /// <summary>
    /// 获取指定规则
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<EnvConfigRuleDto>> GetById(int projectId, int id)
    {
        var rule = await _ruleRepository.GetByIdAsync(id);
        if (rule == null || rule.ProjectId != projectId)
            return NotFound();

        return Ok(MapToDto(rule));
    }

    /// <summary>
    /// 创建新规则
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<EnvConfigRuleDto>> Create(int projectId, [FromBody] CreateEnvConfigRuleDto dto)
    {
        // 验证项目存在
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            return NotFound("Project not found");

        // 验证环境配置组存在
        var group = await _groupRepository.GetByIdAsync(dto.EnvConfigGroupId);
        if (group == null)
            return NotFound("EnvConfigGroup not found");

        var rule = new EnvConfigRule
        {
            ProjectId = projectId,
            EnvConfigGroupId = dto.EnvConfigGroupId,
            FromPhase = dto.FromPhase,
            ToPhase = dto.ToPhase,
            Tags = JsonSerializer.Serialize(dto.Tags),
            Priority = dto.Priority,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _ruleRepository.AddAsync(rule);

        // 重新加载以包含 EnvConfigGroup
        var loaded = await _ruleRepository.GetByIdAsync(created.Id);
        return CreatedAtAction(nameof(GetById), new { projectId, id = created.Id }, MapToDto(loaded!));
    }

    /// <summary>
    /// 更新规则
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<EnvConfigRuleDto>> Update(int projectId, int id, [FromBody] UpdateEnvConfigRuleDto dto)
    {
        var rule = await _ruleRepository.GetByIdAsync(id);
        if (rule == null || rule.ProjectId != projectId)
            return NotFound();

        if (dto.EnvConfigGroupId.HasValue)
        {
            // 验证环境配置组存在
            var group = await _groupRepository.GetByIdAsync(dto.EnvConfigGroupId.Value);
            if (group == null)
                return NotFound("EnvConfigGroup not found");

            rule.EnvConfigGroupId = dto.EnvConfigGroupId.Value;
        }

        if (dto.FromPhase.HasValue)
            rule.FromPhase = dto.FromPhase;

        if (dto.ToPhase.HasValue)
            rule.ToPhase = dto.ToPhase;

        if (dto.Tags != null)
            rule.Tags = JsonSerializer.Serialize(dto.Tags);

        if (dto.Priority.HasValue)
            rule.Priority = dto.Priority.Value;

        await _ruleRepository.UpdateAsync(rule);

        // 重新加载以包含 EnvConfigGroup
        var updated = await _ruleRepository.GetByIdAsync(id);
        return Ok(MapToDto(updated!));
    }

    /// <summary>
    /// 删除规则
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int projectId, int id)
    {
        var rule = await _ruleRepository.GetByIdAsync(id);
        if (rule == null || rule.ProjectId != projectId)
            return NotFound();

        await _ruleRepository.DeleteAsync(id);
        return NoContent();
    }

    private static EnvConfigRuleDto MapToDto(EnvConfigRule rule)
    {
        var tags = JsonSerializer.Deserialize<List<string>>(rule.Tags) ?? new List<string>();

        return new EnvConfigRuleDto
        {
            Id = rule.Id,
            ProjectId = rule.ProjectId,
            EnvConfigGroupId = rule.EnvConfigGroupId,
            FromPhase = rule.FromPhase,
            ToPhase = rule.ToPhase,
            Tags = tags,
            Priority = rule.Priority,
            CreatedAt = rule.CreatedAt,
            EnvConfigGroup = rule.EnvConfigGroup != null ? new EnvConfigGroupDto
            {
                Id = rule.EnvConfigGroup.Id,
                Name = rule.EnvConfigGroup.Name,
                Description = rule.EnvConfigGroup.Description,
                CreatedAt = rule.EnvConfigGroup.CreatedAt,
                Variables = rule.EnvConfigGroup.Variables.Select(v => new EnvVariableDto
                {
                    Id = v.Id,
                    EnvConfigGroupId = v.EnvConfigGroupId,
                    Key = v.Key,
                    Value = v.Value,
                    IsRequired = v.IsRequired,
                    DefaultValue = v.DefaultValue
                }).ToList()
            } : null
        };
    }
}
