using Microsoft.AspNetCore.Mvc;
using Morty.Entities;
using Morty.Repositories;
using Morty.DTOs;
using System.Text.Json;

namespace Morty.Controllers;

/// <summary>
/// 环境配置组控制器
/// 管理全局环境配置组
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EnvConfigGroupsController : ControllerBase
{
    private readonly IEnvConfigGroupRepository _repository;

    public EnvConfigGroupsController(IEnvConfigGroupRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// 获取所有环境配置组
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<EnvConfigGroupDto>>> GetAll()
    {
        var groups = await _repository.GetAllAsync();
        return Ok(groups.Select(MapToDto).ToList());
    }

    /// <summary>
    /// 获取指定环境配置组
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<EnvConfigGroupDto>> GetById(int id)
    {
        var group = await _repository.GetByIdAsync(id);
        if (group == null)
            return NotFound();

        return Ok(MapToDto(group));
    }

    /// <summary>
    /// 创建新环境配置组
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<EnvConfigGroupDto>> Create([FromBody] CreateEnvConfigGroupDto dto)
    {
        var group = new EnvConfigGroup
        {
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow,
            Variables = dto.Variables.Select(v => new EnvVariable
            {
                Key = v.Key,
                Value = v.Value,
                IsRequired = v.IsRequired,
                DefaultValue = v.DefaultValue
            }).ToList()
        };

        var created = await _repository.AddAsync(group);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
    }

    /// <summary>
    /// 更新环境配置组
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<EnvConfigGroupDto>> Update(int id, [FromBody] UpdateEnvConfigGroupDto dto)
    {
        var group = await _repository.GetByIdAsync(id);
        if (group == null)
            return NotFound();

        if (dto.Name != null)
            group.Name = dto.Name;

        if (dto.Description != null)
            group.Description = dto.Description;

        if (dto.Variables != null)
        {
            // 替换所有变量
            group.Variables = dto.Variables.Select(v => new EnvVariable
            {
                EnvConfigGroupId = group.Id,
                Key = v.Key,
                Value = v.Value,
                IsRequired = v.IsRequired,
                DefaultValue = v.DefaultValue
            }).ToList();
        }

        await _repository.UpdateAsync(group);

        // 重新加载以获取更新后的数据
        var updated = await _repository.GetByIdAsync(id);
        return Ok(MapToDto(updated!));
    }

    /// <summary>
    /// 删除环境配置组
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var group = await _repository.GetByIdAsync(id);
        if (group == null)
            return NotFound();

        await _repository.DeleteAsync(id);
        return NoContent();
    }

    private static EnvConfigGroupDto MapToDto(EnvConfigGroup group)
    {
        return new EnvConfigGroupDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            CreatedAt = group.CreatedAt,
            Variables = group.Variables.Select(v => new EnvVariableDto
            {
                Id = v.Id,
                EnvConfigGroupId = v.EnvConfigGroupId,
                Key = v.Key,
                Value = v.Value,
                IsRequired = v.IsRequired,
                DefaultValue = v.DefaultValue
            }).ToList()
        };
    }
}
