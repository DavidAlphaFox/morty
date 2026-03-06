// =============================================================================
// Morty 配置文件模型
// =============================================================================
// 配置文件格式: JSON
// 配置位置:
//   - ~/.config/morty/morty.json (用户默认)
//   - $MORTY_CONFIG_DIR/morty.json (环境变量指定)
//   - ./.morty/config.json (项目目录)
// =============================================================================

using System.Text.Json.Serialization;

namespace Morty.Config;

/// <summary>
/// Morty 主配置类
/// </summary>
public class MortyConfig
{
    /// <summary>
    /// LLM 提供商配置
    /// </summary>
    [JsonPropertyName("provider")]
    public ProviderConfig? Provider { get; set; }

    /// <summary>
    /// 工具配置
    /// </summary>
    [JsonPropertyName("tools")]
    public ToolsConfig? Tools { get; set; }

    /// <summary>
    /// MCP 服务器配置
    /// </summary>
    [JsonPropertyName("mcp")]
    public Dictionary<string, McpServerConfig>? Mcp { get; set; }

    /// <summary>
    /// 会话配置
    /// </summary>
    [JsonPropertyName("session")]
    public SessionConfig? Session { get; set; }

    /// <summary>
    /// TUI 配置
    /// </summary>
    [JsonPropertyName("tui")]
    public TuiConfig? Tui { get; set; }

    /// <summary>
    /// 权限配置
    /// </summary>
    [JsonPropertyName("permission")]
    public PermissionConfig? Permission { get; set; }
}

/// <summary>
/// LLM 提供商配置
/// </summary>
public class ProviderConfig
{
    /// <summary>
    /// 提供商类型: zhipu, minimax, qianwen
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "zhipu";

    /// <summary>
    /// 默认模型名称
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "glm-5";

    /// <summary>
    /// 自定义 API 地址 (可选)
    /// </summary>
    [JsonPropertyName("baseUrl")]
    public string? BaseUrl { get; set; }

    /// <summary>
    /// 可用模型列表 (可选)
    /// </summary>
    [JsonPropertyName("models")]
    public List<string>? Models { get; set; }

    /// <summary>
    /// 自定义 HTTP 头 (可选)
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }
}

/// <summary>
/// 工具配置
/// </summary>
public class ToolsConfig
{
    /// <summary>
    /// 启用的工具列表
    /// </summary>
    [JsonPropertyName("enabled")]
    public List<string> Enabled { get; set; } = new()
    {
        "read", "write", "edit", "apply_patch", "bash", "grep", "glob", "ls",
        "todoread", "todowrite", "question"
    };

    /// <summary>
    /// Bash 工具配置
    /// </summary>
    [JsonPropertyName("bash")]
    public BashConfig? Bash { get; set; }
}

/// <summary>
/// Bash 工具配置
/// </summary>
public class BashConfig
{
    /// <summary>
    /// 允许执行的命令白名单
    /// </summary>
    [JsonPropertyName("allowedCommands")]
    public List<string> AllowedCommands { get; set; } = new();

    /// <summary>
    /// 命令超时时间 (秒)
    /// </summary>
    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 300;
}

/// <summary>
/// MCP 服务器配置
/// </summary>
public class McpServerConfig
{
    /// <summary>
    /// 连接类型: stdio, sse
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "stdio";

    /// <summary>
    /// 执行命令
    /// </summary>
    [JsonPropertyName("command")]
    public string? Command { get; set; }

    /// <summary>
    /// 命令参数
    /// </summary>
    [JsonPropertyName("args")]
    public List<string>? Args { get; set; }

    /// <summary>
    /// SSE 服务器 URL
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 会话配置
/// </summary>
public class SessionConfig
{
    /// <summary>
    /// 会话存储目录
    /// </summary>
    [JsonPropertyName("dir")]
    public string Dir { get; set; } = "~/.morty/sessions";

    /// <summary>
    /// 是否自动压缩上下文
    /// </summary>
    [JsonPropertyName("autoCompact")]
    public bool AutoCompact { get; set; } = true;

    /// <summary>
    /// 上下文压缩阈值 (0-1)
    /// </summary>
    [JsonPropertyName("compactThreshold")]
    public double CompactThreshold { get; set; } = 0.8;
}

/// <summary>
/// 权限配置
/// </summary>
public class PermissionConfig
{
    /// <summary>
    /// 权限规则列表
    /// </summary>
    [JsonPropertyName("rules")]
    public List<PermissionRuleConfig>? Rules { get; set; }

    /// <summary>
    /// 自动允许的权限类型 (如 ["read", "bash:low"])
    /// </summary>
    [JsonPropertyName("autoAllow")]
    public List<string>? AutoAllow { get; set; }
}

/// <summary>
/// 权限规则配置
/// </summary>
public class PermissionRuleConfig
{
    /// <summary>
    /// 权限类型: read, edit, bash
    /// </summary>
    [JsonPropertyName("permission")]
    public string Permission { get; set; } = "";

    /// <summary>
    /// 匹配模式: glob 路径或命令前缀
    /// </summary>
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "*";

    /// <summary>
    /// 动作: allow, deny, ask
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = "ask";
}

/// <summary>
/// TUI 配置
/// </summary>
public class TuiConfig
{
    /// <summary>
    /// 主题: dark, light
    /// </summary>
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "dark";

    /// <summary>
    /// 是否启用语法高亮
    /// </summary>
    [JsonPropertyName("syntaxHighlighting")]
    public bool SyntaxHighlighting { get; set; } = true;
}
