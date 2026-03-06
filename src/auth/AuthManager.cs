// =============================================================================
// 凭证管理器
// =============================================================================
// 提供登录、登出、查询等凭证管理功能
// =============================================================================

namespace Morty.Auth;

/// <summary>
/// 凭证管理器
/// </summary>
public class AuthManager
{
    /// <summary>
    /// 凭证存储
    /// </summary>
    private readonly AuthStore _store;

    /// <summary>
    /// 初始化凭证管理器
    /// </summary>
    public AuthManager()
    {
        _store = new AuthStore();
    }

    /// <summary>
    /// 登录提供商
    /// </summary>
    /// <param name="provider">提供商名称 (zhipu, minimax, qianwen)</param>
    /// <param name="apiKey">API Key</param>
    /// <returns>异步任务</returns>
    public Task LoginAsync(string provider, string apiKey)
    {
        // 加载现有凭证
        var credentials = _store.Load();
        
        // 添加或更新凭证
        credentials[provider.ToLower()] = new Credential { Type = "api", Key = apiKey };
        
        // 保存
        _store.Save(credentials);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取 API Key
    /// </summary>
    /// <param name="provider">提供商名称</param>
    /// <returns>API Key，如果不存在返回 null</returns>
    public string? GetApiKey(string provider)
    {
        var credentials = _store.Load();
        return credentials.TryGetValue(provider.ToLower(), out var cred) ? cred.Key : null;
    }

    /// <summary>
    /// 列出所有已登录的提供商
    /// </summary>
    /// <returns>提供商名称列表</returns>
    public IEnumerable<string> ListProviders()
    {
        return _store.Load().Keys;
    }

    /// <summary>
    /// 登出提供商
    /// </summary>
    /// <param name="provider">提供商名称</param>
    /// <returns>异步任务</returns>
    public Task LogoutAsync(string provider)
    {
        // 加载现有凭证
        var credentials = _store.Load();
        
        // 移除凭证
        credentials.Remove(provider.ToLower());
        
        // 保存
        _store.Save(credentials);
        return Task.CompletedTask;
    }
}
