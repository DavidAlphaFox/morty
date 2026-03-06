# 任务: 实现配置加载器

## 阶段
Phase 2: 配置与凭证

## 描述
实现配置管理系统，支持从多个位置加载配置：~/.config/morty/morty.json、环境变量指定目录、项目根目录 .morty.json。

## 验收标准
- [ ] 支持多个配置位置
- [ ] 正确解析 JSON 配置
- [ ] 配置优先级正确
- [ ] 支持 JSON Schema 验证

## 实现步骤

### 4.1 创建配置选项类
创建 `src/config/ConfigOptions.cs`:
```csharp
public class MortyConfig
{
    public ProviderConfig? Provider { get; set; }
    public ToolsConfig? Tools { get; set; }
    public Dictionary<string, McpServerConfig>? Mcp { get; set; }
    public SessionConfig? Session { get; set; }
    public TuiConfig? Tui { get; set; }
}

public class ProviderConfig
{
    public string Type { get; set; } = "zhipu";
    public string Model { get; set; } = "glm-4-plus";
    public string? BaseUrl { get; set; }
}

public class ToolsConfig
{
    public List<string> Enabled { get; set; } = new();
    public BashConfig? Bash { get; set; }
}

public class BashConfig
{
    public List<string> AllowedCommands { get; set; } = new();
    public int Timeout { get; set; } = 300;
}

public class McpServerConfig
{
    public string Type { get; set; } = "stdio";
    public string? Command { get; set; }
    public List<string>? Args { get; set; }
    public string? Url { get; set; }
    public bool Enabled { get; set; } = true;
}

public class SessionConfig
{
    public string Dir { get; set; } = "~/.morty/sessions";
    public bool AutoCompact { get; set; } = true;
    public double CompactThreshold { get; set; } = 0.8;
}

public class TuiConfig
{
    public string Theme { get; set; } = "dark";
    public bool SyntaxHighlighting { get; set; } = true;
}
```

### 4.2 创建配置加载器
创建 `src/config/ConfigLoader.cs`:
```csharp
public class ConfigLoader
{
    private readonly string[] _configPaths;
    
    public ConfigLoader()
    {
        var configDir = Environment.GetEnvironmentVariable("MORTY_CONFIG_DIR") 
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationConfig), "morty");
        
        _configPaths = new[]
        {
            Path.Combine(configDir, "morty.json"),
            Path.Combine(Environment.CurrentDirectory, ".morty.json")
        };
    }
    
    public MortyConfig Load()
    {
        foreach (var path in _configPaths)
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<MortyConfig>(json) ?? new MortyConfig();
            }
        }
        
        return new MortyConfig();
    }
}
```

### 4.3 实现配置验证
创建 `src/config/ConfigValidator.cs`:
- 验证 provider.type 有效 (zhipu/minimax/qianwen)
- 验证 tools.enabled 包含有效工具名
- 验证 mcp 服务器配置完整

## 配置优先级
1. `~/.config/morty/morty.json`
2. `$MORTY_CONFIG_DIR/morty.json`
3. 项目目录 `.morty.json`

## 相关文件
- design/dotnet-coding-agent.md (10. 配置文件)
- tasks/01-create-project-structure.md
