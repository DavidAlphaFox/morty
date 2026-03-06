using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Morty.Agent;

/// <summary>
/// Todo 项
/// </summary>
public class TodoItem
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "medium";
}

/// <summary>
/// 会话级 Todo 列表管理
/// </summary>
public class TodoList
{
    private List<TodoItem> _items = new();

    /// <summary>
    /// 当前 Todo 列表
    /// </summary>
    public IReadOnlyList<TodoItem> Items => _items;

    /// <summary>
    /// 读取当前 Todo 列表
    /// </summary>
    public string Read()
    {
        if (_items.Count == 0)
            return "No todos yet.";

        var sb = new StringBuilder();
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var icon = item.Status switch
            {
                "completed" => "[x]",
                "in_progress" => "[>]",
                "cancelled" => "[-]",
                _ => "[ ]"
            };
            var priority = item.Priority switch
            {
                "high" => " (HIGH)",
                "low" => " (low)",
                _ => ""
            };
            sb.AppendLine($"{i + 1}. {icon} {item.Content}{priority}");
        }

        var pending = _items.Count(i => i.Status == "pending");
        var inProgress = _items.Count(i => i.Status == "in_progress");
        var completed = _items.Count(i => i.Status == "completed");
        sb.AppendLine();
        sb.Append($"Summary: {completed}/{_items.Count} completed, {inProgress} in progress, {pending} pending");

        return sb.ToString();
    }

    /// <summary>
    /// 更新 Todo 列表 (替换整个列表)
    /// </summary>
    public string Write(string todosJson)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<TodoItem>>(todosJson);
            if (items == null)
                return "Error: invalid JSON — expected array of {content, status, priority} objects.";

            _items = items;
            return Read();
        }
        catch (JsonException ex)
        {
            return $"Error: failed to parse todos JSON: {ex.Message}";
        }
    }
}
