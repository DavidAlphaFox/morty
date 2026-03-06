using System.Text.Json.Serialization;

namespace Morty.Agent;

public class Session
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("workingDirectory")]
    public string WorkingDirectory { get; set; } = "";

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("lastMessageId")]
    public string? LastMessageId { get; set; }
}

public class SessionMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("toolCalls")]
    public List<ToolCall>? ToolCalls { get; set; }

    [JsonPropertyName("toolResults")]
    public List<ToolResult>? ToolResults { get; set; }
}

public class ToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = "";
}

public class ToolResult
{
    [JsonPropertyName("toolCallId")]
    public string ToolCallId { get; set; } = "";

    [JsonPropertyName("result")]
    public string Result { get; set; } = "";
}
