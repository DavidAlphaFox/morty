// =============================================================================
// Agent 定义
// =============================================================================
// 支持多种 Agent 角色，每个有独立的工具集、权限和系统提示
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Morty.Agent;

/// <summary>
/// Agent 定义
/// </summary>
public class AgentDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "build";

    [JsonPropertyName("tools")]
    public List<string>? Tools { get; set; }

    [JsonPropertyName("systemPrompt")]
    public string? SystemPrompt { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    public AgentMode GetAgentMode() => Mode.ToLower() switch
    {
        "plan" => AgentMode.Plan,
        _ => AgentMode.Build
    };
}

/// <summary>
/// 内置 Agent 定义
/// </summary>
public static class BuiltinAgents
{
    public static readonly AgentDefinition Build = new()
    {
        Name = "build",
        Description = "Full-access development agent",
        Mode = "build"
    };

    public static readonly AgentDefinition Plan = new()
    {
        Name = "plan",
        Description = "Read-only exploration and planning agent",
        Mode = "plan",
        Tools = new() { "read", "grep", "glob", "ls", "bash", "webfetch" }
    };

    public static readonly AgentDefinition Explore = new()
    {
        Name = "explore",
        Description = "Fast search and analysis agent",
        Mode = "plan",
        Tools = new() { "read", "grep", "glob", "ls" }
    };

    /// <summary>
    /// 获取所有内置 agent 定义
    /// </summary>
    public static List<AgentDefinition> All => new() { Build, Plan, Explore };

    /// <summary>
    /// 加载所有 agent 定义 (内置 + 自定义)
    /// </summary>
    public static List<AgentDefinition> LoadAll(string cwd)
    {
        var agents = new List<AgentDefinition>(All);

        var customDir = Path.Combine(cwd, ".morty", "agents");
        if (Directory.Exists(customDir))
        {
            foreach (var file in Directory.GetFiles(customDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var agent = JsonSerializer.Deserialize<AgentDefinition>(json);
                    if (agent != null && !string.IsNullOrEmpty(agent.Name))
                        agents.Add(agent);
                }
                catch
                {
                    // 加载失败的自定义 agent 跳过
                }
            }
        }

        return agents;
    }

    /// <summary>
    /// 按名称查找 agent
    /// </summary>
    public static AgentDefinition? FindByName(string name, string cwd)
    {
        return LoadAll(cwd).FirstOrDefault(a =>
            a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
