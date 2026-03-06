// =============================================================================
// OpenAI 兼容格式序列化
// =============================================================================
// 智谱、MiniMax、通义千问均使用 OpenAI 兼容的 API 格式
// 此文件提供共用的消息和工具序列化/反序列化逻辑
// =============================================================================

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Morty.LLM;

/// <summary>
/// OpenAI 兼容格式序列化工具
/// </summary>
internal static partial class OpenAISerializer
{
    /// <summary>
    /// 将 ChatMessage 列表序列化为 OpenAI 兼容的 messages 数组
    /// </summary>
    public static List<object> SerializeMessages(IEnumerable<ChatMessage> messages)
    {
        var result = new List<object>();
        foreach (var msg in messages)
        {
            var functionCalls = msg.Contents.OfType<FunctionCallContent>().ToList();
            var functionResults = msg.Contents.OfType<FunctionResultContent>().ToList();

            if (functionCalls.Count > 0)
            {
                // assistant 消息附带 tool_calls
                result.Add(new Dictionary<string, object?>
                {
                    ["role"] = msg.Role.Value,
                    ["content"] = msg.Text,
                    ["tool_calls"] = functionCalls.Select(fc => new Dictionary<string, object>
                    {
                        ["id"] = fc.CallId,
                        ["type"] = "function",
                        ["function"] = new Dictionary<string, object>
                        {
                            ["name"] = fc.Name,
                            ["arguments"] = fc.Arguments != null
                                ? JsonSerializer.Serialize(fc.Arguments)
                                : "{}"
                        }
                    }).ToList()
                });
            }
            else if (functionResults.Count > 0)
            {
                // tool 结果消息 — 每个结果一条
                foreach (var fr in functionResults)
                {
                    result.Add(new Dictionary<string, object?>
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = fr.CallId,
                        ["content"] = fr.Result?.ToString() ?? ""
                    });
                }
            }
            else
            {
                result.Add(new Dictionary<string, object>
                {
                    ["role"] = msg.Role.Value,
                    ["content"] = msg.Text ?? ""
                });
            }
        }
        return result;
    }

    /// <summary>
    /// 将 ChatOptions.Tools 序列化为 OpenAI 兼容的 tools 数组
    /// </summary>
    public static List<object>? SerializeTools(ChatOptions? options)
    {
        if (options?.Tools == null || options.Tools.Count == 0)
            return null;

        return options.Tools.OfType<AIFunction>().Select(f => (object)new Dictionary<string, object?>
        {
            ["type"] = "function",
            ["function"] = new Dictionary<string, object?>
            {
                ["name"] = f.Name,
                ["description"] = f.Description ?? "",
                ["parameters"] = f.JsonSchema
            }
        }).ToList();
    }

    /// <summary>
    /// 构建请求 payload
    /// </summary>
    public static Dictionary<string, object?> BuildPayload(
        string model,
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        Dictionary<string, object?>? extra = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = SerializeMessages(messages)
        };

        if (options?.Temperature != null)
            payload["temperature"] = options.Temperature;
        if (options?.MaxOutputTokens != null)
            payload["max_tokens"] = options.MaxOutputTokens;

        var tools = SerializeTools(options);
        if (tools != null)
            payload["tools"] = tools;

        if (extra != null)
        {
            foreach (var (key, value) in extra)
                payload[key] = value;
        }

        return payload;
    }

    /// <summary>
    /// 解析 assistant 消息（可能包含 tool_calls）
    /// </summary>
    public static ChatMessage ParseAssistantMessage(string? content, List<OpenAIToolCall>? toolCalls)
    {
        if (toolCalls != null && toolCalls.Count > 0)
        {
            var contents = new List<AIContent>();

            if (!string.IsNullOrEmpty(content))
                contents.Add(new TextContent(content));

            foreach (var tc in toolCalls)
            {
                IDictionary<string, object?>? arguments = null;
                if (!string.IsNullOrEmpty(tc.Function?.Arguments))
                {
                    arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                        tc.Function.Arguments);
                }
                contents.Add(new FunctionCallContent(tc.Id, tc.Function?.Name ?? "", arguments));
            }

            return new ChatMessage(ChatRole.Assistant, contents);
        }

        return new ChatMessage(ChatRole.Assistant, content ?? "");
    }
}

/// <summary>
/// OpenAI 兼容的 tool_call 结构
/// </summary>
internal class OpenAIToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public OpenAIToolCallFunction Function { get; set; } = new();
}

/// <summary>
/// tool_call 中的 function 字段
/// </summary>
internal class OpenAIToolCallFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = "";
}

// =============================================================================
// SSE 流式解析 (共用)
// =============================================================================

internal static partial class OpenAISerializer
{
    /// <summary>
    /// 共用的 finish_reason 映射
    /// </summary>
    internal static ChatFinishReason? MapFinishReason(string? reason) => reason switch
    {
        "stop" => ChatFinishReason.Stop,
        "length" => ChatFinishReason.Length,
        "tool_calls" => ChatFinishReason.ToolCalls,
        "content_filter" => ChatFinishReason.ContentFilter,
        _ => null
    };

    /// <summary>
    /// 解析 OpenAI 兼容的 SSE 流，支持文本和 tool_call 增量
    /// </summary>
    internal static async IAsyncEnumerable<ChatResponseUpdate> ParseStreamAsync(
        Stream stream, string model,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream);
        var toolAccumulators = new Dictionary<int, ToolCallAccumulator>();

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..];
            if (data == "[DONE]") break;

            OpenAIStreamChunk? chunk;
            try { chunk = JsonSerializer.Deserialize<OpenAIStreamChunk>(data); }
            catch { continue; }

            var choice = chunk?.Choices?.FirstOrDefault();
            if (choice?.Delta == null) continue;

            // 文本增量
            if (!string.IsNullOrEmpty(choice.Delta.Content))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, choice.Delta.Content)
                {
                    ModelId = model
                };
            }

            // tool_call 增量 — 累积参数
            if (choice.Delta.ToolCalls != null)
            {
                foreach (var tc in choice.Delta.ToolCalls)
                {
                    if (!toolAccumulators.TryGetValue(tc.Index, out var acc))
                    {
                        acc = new ToolCallAccumulator();
                        toolAccumulators[tc.Index] = acc;
                    }
                    if (tc.Id != null) acc.Id = tc.Id;
                    if (tc.Function?.Name != null) acc.Name = tc.Function.Name;
                    if (tc.Function?.Arguments != null) acc.Arguments.Append(tc.Function.Arguments);
                }
            }

            // finish_reason 到达时输出完整的 tool_call
            if (choice.FinishReason != null && toolAccumulators.Count > 0)
            {
                foreach (var (_, acc) in toolAccumulators.OrderBy(kv => kv.Key))
                    yield return acc.ToChatResponseUpdate(model);
                toolAccumulators.Clear();
            }
        }

        // 流结束后输出剩余的 tool_call
        foreach (var (_, acc) in toolAccumulators.OrderBy(kv => kv.Key))
            yield return acc.ToChatResponseUpdate(model);
    }

    /// <summary>
    /// 工具调用累积器 — 收集流式 tool_call 片段
    /// </summary>
    private class ToolCallAccumulator
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public StringBuilder Arguments { get; } = new();

        public ChatResponseUpdate ToChatResponseUpdate(string model)
        {
            IDictionary<string, object?>? args = null;
            var argsJson = Arguments.ToString();
            if (!string.IsNullOrEmpty(argsJson))
            {
                try { args = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson); }
                catch { /* 无效 JSON, args 保持 null */ }
            }

            var update = new ChatResponseUpdate { Role = ChatRole.Assistant, ModelId = model };
            update.Contents.Add(new FunctionCallContent(Id, Name, args));
            return update;
        }
    }
}

// =============================================================================
// SSE 流 chunk 类型 (共用)
// =============================================================================

internal class OpenAIStreamChunk
{
    [JsonPropertyName("choices")]
    public List<OpenAIStreamChoice>? Choices { get; set; }
}

internal class OpenAIStreamChoice
{
    [JsonPropertyName("delta")]
    public OpenAIStreamDelta? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class OpenAIStreamDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<OpenAIStreamToolCall>? ToolCalls { get; set; }
}

internal class OpenAIStreamToolCall
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("function")]
    public OpenAIStreamToolCallFunction? Function { get; set; }
}

internal class OpenAIStreamToolCallFunction
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}
