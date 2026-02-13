using System.Text.Json;
using Morty.Core.Interfaces;
using Serilog;

namespace Morty.Core.Providers;

/// <summary>
/// Claude 供应商工厂 - 管理多个供应商实例
/// </summary>
public class ClaudeProviderFactory : IClaudeProviderFactory
{
    private readonly Dictionary<string, IClaudeProvider> _providers = new();
    private readonly ILogger _logger;
    private string? _defaultProviderName;

    public ClaudeProviderFactory(ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;
    }

    /// <summary>
    /// 根据名称获取供应商
    /// </summary>
    public IClaudeProvider GetProvider(string name)
    {
        if (_providers.TryGetValue(name, out var provider))
        {
            return provider;
        }
        throw new ArgumentException($"未找到供应商 '{name}'。可用供应商: {string.Join(", ", _providers.Keys)}");
    }

    /// <summary>
    /// 根据类型获取供应商
    /// </summary>
    public IClaudeProvider GetProviderByType(string type)
    {
        var provider = _providers.Values.FirstOrDefault(p => p.Type == type);
        if (provider != null)
            return provider;
        throw new ArgumentException($"未找到类型为 '{type}' 的供应商。");
    }

    /// <summary>
    /// 获取默认供应商
    /// </summary>
    public IClaudeProvider? GetDefaultProvider()
    {
        if (string.IsNullOrEmpty(_defaultProviderName))
            return _providers.Values.FirstOrDefault();

        return _providers.TryGetValue(_defaultProviderName, out var provider) ? provider : null;
    }

    /// <summary>
    /// 根据计划类型获取供应商
    /// </summary>
    public IClaudeProvider? GetProviderForPlanType(PlanUsageType planType)
    {
        // 目前返回默认供应商
        // 未来可扩展为规划和执行使用不同供应商
        return GetDefaultProvider();
    }

    /// <summary>
    /// 注册供应商
    /// </summary>
    public void RegisterProvider(IClaudeProvider provider)
    {
        _providers[provider.Name] = provider;
        _logger.Information("已注册供应商: {Name} ({Type})", provider.Name, provider.Type);
    }

    /// <summary>
    /// 设置默认供应商
    /// </summary>
    public void SetDefaultProvider(string name)
    {
        if (!_providers.ContainsKey(name))
        {
            throw new ArgumentException($"无法设置默认供应商 '{name}' - 供应商未找到。");
        }
        _defaultProviderName = name;
        _logger.Information("默认供应商设置为: {Name}", name);
    }
}

/// <summary>
/// 供应商配置加载器 - 从环境变量加载配置
/// </summary>
public class ProviderConfigLoader
{
    private readonly ILogger _logger;

    public ProviderConfigLoader(ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;
    }

    /// <summary>
    /// 从环境变量加载供应商配置
    /// </summary>
    public List<ProviderConfig> LoadFromEnvironment()
    {
        var providersJson = Environment.GetEnvironmentVariable("MORTY_PROVIDERS");
        if (string.IsNullOrEmpty(providersJson))
        {
            _logger.Warning("未设置 MORTY_PROVIDERS 环境变量。使用 CLI 供应商作为默认。");
            return GetDefaultCliProvider();
        }

        try
        {
            // 替换环境变量引用，如 ${VAR_NAME}
            providersJson = ReplaceEnvVariables(providersJson);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var configs = JsonSerializer.Deserialize<List<ProviderConfig>>(providersJson, options);
            return configs ?? new List<ProviderConfig>();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "解析 MORTY_PROVIDERS 环境变量失败");
            return GetDefaultCliProvider();
        }
    }

    /// <summary>
    /// 根据配置创建供应商实例
    /// </summary>
    public IClaudeProvider CreateProvider(ProviderConfig config)
    {
        // 解析配置中的环境变量
        var apiUrl = ReplaceEnvVariables(config.ApiUrl);
        var model = ReplaceEnvVariables(config.Model);
        var token = ReplaceEnvVariables(config.Token);

        return config.Type.ToLower() switch
        {
            "anthropic" => new AnthropicProvider(
                config.Name,
                apiUrl,
                model,
                token,
                config.Config),

            "cli" => new ClaudeCliProvider(
                string.IsNullOrEmpty(config.Token) ? "claude" : config.Token,
                config.Config.GetValueOrDefault("args", "-p").ToString()),

            _ => throw new NotSupportedException($"不支持的供应商类型 '{config.Type}'")
        };
    }

    /// <summary>
    /// 替换字符串中的环境变量
    /// </summary>
    private string ReplaceEnvVariables(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // 匹配 ${VAR_NAME} 模式
        var pattern = @"\$\{([^}]+)\}";
        return System.Text.RegularExpressions.Regex.Replace(input, pattern, match =>
        {
            var varName = match.Groups[1].Value;
            return Environment.GetEnvironmentVariable(varName) ?? match.Value;
        });
    }

    /// <summary>
    /// 获取默认 CLI 供应商配置
    /// </summary>
    private List<ProviderConfig> GetDefaultCliProvider()
    {
        return new List<ProviderConfig>
        {
            new ProviderConfig
            {
                Name = "Claude CLI",
                Type = "cli",
                ApiUrl = "",
                Model = "",
                Token = "claude",
                IsDefault = true,
                Config = new Dictionary<string, object>()
            }
        };
    }
}

/// <summary>
/// 供应商配置
/// </summary>
public class ProviderConfig
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public Dictionary<string, object> Config { get; set; } = new();
}
