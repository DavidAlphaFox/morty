// =============================================================================
// MiniMax LLM Provider
// =============================================================================
// API 文档: https://platform.minimax.io/docs
// 支持模型: MiniMax-M2, MiniMax-M2.1
// 认证方式: HMAC-SHA256 签名
// =============================================================================

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Morty.LLM;

/// <summary>
/// MiniMax LLM Provider
/// </summary>
public class MiniMaxProvider : ILlmProvider
{
    /// <summary>
    /// HTTP 客户端
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// API Key
    /// </summary>
    private readonly string _apiKey;

    /// <summary>
    /// API 基础地址
    /// </summary>
    private readonly string _baseUrl;

    /// <inheritdoc/>
    public string Name => "minimax";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedModels => new[]
    {
        "MiniMax-M2",
        "MiniMax-M2.1"
    };

    /// <summary>
    /// 初始化 MiniMax Provider
    /// </summary>
    /// <param name="apiKey">API Key</param>
    /// <param name="baseUrl">自定义 API 地址 (可选)</param>
    public MiniMaxProvider(string apiKey, string? baseUrl = null)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "https://api.minimax.io/v1";

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("X-Minimax-Api-Version", "2024-05-01");
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        var payload = new
        {
            model = request.Model,
            messages = request.Messages,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens
        };

        var response = await SendRequestAsync("POST", "/chat/completions", payload, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MiniMaxResponse>(cancellationToken: ct);
        return MapToChatResponse(result);
    }

    public async IAsyncEnumerable<string> StreamChatAsync(ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new
        {
            model = request.Model,
            messages = request.Messages,
            stream = true
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            $"{_baseUrl}/chat/completions",
            content,
            ct);

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line?.StartsWith("data: ") == true)
            {
                var data = line[6..];
                if (data == "[DONE]") yield break;

                var chunk = JsonSerializer.Deserialize<MiniMaxStreamChunk>(data);
                if (chunk?.Choices?.First()?.Delta?.Content is { } text)
                {
                    yield return text;
                }
            }
        }
    }

    public Task<ChatResponse> ChatWithToolsAsync(ChatRequest request, IList<AgentTool> tools, CancellationToken ct = default)
    {
        throw new NotImplementedException();
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

    private static ChatResponse MapToChatResponse(MiniMaxResponse? response)
    {
        if (response == null || response.Choices == null || response.Choices.Count == 0)
        {
            return new ChatResponse { Content = "" };
        }

        var choice = response.Choices.First();
        return new ChatResponse
        {
            Content = choice.Message?.Content ?? "",
            Usage = response.Usage != null ? new Usage
            {
                PromptTokens = response.Usage.PromptTokens,
                CompletionTokens = response.Usage.CompletionTokens,
                TotalTokens = response.Usage.TotalTokens
            } : null,
            FinishReason = choice.FinishReason
        };
    }
}

internal class MiniMaxResponse
{
    public string Id { get; set; } = "";
    public string Object { get; set; } = "";
    public int Created { get; set; }
    public string Model { get; set; } = "";
    public List<MiniMaxChoice>? Choices { get; set; }
    public MiniMaxUsage? Usage { get; set; }
}

internal class MiniMaxChoice
{
    public int Index { get; set; }
    public MiniMaxMessage? Message { get; set; }
    public string? FinishReason { get; set; }
}

internal class MiniMaxMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

internal class MiniMaxUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

internal class MiniMaxStreamChunk
{
    public List<MiniMaxStreamChoice>? Choices { get; set; }
}

internal class MiniMaxStreamChoice
{
    public int Index { get; set; }
    public MiniMaxStreamDelta? Delta { get; set; }
    public string? FinishReason { get; set; }
}

internal class MiniMaxStreamDelta
{
    public string Content { get; set; } = "";
}
