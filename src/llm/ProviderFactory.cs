namespace Morty.LLM;

public static class ProviderFactory
{
    public static ILlmProvider Create(string providerName, string apiKey, string? baseUrl = null)
    {
        return providerName.ToLower() switch
        {
            "zhipu" => new ZhipuProvider(apiKey, baseUrl),
            "minimax" => new MiniMaxProvider(apiKey, baseUrl),
            "qianwen" or "qwen" => new QianwenProvider(apiKey, baseUrl),
            _ => throw new NotSupportedException($"不支持的 LLM 提供商: {providerName}")
        };
    }
}
