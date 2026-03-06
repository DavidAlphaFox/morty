using System.Text;
using Morty.LLM;

namespace Morty.Agent;

public class ContextCompactor
{
    private const int DefaultMaxTokens = 128000;

    public async Task<List<ChatMessageContent>> CompressAsync(
        List<ChatMessageContent> history,
        int maxTokens,
        ILlmProvider provider,
        CancellationToken ct = default)
    {
        var currentTokens = EstimateTokens(history);

        if (currentTokens < maxTokens * 0.8)
            return history;

        var (keep, compress) = SplitMessages(history);

        if (compress.Count == 0)
            return keep;

        var summary = await SummarizeAsync(compress, provider, ct);

        keep.Add(new ChatMessageContent
        {
            Role = "system",
            Content = $"[对话摘要] {summary}"
        });

        return keep;
    }

    public int EstimateTokens(ChatMessageContent message)
    {
        var text = message.Content;
        var chineseChars = text.Count(c => IsChinese(c));
        var otherChars = text.Length - chineseChars;

        return chineseChars * 2 + (int)(otherChars * 1.3);
    }

    public int EstimateTokens(IEnumerable<ChatMessageContent> history)
    {
        return history.Sum(EstimateTokens);
    }

    private static bool IsChinese(char c)
    {
        return c >= 0x4E00 && c <= 0x9FFF;
    }

    private (List<ChatMessageContent> keep, List<ChatMessageContent> compress) SplitMessages(
        List<ChatMessageContent> history)
    {
        var keep = new List<ChatMessageContent>();
        var compress = new List<ChatMessageContent>();

        const int keepCount = 20;

        for (int i = 0; i < history.Count; i++)
        {
            if (i >= history.Count - keepCount)
                keep.Add(history[i]);
            else
                compress.Add(history[i]);
        }

        keep.AddRange(history.Where(m => m.Role == "tool"));

        return (keep, compress);
    }

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
