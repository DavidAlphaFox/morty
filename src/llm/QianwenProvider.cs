// =============================================================================
// 百炼 (Qianwen) LLM Provider
// =============================================================================
// API 文档: https://dashscope.aliyuncs.com/
// 支持模型: qwen-turbo, qwen-plus, qwen-max, qwen-long, qwen2.5-coder, qwen2.5-vl
// =============================================================================

using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Morty.LLM;

/// <summary>
/// 百炼 (Qianwen) LLM Provider
/// </summary>
public class QianwenProvider : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _defaultModel;

    public ChatClientMetadata Metadata { get; }

    public static IReadOnlyList<string> SupportedModels => new[]
    {
        "qwen-turbo",
        "qwen-plus",
        "qwen-max",
        "qwen-long",
        "qwen2.5-coder",
        "qwen2.5-vl"
    };

    public QianwenProvider(string apiKey, string? baseUrl = null, string? defaultModel = null)
    {
        _baseUrl = baseUrl ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        _defaultModel = defaultModel ?? SupportedModels[0];

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        Metadata = new ChatClientMetadata("qianwen", new Uri(_baseUrl), _defaultModel);
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var model = options?.ModelId ?? _defaultModel;
        var payload = OpenAISerializer.BuildPayload(model, messages, options,
            extra: new Dictionary<string, object?> { ["enable_stream"] = false });

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/chat/completions",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<QianwenResponse>(cancellationToken: cancellationToken);
        return MapToChatResponse(result, model);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var model = options?.ModelId ?? _defaultModel;
        var payload = OpenAISerializer.BuildPayload(model, messages, options,
            extra: new Dictionary<string, object?> { ["enable_stream"] = true });
        payload["stream"] = true;

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            $"{_baseUrl}/chat/completions",
            content,
            cancellationToken);

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await foreach (var update in OpenAISerializer.ParseStreamAsync(stream, model, cancellationToken))
            yield return update;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceKey is null && serviceType.IsInstanceOfType(this))
            return this;

        return null;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static ChatResponse MapToChatResponse(QianwenResponse? response, string model)
    {
        if (response == null || response.Choices == null || response.Choices.Count == 0)
        {
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "")) { ModelId = model };
        }

        var choice = response.Choices.First();
        var message = OpenAISerializer.ParseAssistantMessage(
            choice.Message?.Content, choice.Message?.ToolCalls);

        return new ChatResponse(message)
        {
            ResponseId = response.Id,
            ModelId = model,
            FinishReason = MapFinishReason(choice.FinishReason),
            Usage = response.Usage != null ? new UsageDetails
            {
                InputTokenCount = response.Usage.PromptTokens,
                OutputTokenCount = response.Usage.CompletionTokens,
                TotalTokenCount = response.Usage.TotalTokens
            } : null
        };
    }

    private static ChatFinishReason? MapFinishReason(string? reason) =>
        OpenAISerializer.MapFinishReason(reason);
}

internal class QianwenResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("choices")]
    public List<QianwenChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public QianwenUsage? Usage { get; set; }
}

internal class QianwenChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public QianwenMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class QianwenMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<OpenAIToolCall>? ToolCalls { get; set; }
}

internal class QianwenUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

