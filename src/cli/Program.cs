using Serilog;
using Serilog.Sinks.File;

namespace Morty.CLI;

class Program
{
    static int Main(string[] args)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "morty", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(Path.Combine(logDir, "morty-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        Log.Information("morty starting...");

        try
        {
            if (args.Length == 0)
            {
                RunInteractive();
            }
            else if (args[0] == "--help" || args[0] == "-h")
            {
                PrintHelp();
            }
            else
            {
                var prompt = string.Join(" ", args);
                RunOnce(prompt);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.Information("morty shutting down");
            Log.CloseAndFlush();
        }
    }

    static void RunInteractive()
    {
        Console.WriteLine("morty interactive mode");
        Console.WriteLine("Type 'quit' to exit");

        while (true)
        {
            Console.Write("\n> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.ToLower() == "quit" || input.ToLower() == "exit")
                break;

            Console.WriteLine($"[Echo] {input}");
        }
    }

    static void RunOnce(string prompt)
    {
        Console.WriteLine($"Prompt: {prompt}");
    }

    static void PrintHelp()
    {
        Console.WriteLine(@"morty - AI Coding Assistant

Usage:
  morty                  Start interactive mode
  morty <prompt>        Run a single prompt
  morty --help, -h      Show this help

Examples:
  morty                  # Interactive mode
  morty Hello world      # Single prompt
  morty ""Hello world"" # Single prompt with spaces
");
    }
}
