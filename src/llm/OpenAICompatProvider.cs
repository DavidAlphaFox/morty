// =============================================================================
// 通用 OpenAI 兼容 Provider
// =============================================================================
// 支持任何兼容 OpenAI Chat Completions API 的服务
// 包括 OpenAI, DeepSeek, Ollama, vLLM 等
// =============================================================================

using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Morty.LLM;

/// <summary>
/// 通用 OpenAI 兼容 Provider
/// </summary>
public class OpenAICompatProvider : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _defaultModel;

    public ChatClientMetadata Metadata { get; }

    public OpenAICompatProvider(
        string apiKey,
        string baseUrl,
        string defaultModel = "gpt-4o",
        Dictionary<string, string>? headers = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _defaultModel = defaultModel;

        _httpClient = new HttpClient();
        if (!string.IsNullOrEmpty(apiKey))
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        // 自定义 headers
        if (headers != null)
        {
            foreach (var (key, value) in headers)
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
        }

        Metadata = new ChatClientMetadata("openai-compat", new Uri(_baseUrl), _defaultModel);
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var model = options?.ModelId ?? _defaultModel;
        var payload = OpenAISerializer.BuildPayload(model, messages, options);

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/chat/completions",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAICompatResponse>(
            cancellationToken: cancellationToken);
        return MapToChatResponse(result, model);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var model = options?.ModelId ?? _defaultModel;
        var payload = OpenAISerializer.BuildPayload(model, messages, options);
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

    private static ChatResponse MapToChatResponse(OpenAICompatResponse? response, string model)
    {
        if (response?.Choices == null || response.Choices.Count == 0)
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "")) { ModelId = model };

        var choice = response.Choices.First();
        var message = OpenAISerializer.ParseAssistantMessage(
            choice.Message?.Content, choice.Message?.ToolCalls);

        return new ChatResponse(message)
        {
            ResponseId = response.Id,
            ModelId = model,
            FinishReason = OpenAISerializer.MapFinishReason(choice.FinishReason),
            Usage = response.Usage != null ? new UsageDetails
            {
                InputTokenCount = response.Usage.PromptTokens,
                OutputTokenCount = response.Usage.CompletionTokens,
                TotalTokenCount = response.Usage.TotalTokens
            } : null
        };
    }
}

internal class OpenAICompatResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("choices")]
    public List<OpenAICompatChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public OpenAICompatUsage? Usage { get; set; }
}

internal class OpenAICompatChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public OpenAICompatMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class OpenAICompatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<OpenAIToolCall>? ToolCalls { get; set; }
}

internal class OpenAICompatUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
