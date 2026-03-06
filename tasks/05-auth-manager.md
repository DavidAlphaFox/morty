# 任务: 实现凭证管理

## 阶段
Phase 2: 配置与凭证

## 描述
实现凭证管理系统，参考 opencode 的方式：API Key 存储在 ~/.local/share/morty/auth.json，不在配置文件中明文存储。

## 验收标准
- [ ] 凭证文件存储在正确位置
- [ ] 支持登录、登出、列出提供商
- [ ] 凭证文件权限安全 (600)
- [ ] 交互式输入 API Key

## 实现步骤

### 5.1 创建凭证存储类
创建 `src/auth/AuthStore.cs`:
```csharp
public class AuthStore
{
    private readonly string _authFilePath;
    
    public AuthStore()
    {
        var shareDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "morty");
        Directory.CreateDirectory(shareDir);
        _authFilePath = Path.Combine(shareDir, "auth.json");
    }
    
    public Dictionary<string, Credential> Load()
    {
        if (!File.Exists(_authFilePath))
            return new Dictionary<string, Credential>();
        
        var json = File.ReadAllText(_authFilePath);
        return JsonSerializer.Deserialize<Dictionary<string, Credential>>(json) 
            ?? new Dictionary<string, Credential>();
    }
    
    public void Save(Dictionary<string, Credential> credentials)
    {
        var json = JsonSerializer.Serialize(credentials, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        File.WriteAllText(_authFilePath, json);
        
        // 设置安全权限 (Linux)
        if (OperatingSystem.IsLinux())
        {
            chmod(_authFilePath, 0x600); // owner read/write only
        }
    }
}

public class Credential
{
    public string Type { get; set; } = "api";
    public string Key { get; set; } = "";
}
```

### 5.2 创建凭证管理器
创建 `src/auth/AuthManager.cs`:
```csharp
public class AuthManager
{
    private readonly AuthStore _store;
    
    public AuthManager()
    {
        _store = new AuthStore();
    }
    
    public async Task LoginAsync(string provider, string apiKey)
    {
        var credentials = _store.Load();
        credentials[provider] = new Credential { Type = "api", Key = apiKey };
        _store.Save(credentials);
    }
    
    public string? GetApiKey(string provider)
    {
        var credentials = _store.Load();
        return credentials.TryGetValue(provider, out var cred) ? cred.Key : null;
    }
    
    public IEnumerable<string> ListProviders()
    {
        return _store.Load().Keys;
    }
    
    public Task LogoutAsync(string provider)
    {
        var credentials = _store.Load();
        credentials.Remove(provider);
        _store.Save(credentials);
    }
}
```

### 5.3 创建凭证命令
创建 `src/auth/AuthCommands.cs`:
```csharp
public static class AuthCommands
{
    public static void Register(CommandLineBuilder builder)
    {
        var authCmd = new Command("auth", "Manage credentials");
        
        authCmd.AddCommand(CreateLoginCommand());
        authCmd.AddCommand(CreateListCommand());
        authCmd.AddCommand(CreateLogoutCommand());
        
        builder.Command.AddCommand(authCmd);
    }
    
    private static Command CreateLoginCommand()
    {
        var cmd = new Command("login", "Login to a provider");
        cmd.AddArgument(new Argument<string>("provider", "Provider name: zhipu, minimax, qianwen"));
        
        cmd.SetHandler(async (provider) =>
        {
            Console.Write($"Enter API key for {provider}: ");
            var key = Console.ReadLine();
            
            var manager = new AuthManager();
            await manager.LoginAsync(provider, key);
            Console.WriteLine($"Logged in to {provider}");
        });
        
        return cmd;
    }
}
```

## 凭证文件位置
- `~/.local/share/morty/auth.json`

## 凭证文件格式
```json
{
  "zhipu": { "type": "api", "key": "xxx" },
  "minimax": { "type": "api", "key": "xxx" },
  "qianwen": { "type": "api", "key": "xxx" }
}
```

## 相关文件
- tasks/04-config-loader.md
- design/dotnet-coding-agent.md (11. 凭证管理系统)
