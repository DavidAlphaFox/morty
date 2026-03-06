// =============================================================================
// 百炼 (Qianwen) LLM Provider
// =============================================================================
// API 文档: https://dashscope.aliyuncs.com/
// 支持模型: qwen-turbo, qwen-plus, qwen-max, qwen-long, qwen2.5-coder, qwen2.5-vl
// =============================================================================

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Morty.LLM;

/// <summary>
/// 百炼 (Qianwen) LLM Provider
/// </summary>
public class QianwenProvider : ILlmProvider
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
    public string Name => "qianwen";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedModels => new[]
    {
        "qwen-turbo",
        "qwen-plus",
        "qwen-max",
        "qwen-long",
        "qwen2.5-coder",
        "qwen2.5-vl"
    };

    /// <summary>
    /// 初始化百炼 Provider
    /// </summary>
    /// <param name="apiKey">API Key</param>
    /// <param name="baseUrl">自定义 API 地址 (可选)</param>
    public QianwenProvider(string apiKey, string? baseUrl = null)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";

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
            max_tokens = request.MaxTokens,
            enable_stream = false
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/chat/completions",
            payload,
            ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<QianwenResponse>(cancellationToken: ct);
        return MapToChatResponse(result);
    }

    public async IAsyncEnumerable<string> StreamChatAsync(ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new
        {
            model = request.Model,
            messages = request.Messages,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            enable_stream = true
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

                var chunk = JsonSerializer.Deserialize<QianwenStreamChunk>(data);
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

    private static ChatResponse MapToChatResponse(QianwenResponse? response)
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

internal class QianwenResponse
{
    public string Id { get; set; } = "";
    public string Object { get; set; } = "";
    public int Created { get; set; }
    public string Model { get; set; } = "";
    public List<QianwenChoice>? Choices { get; set; }
    public QianwenUsage? Usage { get; set; }
}

internal class QianwenChoice
{
    public int Index { get; set; }
    public QianwenMessage? Message { get; set; }
    public string? FinishReason { get; set; }
}

internal class QianwenMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

internal class QianwenUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

internal class QianwenStreamChunk
{
    public List<QianwenStreamChoice>? Choices { get; set; }
}

internal class QianwenStreamChoice
{
    public int Index { get; set; }
    public QianwenStreamDelta? Delta { get; set; }
    public string? FinishReason { get; set; }
}

internal class QianwenStreamDelta
{
    public string Content { get; set; } = "";
}
