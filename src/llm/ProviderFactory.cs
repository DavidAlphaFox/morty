// =============================================================================
// LLM Provider 工厂
// =============================================================================
// 根据提供商名称创建对应的 LLM Provider 实例
// =============================================================================

namespace Morty.LLM;

/// <summary>
/// LLM Provider 工厂
/// </summary>
public static class ProviderFactory
{
    /// <summary>
    /// 创建 LLM Provider
    /// </summary>
    /// <param name="providerName">提供商名称: zhipu, minimax, qianwen</param>
    /// <param name="apiKey">API Key</param>
    /// <param name="baseUrl">自定义 API 地址 (可选)</param>
    /// <returns>ILlmProvider 实例</returns>
    /// <exception cref="NotSupportedException">不支持的提供商</exception>
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
