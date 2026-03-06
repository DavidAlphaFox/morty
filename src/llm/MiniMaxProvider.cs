// =============================================================================
// MiniMax LLM Provider
// =============================================================================
// API 文档: https://platform.minimax.io/docs
// 支持模型: MiniMax-M2, MiniMax-M2.1
// 认证方式: HMAC-SHA256 签名
// =============================================================================

using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Morty.LLM;

/// <summary>
/// MiniMax LLM Provider
/// </summary>
public class MiniMaxProvider : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _defaultModel;

    public ChatClientMetadata Metadata { get; }

    public static IReadOnlyList<string> SupportedModels => new[]
    {
        "MiniMax-M2",
        "MiniMax-M2.1"
    };

    public MiniMaxProvider(string apiKey, string? baseUrl = null, string? defaultModel = null)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "https://api.minimax.io/v1";
        _defaultModel = defaultModel ?? SupportedModels[0];

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("X-Minimax-Api-Version", "2024-05-01");

        Metadata = new ChatClientMetadata("minimax", new Uri(_baseUrl), _defaultModel);
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var model = options?.ModelId ?? _defaultModel;
        var payload = OpenAISerializer.BuildPayload(model, messages, options);

        var response = await SendRequestAsync("POST", "/chat/completions", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MiniMaxResponse>(cancellationToken: cancellationToken);
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

    private async Task<HttpResponseMessage> SendRequestAsync(string method, string endpoint, object? body, CancellationToken ct = default)
    {
        var url = $"{_baseUrl}{endpoint}";
        var bodyJson = body != null ? JsonSerializer.Serialize(body) : "";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = Sign(_apiKey, method, url, timestamp, bodyJson);

        var request = new HttpRequestMessage(new HttpMethod(method), url);
        request.Headers.Add("Authorization", $"Bearer {_apiKey}:{timestamp}:{signature}");

        if (!string.IsNullOrEmpty(bodyJson))
        {
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        }

        return await _httpClient.SendAsync(request, ct);
    }

    private static string Sign(string apiKey, string method, string url, string timestamp, string body)
    {
        var signString = $"{method}\n{url}\n{timestamp}\n{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signString));
        return Convert.ToBase64String(hash);
    }

    private static ChatResponse MapToChatResponse(MiniMaxResponse? response, string model)
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

internal class MiniMaxResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("choices")]
    public List<MiniMaxChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public MiniMaxUsage? Usage { get; set; }
}

internal class MiniMaxChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public MiniMaxMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class MiniMaxMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<OpenAIToolCall>? ToolCalls { get; set; }
}

internal class MiniMaxUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

