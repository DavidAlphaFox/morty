using Morty.Core.Entities;

namespace Morty.Web.DTOs;

/// <summary>
/// 环境配置组 DTO
/// </summary>
public class EnvConfigGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<EnvVariableDto> Variables { get; set; } = new();
}

/// <summary>
/// 环境变量 DTO
/// </summary>
public class EnvVariableDto
{
    public int Id { get; set; }
    public int EnvConfigGroupId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = false;
    public string? DefaultValue { get; set; }
}

/// <summary>
/// 创建环境配置组 DTO
/// </summary>
public class CreateEnvConfigGroupDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<CreateEnvVariableDto> Variables { get; set; } = new();
}

/// <summary>
/// 创建环境变量 DTO
/// </summary>
public class CreateEnvVariableDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = false;
    public string? DefaultValue { get; set; }
}

/// <summary>
/// 更新环境配置组 DTO
/// </summary>
public class UpdateEnvConfigGroupDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<CreateEnvVariableDto>? Variables { get; set; }
}

/// <summary>
/// 环境变量规则 DTO
/// </summary>
public class EnvConfigRuleDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int EnvConfigGroupId { get; set; }
    public StoryPhase? FromPhase { get; set; }
    public StoryPhase? ToPhase { get; set; }
    public List<string> Tags { get; set; } = new();
    public int Priority { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public EnvConfigGroupDto? EnvConfigGroup { get; set; }
}

/// <summary>
/// 创建环境变量规则 DTO
/// </summary>
public class CreateEnvConfigRuleDto
{
    public int ProjectId { get; set; }
    public int EnvConfigGroupId { get; set; }
    public StoryPhase? FromPhase { get; set; }
    public StoryPhase? ToPhase { get; set; }
    public List<string> Tags { get; set; } = new();
    public int Priority { get; set; } = 0;
}

/// <summary>
/// 更新环境变量规则 DTO
/// </summary>
public class UpdateEnvConfigRuleDto
{
    public int? EnvConfigGroupId { get; set; }
    public StoryPhase? FromPhase { get; set; }
    public StoryPhase? ToPhase { get; set; }
    public List<string>? Tags { get; set; }
    public int? Priority { get; set; }
}
