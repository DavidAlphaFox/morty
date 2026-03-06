# 任务: 实现 CLI 命令

## 阶段
Phase 7: CLI 命令

## 描述
实现命令行入口和所有 CLI 命令。

## 验收标准
- [ ] 交互模式 `morty`
- [ ] 单次对话 `morty "prompt"`
- [ ] 打印模式 `morty -p "prompt"`
- [ ] 会话管理命令

## 实现步骤

### 17.1 创建 CLI 入口
创建 `src/cli/Program.cs`:
```csharp
using System.CommandLine;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("morty - AI Coding Assistant");
        
        // 添加子命令
        rootCommand.AddCommand(CreateAuthCommands());
        rootCommand.AddCommand(CreateSessionCommands());
        rootCommand.AddCommand(CreateModelCommands());
        
        // 添加选项
        var modelOption = new Option<string>("--model", "Model to use");
        var continueOption = new Option<bool>("-c", "Continue last session");
        var sessionOption = new Option<string>("--session", "Session ID to continue");
        
        rootCommand.AddOption(modelOption);
        rootCommand.AddOption(continueOption);
        rootCommand.AddOption(sessionOption);
        
        // 默认命令 - 交互模式或单次对话
        var promptArgument = new Argument<string?>("prompt", "Prompt to send");
        rootCommand.AddArgument(promptArgument);
        
        rootCommand.SetHandler(async (prompt, model, cont, session) =>
        {
            if (string.IsNullOrEmpty(prompt))
            {
                // 交互模式
                await RunInteractive(model, cont, session);
            }
            else
            {
                // 单次对话
                await RunOnce(prompt, model);
            }
        }, promptArgument, modelOption, continueOption, sessionOption);
        
        return await rootCommand.InvokeAsync(args);
    }
}
```

### 17.2 实现认证命令
创建 `src/cli/Commands/AuthCommands.cs`:
```csharp
public static class AuthCommands
{
    public static Command Create()
    {
        var cmd = new Command("auth", "Manage authentication");
        
        cmd.AddCommand(CreateLoginCommand());
        cmd.AddCommand(CreateListCommand());
        cmd.AddCommand(CreateLogoutCommand());
        
        return cmd;
    }
    
    private static Command CreateLoginCommand()
    {
        var cmd = new Command("login", "Login to a provider");
        var providerArg = new Argument<string>("provider", "Provider: zhipu, minimax, qianwen");
        cmd.AddArgument(providerArg);
        
        cmd.SetHandler(async (provider) =>
        {
            Console.Write($"API Key for {provider}: ");
            var key = Console.ReadLine();
            
            var auth = new AuthManager();
            await auth.LoginAsync(provider, key!);
            
            Console.WriteLine($"✓ Logged in to {provider}");
        }, providerArg);
        
        return cmd;
    }
    
    private static Command CreateListCommand()
    {
        var cmd = new Command("list", "List logged in providers");
        
        cmd.SetHandler(() =>
        {
            var auth = new AuthManager();
            var providers = auth.ListProviders().ToList();
            
            if (providers.Count == 0)
            {
                Console.WriteLine("No providers logged in");
                return;
            }
            
            Console.WriteLine("Logged in providers:");
            foreach (var p in providers)
            {
                Console.WriteLine($"  • {p}");
            }
        });
        
        return cmd;
    }
}
```

### 17.3 实现会话命令
创建 `src/cli/Commands/SessionCommands.cs`:
```csharp
public static class SessionCommands
{
    public static Command Create()
    {
        var cmd = new Command("session", "Manage sessions");
        
        cmd.AddCommand(CreateListCommand());
        cmd.AddCommand(CreateShowCommand());
        cmd.AddCommand(CreateExportCommand());
        
        return cmd;
    }
    
    private static Command CreateListCommand()
    {
        var cmd = new Command("list", "List all sessions");
        
        cmd.SetHandler(() =>
        {
            var sessionDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "morty", "sessions");
            
            var sessions = Directory.GetFiles(sessionDir, "*.meta.json");
            
            foreach (var s in sessions)
            {
                var json = File.ReadAllText(s);
                var session = JsonSerializer.Deserialize<Session>(json);
                Console.WriteLine($"{session.Id} - {session.WorkingDirectory}");
            }
        });
        
        return cmd;
    }
}
```

### 17.4 实现模型命令
创建 `src/cli/Commands/ModelCommands.cs`:
```csharp
public static class ModelCommands
{
    public static Command Create()
    {
        var cmd = new Command("models", "List available models");
        
        var providerArg = new Argument<string?>("provider", "Filter by provider");
        cmd.AddArgument(providerArg);
        
        cmd.SetHandler((provider) =>
        {
            var models = provider?.ToLower() switch
            {
                "zhipu" => new[] { "glm-4", "glm-4-flash", "glm-4-plus", "glm-4v-plus" },
                "minimax" => new[] { "MiniMax-M2", "MiniMax-M2.1" },
                "qianwen" => new[] { "qwen-turbo", "qwen-plus", "qwen-max", "qwen-long", "qwen2.5-vl" },
                _ => new[] { 
                    "glm-4", "glm-4-flash", "glm-4-plus",
                    "MiniMax-M2", "MiniMax-M2.1",
                    "qwen-turbo", "qwen-plus", "qwen-max"
                }
            };
            
            foreach (var m in models)
            {
                Console.WriteLine(m);
            }
        }, providerArg);
        
        return cmd;
    }
}
```

## CLI 命令列表

| 命令 | 说明 |
|------|------|
| `morty` | 交互模式 |
| `morty "prompt"` | 单次对话 |
| `morty -p "prompt"` | 打印模式 |
| `morty --model <name>` | 指定模型 |
| `morty -c` | 继续会话 |
| `morty --session <id>` | 指定会话 |
| `morty auth login <provider>` | 登录 |
| `morty auth list` | 列出已登录 |
| `morty auth logout <provider>` | 登出 |
| `morty session list` | 列出会话 |
| `morty session show <id>` | 显示会话 |
| `morty models` | 列出模型 |

## 相关文件
- tasks/05-auth-manager.md
- design/dotnet-coding-agent.md (6. CLI 命令设计)
