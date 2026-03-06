// =============================================================================
// Morty 配置加载器
// =============================================================================
// 负责从多个位置加载配置文件，支持 JSON 格式
// 配置文件优先级:
//   1. ~/.config/morty/morty.json (用户默认)
//   2. $MORTY_CONFIG_DIR/morty.json (环境变量指定)
//   3. ./.morty/config.json (项目目录)
// =============================================================================

using System.Text.Json;

namespace Morty.Config;

/// <summary>
/// 配置加载器
/// </summary>
public class ConfigLoader
{
    /// <summary>
    /// 配置文件路径列表 (按优先级排序)
    /// </summary>
    private readonly string[] _configPaths;

    /// <summary>
    /// JSON 序列化选项
    /// </summary>
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// 初始化配置加载器
    /// </summary>
    public ConfigLoader()
    {
        // 获取配置目录: 环境变量 MORTY_CONFIG_DIR 或默认 ~/.config/morty
        var configDir = Environment.GetEnvironmentVariable("MORTY_CONFIG_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "morty");

        // 配置路径优先级
        _configPaths = new[]
        {
            // 用户配置: ~/.config/morty/morty.json
            Path.Combine(configDir, "morty.json"),
            // 项目配置: ./.morty/config.json
            Path.Combine(Environment.CurrentDirectory, ".morty")
        };

        // JSON 反序列化选项
        _jsonOptions = new JsonSerializerOptions
        {
            // 属性名不区分大小写
            PropertyNameCaseInsensitive = true,
            // 允许 JSON 中的注释
            ReadCommentHandling = JsonCommentHandling.Skip
        };
    }

    /// <summary>
    /// 加载配置 (按优先级读取第一个存在的配置文件)
    /// </summary>
    /// <returns>MortyConfig 实例</returns>
    public MortyConfig Load()
    {
        // 遍历配置路径，返回第一个存在的配置
        foreach (var path in _configPaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var config = JsonSerializer.Deserialize<MortyConfig>(json, _jsonOptions);
                    if (config != null)
                    {
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    // 输出加载错误但继续尝试下一个配置
                    Console.Error.WriteLine($"Warning: Failed to load config from {path}: {ex.Message}");
                }
            }
        }

        // 所有配置都不存在，返回默认配置
        return new MortyConfig();
    }

    /// <summary>
    /// 展开路径中的 ~ 为用户主目录
    /// </summary>
    /// <param name="path">原始路径</param>
    /// <returns>展开后的路径</returns>
    public static string ExpandPath(string path)
    {
        // 处理 ~ 路径
        if (path.StartsWith("~/"))
        {
            // 获取用户主目录
            var home = Environment.GetEnvironmentVariable("HOME") 
                ?? "/home/" + Environment.GetEnvironmentVariable("USER");
            return Path.Combine(home, path[2..]);
        }

        return path;
    }
}
