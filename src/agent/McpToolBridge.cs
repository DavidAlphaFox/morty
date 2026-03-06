// =============================================================================
// MCP 工具桥接
// =============================================================================
// 将 MCP Server 的工具转为 AIFunction
// =============================================================================

using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Morty.Tools;

namespace Morty.Agent;

/// <summary>
/// MCP 工具桥接 — 将 MCP Server 工具转为 AIFunction
/// </summary>
public static class McpToolBridge
{
    /// <summary>
    /// 将 MCP 工具列表转为 AIFunction 列表
    /// </summary>
    public static List<AIFunction> BridgeTools(McpClient client, List<McpToolDefinition> tools, string prefix = "mcp")
    {
        return tools.Select(tool => AIFunctionFactory.Create(
            ([Description("Tool arguments as JSON")] string argsJson) =>
                CallMcpToolAsync(client, tool.Name, argsJson),
            $"{prefix}_{tool.Name}",
            tool.Description
        )).ToList();
    }

    private static async Task<string> CallMcpToolAsync(McpClient client, string name, string argsJson)
    {
        Dictionary<string, object?>? args;
        try
        {
            args = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson) ?? new();
        }
        catch
        {
            args = new();
        }

        return await client.CallToolAsync(name, args);
    }
}
