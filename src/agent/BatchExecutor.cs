// =============================================================================
// Batch 工具执行器
// =============================================================================
// 支持并行执行多个工具调用
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Morty.Agent;

/// <summary>
/// 批量工具调用描述
/// </summary>
public class BatchCall
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = "";

    [JsonPropertyName("args")]
    public Dictionary<string, object?> Args { get; set; } = new();
}

/// <summary>
/// Batch 执行器 — 并行执行多个工具调用
/// </summary>
public class BatchExecutor
{
    private readonly List<AIFunction> _tools;
    private readonly PermissionChecker? _permissionChecker;
    private const int MaxConcurrent = 25;

    public BatchExecutor(List<AIFunction> tools, PermissionChecker? permissionChecker = null)
    {
        _tools = tools;
        _permissionChecker = permissionChecker;
    }

    /// <summary>
    /// 并行执行批量工具调用
    /// </summary>
    public async Task<string> ExecuteAsync(string callsJson)
    {
        List<BatchCall>? calls;
        try
        {
            calls = JsonSerializer.Deserialize<List<BatchCall>>(callsJson);
        }
        catch (JsonException ex)
        {
            return $"Error: Invalid JSON — {ex.Message}";
        }

        if (calls == null || calls.Count == 0)
            return "Error: No tool calls provided";
        if (calls.Count > MaxConcurrent)
            return $"Error: Maximum {MaxConcurrent} concurrent calls allowed, got {calls.Count}";

        var tasks = calls.Select(async (call, i) =>
        {
            var tool = _tools.FirstOrDefault(t => t.Name == call.Tool);
            if (tool == null)
                return $"[{i}] Error: Unknown tool '{call.Tool}'";

            // 权限检查
            if (_permissionChecker != null)
            {
                var allowed = await _permissionChecker.CheckAsync(call.Tool, call.Args);
                if (!allowed)
                    return $"[{i}] {call.Tool}: Permission denied";
            }

            try
            {
                var args = new AIFunctionArguments(call.Args);
                var result = await tool.InvokeAsync(args);
                var output = result?.ToString() ?? "(no output)";

                // 截断单个结果
                if (output.Length > 10000)
                    output = output[..10000] + "\n[Truncated]";

                return $"[{i}] {call.Tool}: {output}";
            }
            catch (Exception ex)
            {
                return $"[{i}] {call.Tool} Error: {ex.Message}";
            }
        });

        var results = await Task.WhenAll(tasks);
        var success = results.Count(r => !r.Contains("Error:"));

        return $"Batch: {success}/{calls.Count} succeeded\n\n" +
               string.Join("\n\n", results);
    }
}
