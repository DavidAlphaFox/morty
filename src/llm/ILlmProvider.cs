namespace Morty.LLM;

public interface ILlmProvider
{
    string Name { get; }
    IReadOnlyList<string> SupportedModels { get; }

    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default);

    IAsyncEnumerable<string> StreamChatAsync(ChatRequest request, CancellationToken ct = default);

    Task<ChatResponse> ChatWithToolsAsync(ChatRequest request, IList<AgentTool> tools, CancellationToken ct = default);
}

public class ChatRequest
{
    public string Model { get; set; } = "";
    public List<ChatMessage> Messages { get; set; } = new();
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public List<string>? Tools { get; set; }
}

public class ChatMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
}

public class ChatResponse
{
    public string Content { get; set; } = "";
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
    public Usage? Usage { get; set; }
    public string? FinishReason { get; set; }
}

public class Usage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

public class AgentTool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public object? Parameters { get; set; }
}
