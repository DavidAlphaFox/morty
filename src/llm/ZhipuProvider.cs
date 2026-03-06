// =============================================================================
// 智谱 (Zhipu) LLM Provider
// =============================================================================
// API 文档: https://open.bigmodel.cn/doc
// 支持模型: glm-5, glm-4.7, glm-4.5-air, glm-4, glm-4-flash, glm-4-plus, glm-4v-plus
// =============================================================================

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Morty.LLM;

/// <summary>
/// 智谱 LLM Provider
/// </summary>
public class ZhipuProvider : ILlmProvider
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
    public string Name => "zhipu";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedModels => new[]
    {
        "glm-5",
        "glm-4.7",
        "glm-4.5-air",
        "glm-4",
        "glm-4-flash",
        "glm-4-plus",
        "glm-4v-plus"
    };

    /// <summary>
    /// 初始化智谱 Provider
    /// </summary>
    /// <param name="apiKey">API Key</param>
    /// <param name="baseUrl">自定义 API 地址 (可选)</param>
    public ZhipuProvider(string apiKey, string? baseUrl = null)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "https://open.bigmodel.cn/api/paas/v4";

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        var payload = new
        {
            model = request.Model,
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
            temperature = request.Temperature,
            max_tokens = request.MaxTokens
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/chat/completions",
            payload,
            ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ZhipuResponse>(cancellationToken: ct);
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

                var chunk = JsonSerializer.Deserialize<ZhipuStreamChunk>(data);
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

    private static ChatResponse MapToChatResponse(ZhipuResponse? response)
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

internal class ZhipuResponse
{
    public string Id { get; set; } = "";
    public string Object { get; set; } = "";
    public int Created { get; set; }
    public string Model { get; set; } = "";
    public List<ZhipuChoice>? Choices { get; set; }
    public ZhipuUsage? Usage { get; set; }
}

internal class ZhipuChoice
{
    public int Index { get; set; }
    public ZhipuMessage? Message { get; set; }
    public string? FinishReason { get; set; }
}

internal class ZhipuMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

internal class ZhipuUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

internal class ZhipuStreamChunk
{
    public List<ZhipuStreamChoice>? Choices { get; set; }
}

internal class ZhipuStreamChoice
{
    public int Index { get; set; }
    public ZhipuStreamDelta? Delta { get; set; }
    public string? FinishReason { get; set; }
}

internal class ZhipuStreamDelta
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}
