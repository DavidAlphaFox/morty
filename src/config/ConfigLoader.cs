// =============================================================================
// Morty 配置加载器
// =============================================================================
// 多层级配置加载和合并:
//   1. 内置默认值
//   2. ~/.config/morty/morty.json (全局)
//   3. $MORTY_CONFIG_DIR/morty.json (环境变量)
//   4. .morty/config.json (项目)
// 支持 JSONC (带注释的 JSON)
// =============================================================================

using System.Text.Json;

namespace Morty.Config;

/// <summary>
/// 配置加载器
/// </summary>
public class ConfigLoader
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// 加载配置 (多层级合并)
    /// </summary>
    public MortyConfig Load()
    {
        var config = new MortyConfig();

        // 层级 1: 全局配置
        var globalConfig = LoadFile(GetGlobalConfigPath());
        if (globalConfig != null) MergeFrom(config, globalConfig);

        // 层级 2: 环境变量指定的配置
        var envDir = Environment.GetEnvironmentVariable("MORTY_CONFIG_DIR");
        if (envDir != null)
        {
            var envConfig = LoadFile(Path.Combine(envDir, "morty.json"));
            if (envConfig != null) MergeFrom(config, envConfig);
        }

        // 层级 3: 项目配置
        var projectConfig = LoadFile(GetProjectConfigPath());
        if (projectConfig != null) MergeFrom(config, projectConfig);

        return config;
    }

    /// <summary>
    /// 获取全局配置路径
    /// </summary>
    public static string GetGlobalConfigPath()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "morty");
        return Path.Combine(configDir, "morty.json");
    }

    /// <summary>
    /// 获取项目配置路径
    /// </summary>
    public static string GetProjectConfigPath()
    {
        return Path.Combine(Environment.CurrentDirectory, ".morty", "config.json");
    }

    /// <summary>
    /// 初始化 .morty/ 项目目录结构
    /// </summary>
    public static void InitProjectDir(string cwd)
    {
        var mortyDir = Path.Combine(cwd, ".morty");
        Directory.CreateDirectory(mortyDir);
        Directory.CreateDirectory(Path.Combine(mortyDir, "agents"));
        Directory.CreateDirectory(Path.Combine(mortyDir, "skills"));
        Directory.CreateDirectory(Path.Combine(mortyDir, "plugins"));

        var configPath = Path.Combine(mortyDir, "config.json");
        if (!File.Exists(configPath))
        {
            File.WriteAllText(configPath, """
                {
                  // Morty 项目配置
                  // 全局配置: ~/.local/share/morty/morty.json
                  "provider": {
                    "type": "zhipu",
                    "model": "glm-5"
                  }
                }
                """);
        }
    }

    /// <summary>
    /// 展开路径中的 ~ 和环境变量
    /// </summary>
    public static string ExpandPath(string path)
    {
        if (path.StartsWith("~/"))
        {
            var home = Environment.GetEnvironmentVariable("HOME")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }

        return Environment.ExpandEnvironmentVariables(path);
    }

    private MortyConfig? LoadFile(string path)
    {
        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<MortyConfig>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to load config from {path}: {ex.Message}");
            return null;
        }
    }

    private static void MergeFrom(MortyConfig target, MortyConfig source)
    {
        // Provider
        if (source.Provider != null)
        {
            target.Provider ??= new ProviderConfig();
            if (!string.IsNullOrEmpty(source.Provider.Type) && source.Provider.Type != "zhipu")
                target.Provider.Type = source.Provider.Type;
            if (!string.IsNullOrEmpty(source.Provider.Model) && source.Provider.Model != "glm-5")
                target.Provider.Model = source.Provider.Model;
            if (source.Provider.BaseUrl != null)
                target.Provider.BaseUrl = source.Provider.BaseUrl;
            if (source.Provider.Models != null)
                target.Provider.Models = source.Provider.Models;
            if (source.Provider.Headers != null)
                target.Provider.Headers = source.Provider.Headers;
        }

        // Tools
        if (source.Tools != null)
        {
            target.Tools ??= new ToolsConfig();
            if (source.Tools.Enabled.Count > 0)
                target.Tools.Enabled = source.Tools.Enabled;
            if (source.Tools.Bash != null)
                target.Tools.Bash = source.Tools.Bash;
        }

        // MCP
        if (source.Mcp != null)
        {
            target.Mcp ??= new Dictionary<string, McpServerConfig>();
            foreach (var (key, value) in source.Mcp)
                target.Mcp[key] = value;
        }

        // Session
        if (source.Session != null)
        {
            target.Session ??= new SessionConfig();
            target.Session.Dir = source.Session.Dir;
            target.Session.AutoCompact = source.Session.AutoCompact;
            target.Session.CompactThreshold = source.Session.CompactThreshold;
        }

        // Permission
        if (source.Permission != null)
            target.Permission = source.Permission;

        // TUI
        if (source.Tui != null)
            target.Tui = source.Tui;
    }
}
