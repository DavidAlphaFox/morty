using System.Text.Json.Serialization;

namespace Morty.Config;

public class MortyConfig
{
    [JsonPropertyName("provider")]
    public ProviderConfig? Provider { get; set; }

    [JsonPropertyName("tools")]
    public ToolsConfig? Tools { get; set; }

    [JsonPropertyName("mcp")]
    public Dictionary<string, McpServerConfig>? Mcp { get; set; }

    [JsonPropertyName("session")]
    public SessionConfig? Session { get; set; }

    [JsonPropertyName("tui")]
    public TuiConfig? Tui { get; set; }
}

public class ProviderConfig
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "zhipu";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "glm-5";

    [JsonPropertyName("baseUrl")]
    public string? BaseUrl { get; set; }

    [JsonPropertyName("models")]
    public List<string>? Models { get; set; }
}

public class ToolsConfig
{
    [JsonPropertyName("enabled")]
    public List<string> Enabled { get; set; } = new()
    {
        "read", "write", "edit", "bash", "grep", "find", "ls"
    };

    [JsonPropertyName("bash")]
    public BashConfig? Bash { get; set; }
}

public class BashConfig
{
    [JsonPropertyName("allowedCommands")]
    public List<string> AllowedCommands { get; set; } = new();

    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 300;
}

public class McpServerConfig
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "stdio";

    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("args")]
    public List<string>? Args { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

public class SessionConfig
{
    [JsonPropertyName("dir")]
    public string Dir { get; set; } = "~/.morty/sessions";

    [JsonPropertyName("autoCompact")]
    public bool AutoCompact { get; set; } = true;

    [JsonPropertyName("compactThreshold")]
    public double CompactThreshold { get; set; } = 0.8;
}

public class TuiConfig
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "dark";

    [JsonPropertyName("syntaxHighlighting")]
    public bool SyntaxHighlighting { get; set; } = true;
}
