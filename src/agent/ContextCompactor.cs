// =============================================================================
// 上下文压缩器
// =============================================================================
// 当上下文接近 token 限制时，使用 LLM 总结旧消息
// 保留关键信息如文件名、修改内容、决策等
// =============================================================================

using System.Text;
using Morty.LLM;

namespace Morty.Agent;

/// <summary>
/// 上下文压缩器
/// </summary>
public class ContextCompactor
{
    /// <summary>
    /// 默认最大 Token 数
    /// </summary>
    private const int DefaultMaxTokens = 128000;

    /// <summary>
    /// 压缩上下文
    /// </summary>
    /// <param name="history">消息历史</param>
    /// <param name="maxTokens">最大 Token 数</param>
    /// <param name="provider">LLM Provider</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>压缩后的消息列表</returns>
    public async Task<List<ChatMessageContent>> CompressAsync(
        List<ChatMessageContent> history,
        int maxTokens,
        ILlmProvider provider,
        CancellationToken ct = default)
    {
        // 估算当前 Token 数
        var currentTokens = EstimateTokens(history);

        // 未超过阈值 (80%)，直接返回
        if (currentTokens < maxTokens * 0.8)
            return history;

        // 分离消息: 保留最近的和可压缩的
        var (keep, compress) = SplitMessages(history);

        // 没有可压缩的消息
        if (compress.Count == 0)
            return keep;

        // 使用 LLM 总结
        var summary = await SummarizeAsync(compress, provider, ct);

        // 添加摘要消息
        keep.Add(new ChatMessageContent
        {
            Role = "system",
            Content = $"[对话摘要] {summary}"
        });

        return keep;
    }

    /// <summary>
    /// 估算 Token 数量
    /// </summary>
    /// <param name="message">消息</param>
    /// <returns>估算的 Token 数</returns>
    public int EstimateTokens(ChatMessageContent message)
    {
        var text = message.Content;
        
        // 简单估算: 中文字符 ≈ 2 tokens, 英文 ≈ 1.3 tokens
        var chineseChars = text.Count(c => IsChinese(c));
        var otherChars = text.Length - chineseChars;

        return chineseChars * 2 + (int)(otherChars * 1.3);
    }

    /// <summary>
    /// 估算消息列表的 Token 总数
    /// </summary>
    public int EstimateTokens(IEnumerable<ChatMessageContent> history)
    {
        return history.Sum(EstimateTokens);
    }

    /// <summary>
    /// 判断是否为中文字符
    /// </summary>
    private static bool IsChinese(char c)
    {
        return c >= 0x4E00 && c <= 0x9FFF;
    }

    /// <summary>
    /// 分离消息
    /// </summary>
    /// <param name="history">完整历史</param>
    /// <returns>(保留的消息, 可压缩的消息)</returns>
    private (List<ChatMessageContent> keep, List<ChatMessageContent> compress) SplitMessages(
        List<ChatMessageContent> history)
    {
        var keep = new List<ChatMessageContent>();
        var compress = new List<ChatMessageContent>();

        // 保留最近 20 条消息
        const int keepCount = 20;

        for (int i = 0; i < history.Count; i++)
        {
            if (i >= history.Count - keepCount)
                keep.Add(history[i]);
            else
                compress.Add(history[i]);
        }

        // 工具结果必须保留
        keep.AddRange(history.Where(m => m.Role == "tool"));

        return (keep, compress);
    }

    /// <summary>
    /// 使用 LLM 总结消息
    /// </summary>
    private async Task<string> SummarizeAsync(
        List<ChatMessageContent> messages,
        ILlmProvider provider,
        CancellationToken ct)
    {
        var prompt = BuildCompressionPrompt(messages);

        var request = new ChatRequest
        {
            Model = provider.SupportedModels.First(),
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = "你是一个专业的代码助手。请简洁总结以下对话，保留关键信息如文件名、修改内容、决策等。" },
                new() { Role = "user", Content = prompt }
            },
            MaxTokens = 2000
        };

        var response = await provider.ChatAsync(request, ct);
        return response.Content;
    }

    /// <summary>
    /// 构建压缩提示
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <returns>提示文本</returns>
    public string BuildCompressionPrompt(List<ChatMessageContent> messages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("请简洁总结以下对话历史：");
        sb.AppendLine();

        foreach (var msg in messages)
        {
            var role = msg.Role switch
            {
                "user" => "用户",
                "assistant" => "助手",
                "tool" => "工具",
                _ => msg.Role
            };

            sb.AppendLine($"[{role}]: {msg.Content}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
