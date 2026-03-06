using System.Text.Json;
using Morty.Auth;
using Morty.Config;
using Morty.LLM;

namespace Morty.CLI;

class Program
{
    private static AuthManager? _authManager;
    private static ConfigLoader? _configLoader;

    static int Main(string[] args)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "morty", "logs");
        Directory.CreateDirectory(logDir);

        Console.WriteLine($"morty - AI Coding Assistant");
        Console.WriteLine();

        if (args.Length == 0)
        {
            RunInteractive();
            return 0;
        }

        var command = args[0];

        try
        {
            return command switch
            {
                "auth" => HandleAuth(args[1..]),
                "models" => HandleModels(args[1..]),
                "session" => HandleSession(args[1..]),
                _ => RunPrompt(string.Join(" ", args))
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static int HandleAuth(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: morty auth <login|list|logout> [provider]");
            return 0;
        }

        _authManager = new AuthManager();

        return args[0] switch
        {
            "login" => AuthLogin(args[1..]),
            "list" => AuthList(),
            "logout" => AuthLogout(args[1..]),
            _ => HandleAuth(Array.Empty<string>())
        };
    }

    static int AuthLogin(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: morty auth login <provider>");
            return 0;
        }

        var provider = args[0];
        Console.Write($"API Key for {provider}: ");
        var key = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(key))
        {
            Console.WriteLine("Error: API key is required");
            return 1;
        }

        _authManager!.LoginAsync(provider, key).Wait();
        Console.WriteLine($"Logged in to {provider}");
        return 0;
    }

    static int AuthList()
    {
        var providers = _authManager!.ListProviders().ToList();

        if (providers.Count == 0)
        {
            Console.WriteLine("No providers logged in");
            return 0;
        }

        Console.WriteLine("Logged in providers:");
        foreach (var p in providers)
        {
            Console.WriteLine($"  - {p}");
        }
        return 0;
    }

    static int AuthLogout(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: morty auth logout <provider>");
            return 0;
        }

        var provider = args[0];
        _authManager!.LogoutAsync(provider).Wait();
        Console.WriteLine($"Logged out from {provider}");
        return 0;
    }

    static int HandleModels(string[] args)
    {
        var provider = args.Length > 0 ? args[0] : null;

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
        {
            Console.WriteLine(m);
        }
        return 0;
    }

    static int HandleSession(string[] args)
    {
        Console.WriteLine("Session management not yet implemented");
        return 0;
    }

    static int RunPrompt(string prompt)
    {
        Console.WriteLine($"Prompt: {prompt}");
        Console.WriteLine("(TUI not yet integrated)");
        return 0;
    }

    static void RunInteractive()
    {
        Console.WriteLine("Interactive mode");
        Console.WriteLine("Type 'quit' to exit, 'help' for commands");
        Console.WriteLine();

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.ToLower() == "quit" || input.ToLower() == "exit")
                break;

            if (input.ToLower() == "help")
            {
                Console.WriteLine(@"Commands:
  morty auth login <provider>  - Login to a provider
  morty auth list             - List logged in providers
  morty auth logout <provider> - Logout from a provider
  morty models [provider]     - List available models
  quit                        - Exit");
                continue;
            }

            Console.WriteLine($"[Echo] {input}");
        }
    }
}
