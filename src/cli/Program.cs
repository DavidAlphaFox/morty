// =============================================================================
// Morty CLI 入口
// =============================================================================
// 使用 System.CommandLine 2.0.3 解析命令行参数
//
// 命令结构:
//   morty [prompt]                     交互模式 / 单次对话
//     -p, --provider <provider>        覆盖 LLM 提供商
//     -m, --model <model>              覆盖模型
//     -r, --resume                     继续上次会话
//   morty auth login <provider>        登录
//   morty auth list                    列出已登录
//   morty auth logout <provider>       登出
//   morty models [provider]            列出可用模型
//   morty session                      会话管理
// =============================================================================

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Extensions.AI;
using Morty.Agent;
using Morty.Auth;
using Morty.Config;
using Morty.LLM;

namespace Morty.CLI;

class Program
{
    private static AuthManager? _authManager;
    private static ConfigLoader? _configLoader;

    static async Task<int> Main(string[] args)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "morty", "logs");
        Directory.CreateDirectory(logDir);

        var rootCommand = BuildCommands();
        var parseResult = CommandLineParser.Parse(rootCommand, args);
        return await parseResult.InvokeAsync();
    }

    // ================================================================
    // 命令定义
    // ================================================================

    static RootCommand BuildCommands()
    {
        // 根命令参数和选项
        var promptArg = new Argument<string?>("prompt")
        {
            Description = "Prompt text for single-shot mode. Omit to enter interactive mode.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var providerOption = new Option<string?>("--provider", "-p")
        {
            Description = "Override LLM provider"
        };

        var modelOption = new Option<string?>("--model", "-m")
        {
            Description = "Override model"
        };

        var resumeOption = new Option<bool>("--resume", "-r")
        {
            Description = "Continue last session"
        };

        var planOption = new Option<bool>("--plan")
        {
            Description = "Start in plan (read-only) mode"
        };

        var rootCommand = new RootCommand("morty - AI Coding Assistant");
        rootCommand.Add(promptArg);
        rootCommand.Add(providerOption);
        rootCommand.Add(modelOption);
        rootCommand.Add(resumeOption);
        rootCommand.Add(planOption);

        rootCommand.SetAction(async (parseResult, ct) =>
        {
            var prompt = parseResult.GetValue(promptArg);
            var provider = parseResult.GetValue(providerOption);
            var model = parseResult.GetValue(modelOption);
            var resume = parseResult.GetValue(resumeOption);
            var plan = parseResult.GetValue(planOption);
            var mode = plan ? AgentMode.Plan : AgentMode.Build;

            return prompt != null
                ? await RunPromptAsync(prompt, provider, model, mode)
                : await RunInteractiveAsync(provider, model, resume, mode);
        });

        // 子命令
        rootCommand.Add(BuildAuthCommand());
        rootCommand.Add(BuildModelsCommand());
        rootCommand.Add(BuildSessionCommand());
        rootCommand.Add(BuildInitCommand());
        rootCommand.Add(BuildConfigCommand());

        return rootCommand;
    }

    static Command BuildAuthCommand()
    {
        var authCommand = new Command("auth", "Manage LLM provider authentication");

        // auth login <provider>
        var loginProviderArg = new Argument<string>("provider") { Description = "Provider name (zhipu, minimax, qianwen)" };
        var loginCommand = new Command("login", "Login to a provider");
        loginCommand.Add(loginProviderArg);
        loginCommand.SetAction((parseResult) =>
        {
            var provider = parseResult.GetValue(loginProviderArg);
            _authManager ??= new AuthManager();
            Console.Write($"API Key for {provider}: ");
            var key = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(key))
            {
                Console.Error.WriteLine("Error: API key is required");
                return 1;
            }

            _authManager.LoginAsync(provider, key).Wait();
            Console.WriteLine($"Logged in to {provider}");
            return 0;
        });

        // auth list
        var listCommand = new Command("list", "List logged in providers");
        listCommand.SetAction((_) =>
        {
            _authManager ??= new AuthManager();
            var providers = _authManager.ListProviders().ToList();

            if (providers.Count == 0)
            {
                Console.WriteLine("No providers logged in");
                return;
            }

            Console.WriteLine("Logged in providers:");
            foreach (var p in providers)
                Console.WriteLine($"  - {p}");
        });

        // auth logout <provider>
        var logoutProviderArg = new Argument<string>("provider") { Description = "Provider name" };
        var logoutCommand = new Command("logout", "Logout from a provider");
        logoutCommand.Add(logoutProviderArg);
        logoutCommand.SetAction((parseResult) =>
        {
            var provider = parseResult.GetValue(logoutProviderArg);
            _authManager ??= new AuthManager();
            _authManager.LogoutAsync(provider).Wait();
            Console.WriteLine($"Logged out from {provider}");
        });

        authCommand.Add(loginCommand);
        authCommand.Add(listCommand);
        authCommand.Add(logoutCommand);
        return authCommand;
    }

    static Command BuildModelsCommand()
    {
        var providerArg = new Argument<string?>("provider")
        {
            Description = "Filter by provider",
            Arity = ArgumentArity.ZeroOrOne
        };
        var modelsCommand = new Command("models", "List available models");
        modelsCommand.Add(providerArg);

        modelsCommand.SetAction((parseResult) =>
        {
            var provider = parseResult.GetValue(providerArg);
            _configLoader ??= new ConfigLoader();
            var config = _configLoader.Load();

            IEnumerable<string> models;

            if (!string.IsNullOrEmpty(provider) && config.Provider?.Models?.Count > 0)
            {
                models = config.Provider.Models;
            }
            else
            {
                var providerType = provider ?? config.Provider?.Type ?? "zhipu";
                models = providerType.ToLower() switch
                {
                    "zhipu" => new[] { "glm-5", "glm-4.7", "glm-4.5-air", "glm-4", "glm-4-flash", "glm-4-plus", "glm-4v-plus" },
                    "minimax" => new[] { "MiniMax-M2", "MiniMax-M2.1" },
                    "qianwen" => new[] { "qwen-turbo", "qwen-plus", "qwen-max", "qwen-long", "qwen2.5-vl" },
                    _ => new[]
                    {
                        "glm-5", "glm-4.7", "glm-4.5-air",
                        "MiniMax-M2", "MiniMax-M2.1",
                        "qwen-turbo", "qwen-plus", "qwen-max"
                    }
                };
            }

            foreach (var m in models)
                Console.WriteLine(m);
        });

        return modelsCommand;
    }

    static Command BuildInitCommand()
    {
        var initCommand = new Command("init", "Initialize .morty/ project directory");
        initCommand.SetAction((_) =>
        {
            ConfigLoader.InitProjectDir(Environment.CurrentDirectory);
            Console.WriteLine("Initialized .morty/ project directory");
            Console.WriteLine("  .morty/config.json  — project configuration");
            Console.WriteLine("  .morty/agents/      — custom agent definitions");
            Console.WriteLine("  .morty/skills/      — skill templates");
            Console.WriteLine("  .morty/plugins/     — plugin DLLs");
        });
        return initCommand;
    }

    static Command BuildConfigCommand()
    {
        var configCommand = new Command("config", "Configuration management");

        var showCommand = new Command("show", "Show merged configuration");
        showCommand.SetAction((_) =>
        {
            _configLoader ??= new ConfigLoader();
            var config = _configLoader.Load();
            var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            Console.WriteLine(json);
        });

        configCommand.Add(showCommand);
        return configCommand;
    }

    static Command BuildSessionCommand()
    {
        var sessionCommand = new Command("session", "Manage sessions");
        sessionCommand.SetAction((_) =>
        {
            Console.WriteLine("Session management not yet implemented");
        });
        return sessionCommand;
    }

    // ================================================================
    // Agent 创建
    // ================================================================

    private static CodingAgent? CreateAgent(
        MortyConfig config,
        string? providerOverride,
        string? modelOverride,
        bool resume,
        AgentMode mode,
        out string? error)
    {
        error = null;

        _authManager ??= new AuthManager();
        var providerType = providerOverride ?? config.Provider?.Type ?? "zhipu";
        var apiKey = _authManager.GetApiKey(providerType);

        if (string.IsNullOrEmpty(apiKey))
        {
            error = $"Not logged in to {providerType}. Run: morty auth login {providerType}";
            return null;
        }

        var model = modelOverride ?? config.Provider?.Model;
        var client = ProviderFactory.Create(providerType, apiKey, config.Provider?.BaseUrl, config.Provider?.Headers);

        var cwd = Environment.CurrentDirectory;
        var sessionDir = config.Session?.Dir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "morty", "sessions");
        sessionDir = ConfigLoader.ExpandPath(sessionDir);

        var sessionManager = resume
            ? SessionManager.ContinueRecent(cwd, sessionDir)
            : SessionManager.Create(cwd, sessionDir);

        var agent = new CodingAgent(client, sessionManager);

        // 权限系统 (plan 模式使用限制性规则)
        var permissionRules = mode == AgentMode.Plan
            ? AgentModeConfig.GetPermissions(AgentMode.Plan)
            : BuildPermissionRules(config.Permission);
        var permissionChecker = new PermissionChecker(permissionRules, AskUserPermission);
        agent.PermissionChecker = permissionChecker;

        // 工具集 (plan 模式只有只读工具)
        var toolsConfig = config.Tools ?? new ToolsConfig();
        if (mode == AgentMode.Plan)
            toolsConfig = new ToolsConfig { Enabled = AgentModeConfig.GetTools(AgentMode.Plan), Bash = config.Tools?.Bash };

        var tools = ToolRegistry.CreateAllTools(cwd, toolsConfig, permissionChecker);
        foreach (var tool in tools)
            agent.RegisterTool(tool);

        // 子 Agent 支持 (仅 build 模式)
        if (mode == AgentMode.Build)
        {
            var agentContext = new AgentContext(
                client, cwd, sessionDir, config.Tools, permissionChecker);
            AgentContext.RegisterTaskTool(agent, agentContext);
        }

        var contextFiles = SystemPromptBuilder.LoadContextFiles(cwd);
        agent.SystemPrompt = SystemPromptBuilder.Build(new SystemPromptOptions
        {
            Cwd = cwd,
            SelectedTools = toolsConfig.Enabled,
            ContextFiles = contextFiles
        }) + AgentModeConfig.GetSystemPromptSuffix(mode);

        if (config.Session?.AutoCompact != false)
        {
            var compactor = new ContextCompactor();
            agent.TransformContext = async (messages, ct) =>
            {
                var maxTokens = 128000;
                var history = messages
                    .Select(m => new ChatMessageContent { Role = m.Role.Value, Content = m.Text ?? "" })
                    .ToList();

                var currentTokens = compactor.EstimateTokens(history);
                if (currentTokens < maxTokens * (config.Session?.CompactThreshold ?? 0.8))
                    return messages;

                var compressed = await compactor.CompressAsync(history, maxTokens, client, ct);
                return compressed
                    .Select(m => new ChatMessage(new ChatRole(m.Role), m.Content))
                    .ToList();
            };
        }

        return agent;
    }

    // ================================================================
    // 交互模式
    // ================================================================

    static async Task<int> RunInteractiveAsync(
        string? providerOverride = null,
        string? modelOverride = null,
        bool resume = false,
        AgentMode mode = AgentMode.Build)
    {
        Console.WriteLine("morty - AI Coding Assistant");
        Console.WriteLine();

        _configLoader ??= new ConfigLoader();
        var config = _configLoader.Load();

        var agent = CreateAgent(config, providerOverride, modelOverride, resume, mode, out var error);
        if (agent == null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        agent.OnEvent += OnAgentEvent;

        var providerType = providerOverride ?? config.Provider?.Type ?? "zhipu";
        var model = modelOverride ?? config.Provider?.Model ?? "glm-5";
        Console.WriteLine($"Provider: {providerType} | Model: {model} | Mode: {mode}");
        if (resume) Console.WriteLine("Resuming last session");
        Console.WriteLine("Type 'quit' to exit, 'help' for commands");
        Console.WriteLine();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            agent.Abort();
            Console.WriteLine("\n[Interrupted]");
        };

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("> ");
            Console.ResetColor();

            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                PrintHelp();
                continue;
            }

            try
            {
                await agent.PromptAsync(input);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[Cancelled]");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        return 0;
    }

    // ================================================================
    // 单次对话模式
    // ================================================================

    static async Task<int> RunPromptAsync(
        string prompt,
        string? providerOverride = null,
        string? modelOverride = null,
        AgentMode mode = AgentMode.Build)
    {
        _configLoader ??= new ConfigLoader();
        var config = _configLoader.Load();

        var agent = CreateAgent(config, providerOverride, modelOverride, false, mode, out var error);
        if (agent == null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        agent.OnEvent += OnAgentEvent;

        try
        {
            await agent.PromptAsync(prompt);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Cancelled]");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        return 0;
    }

    // ================================================================
    // 事件处理
    // ================================================================

    private static void OnAgentEvent(AgentEvent evt)
    {
        switch (evt)
        {
            case AgentEvent.MessageDeltaEvent { Role: "assistant" } delta:
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(delta.Delta);
                Console.ResetColor();
                break;

            case AgentEvent.MessageEndEvent { Role: "assistant" }:
                Console.WriteLine();
                break;

            case AgentEvent.ToolExecutionStartEvent tool:
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write($"  [{tool.ToolName}]");
                if (tool.Args != null)
                {
                    var preview = string.Join(", ",
                        tool.Args.Take(2).Select(kv =>
                        {
                            var val = kv.Value?.ToString() ?? "";
                            if (val.Length > 60)
                                val = val[..57] + "...";
                            return $"{kv.Key}={val}";
                        }));
                    Console.Write($" {preview}");
                }
                Console.WriteLine();
                Console.ResetColor();
                break;

            case AgentEvent.ToolExecutionEndEvent { IsError: true } toolErr:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  Error: {toolErr.Result}");
                Console.ResetColor();
                break;

            case AgentEvent.RetryEvent retry:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [Retry {retry.Attempt}] {retry.Error}");
                Console.WriteLine($"  Waiting {retry.Delay.TotalSeconds:F0}s...");
                Console.ResetColor();
                break;

            case AgentEvent.CompactionTriggeredEvent:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  [Compacting context...]");
                Console.ResetColor();
                break;

            case AgentEvent.DoomLoopDetectedEvent doom:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [Doom loop] {doom.ToolName} repeated {doom.RepeatCount} times, switching strategy");
                Console.ResetColor();
                break;
        }
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
            Interactive commands:
              quit / exit    Exit interactive mode
              help           Show this help

            Use 'morty --help' for CLI usage.
            """);
    }

    // ================================================================
    // 权限系统
    // ================================================================

    private static Task<PermissionAction> AskUserPermission(PermissionRequest req)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  Permission: {req.Description}");
        Console.Write(" [y/N/always] ");
        Console.ResetColor();

        var input = Console.ReadLine()?.Trim().ToLower();
        var action = input switch
        {
            "y" or "yes" => PermissionAction.Allow,
            "a" or "always" => PermissionAction.Ask, // Ask 被复用为 "always allow"
            _ => PermissionAction.Deny
        };
        return Task.FromResult(action);
    }

    private static List<PermissionRule>? BuildPermissionRules(PermissionConfig? config)
    {
        if (config?.Rules == null || config.Rules.Count == 0)
            return null; // 使用默认规则

        return config.Rules.Select(r => new PermissionRule
        {
            Permission = r.Permission,
            Pattern = r.Pattern,
            Action = r.Action.ToLower() switch
            {
                "allow" => PermissionAction.Allow,
                "deny" => PermissionAction.Deny,
                _ => PermissionAction.Ask
            }
        }).ToList();
    }
}
