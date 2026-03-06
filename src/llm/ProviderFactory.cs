// =============================================================================
// LLM Provider 工厂
// =============================================================================
// 根据提供商名称创建对应的 IChatClient 实例
// =============================================================================

using Microsoft.Extensions.AI;

namespace Morty.LLM;

/// <summary>
/// LLM Provider 工厂
/// </summary>
public static class ProviderFactory
{
    /// <summary>
    /// 创建 IChatClient
    /// </summary>
    /// <param name="providerName">提供商名称: zhipu, minimax, qianwen</param>
    /// <param name="apiKey">API Key</param>
    /// <param name="baseUrl">自定义 API 地址 (可选)</param>
    /// <returns>IChatClient 实例</returns>
    /// <exception cref="NotSupportedException">不支持的提供商</exception>
    public static IChatClient Create(string providerName, string apiKey, string? baseUrl = null,
        Dictionary<string, string>? headers = null)
    {
        return providerName.ToLower() switch
        {
            "zhipu" => new ZhipuProvider(apiKey, baseUrl),
            "minimax" => new MiniMaxProvider(apiKey, baseUrl),
            "qianwen" or "qwen" => new QianwenProvider(apiKey, baseUrl),
            "openai" => new OpenAICompatProvider(apiKey,
                baseUrl ?? "https://api.openai.com/v1", "gpt-4o", headers),
            "deepseek" => new OpenAICompatProvider(apiKey,
                baseUrl ?? "https://api.deepseek.com/v1", "deepseek-chat", headers),
            "ollama" => new OpenAICompatProvider("",
                baseUrl ?? "http://localhost:11434/v1", "llama3", headers),
            "openai-compat" => new OpenAICompatProvider(apiKey,
                baseUrl ?? throw new ArgumentException("baseUrl is required for openai-compat"),
                "default", headers),
            _ => throw new NotSupportedException($"不支持的 LLM 提供商: {providerName}. 支持: zhipu, minimax, qianwen, openai, deepseek, ollama, openai-compat")
        };
    }
}
