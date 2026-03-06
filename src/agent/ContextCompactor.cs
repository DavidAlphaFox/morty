// =============================================================================
// 上下文压缩器
// =============================================================================
// 两阶段压缩:
//   1. 裁剪旧工具输出 (保留最近 40K token 的工具结果)
//   2. LLM 摘要 (专用压缩 prompt，保留文件路径/代码变更/决策等)
// =============================================================================

using System.Text;
using Microsoft.Extensions.AI;
using Morty.LLM;

namespace Morty.Agent;

/// <summary>
/// 上下文压缩器
/// </summary>
public class ContextCompactor
{
    private const string CompactionSystemPrompt = """
        You are summarizing a coding conversation for context compression.
        Preserve ALL of the following:
        - File paths that were read, edited, or created
        - Code changes made (what was changed and why)
        - Decisions and rationale
        - Current task status and next steps
        - Error messages and their resolutions
        - User preferences and requirements mentioned

        Be concise but complete. Use bullet points. Do not lose any actionable information.
        """;

    /// <summary>
    /// 压缩上下文 — 两阶段策略
    /// </summary>
    public async Task<List<ChatMessageContent>> CompressAsync(
        List<ChatMessageContent> history,
        int maxTokens,
        IChatClient client,
        CancellationToken ct = default)
    {
        var currentTokens = EstimateTokens(history);

        if (currentTokens < maxTokens * 0.8)
            return history;

        // 阶段 1: 裁剪旧工具输出
        var pruned = PruneToolOutputs(history);
        currentTokens = EstimateTokens(pruned);
        if (currentTokens < maxTokens * 0.8)
            return pruned;

        // 阶段 2: LLM 摘要
        var (keep, compress) = SplitMessages(pruned);
        if (compress.Count == 0)
            return keep;

        var summary = await SummarizeAsync(compress, client, ct);

        // 摘要插入到开头
        var result = new List<ChatMessageContent>
        {
            new()
            {
                Role = "system",
                Content = $"[Conversation summary]\n{summary}"
            }
        };
        result.AddRange(keep);
        return result;
    }

    /// <summary>
    /// 裁剪旧的工具输出，保留最近的工具结果
    /// </summary>
    public List<ChatMessageContent> PruneToolOutputs(
        List<ChatMessageContent> messages, int keepRecentToolTokens = 40000)
    {
        // 从后向前扫描，标记哪些工具输出需要裁剪
        var toolTokensFromEnd = 0;
        var shouldPrune = new bool[messages.Count];

        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Role != "tool") continue;

            var tokens = EstimateTokens(messages[i]);
            toolTokensFromEnd += tokens;

            if (toolTokensFromEnd > keepRecentToolTokens)
                shouldPrune[i] = true;
        }

        var result = new List<ChatMessageContent>();
        for (var i = 0; i < messages.Count; i++)
        {
            if (shouldPrune[i])
            {
                result.Add(new ChatMessageContent
                {
                    Role = "tool",
                    Content = "[Output compacted]"
                });
            }
            else
            {
                result.Add(messages[i]);
            }
        }

        return result;
    }

    /// <summary>
    /// 估算单条消息的 Token 数量
    /// </summary>
    public int EstimateTokens(ChatMessageContent message)
    {
        var text = message.Content ?? "";
        return EstimateTokensFromText(text);
    }

    /// <summary>
    /// 估算消息列表的 Token 总数
    /// </summary>
    public int EstimateTokens(IEnumerable<ChatMessageContent> history)
    {
        return history.Sum(m => EstimateTokens(m) + 4); // +4 for message format overhead
    }

    /// <summary>
    /// 从文本估算 token 数
    /// </summary>
    private static int EstimateTokensFromText(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        // 中文字符 ≈ 2 tokens, 英文/代码 ≈ 1.3 tokens per char
        var chineseChars = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
        var otherChars = text.Length - chineseChars;
        return chineseChars * 2 + (int)(otherChars / 3.5); // ~3.5 chars per token for English
    }

    /// <summary>
    /// 分离消息 — 保留最近 N 条，其余可压缩
    /// </summary>
    private static (List<ChatMessageContent> keep, List<ChatMessageContent> compress) SplitMessages(
        List<ChatMessageContent> history)
    {
        var keep = new List<ChatMessageContent>();
        var compress = new List<ChatMessageContent>();

        const int keepCount = 20;

        for (var i = 0; i < history.Count; i++)
        {
            if (i >= history.Count - keepCount)
                keep.Add(history[i]);
            else
                compress.Add(history[i]);
        }

        return (keep, compress);
    }

    /// <summary>
    /// 使用 LLM 总结消息
    /// </summary>
    private static async Task<string> SummarizeAsync(
        List<ChatMessageContent> messages,
        IChatClient client,
        CancellationToken ct)
    {
        var prompt = BuildCompressionPrompt(messages);

        var chatMessages = new List<ChatMessage>
        {
            new(ChatRole.System, CompactionSystemPrompt),
            new(ChatRole.User, prompt)
        };

        var options = new ChatOptions { MaxOutputTokens = 2000 };
        var response = await client.GetResponseAsync(chatMessages, options, ct);
        return response.Text ?? "";
    }

    /// <summary>
    /// 构建压缩提示
    /// </summary>
    private static string BuildCompressionPrompt(List<ChatMessageContent> messages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Summarize the following conversation history:");
        sb.AppendLine();

        foreach (var msg in messages)
        {
            var role = msg.Role switch
            {
                "user" => "User",
                "assistant" => "Assistant",
                "tool" => "Tool",
                "system" => "System",
                _ => msg.Role
            };

            var content = msg.Content ?? "";
            // 截断过长的单条消息
            if (content.Length > 2000)
                content = content[..2000] + "\n[truncated]";

            sb.AppendLine($"[{role}]: {content}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
