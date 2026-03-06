using System.Text.Json;

namespace Morty.Config;

public class ConfigLoader
{
    private readonly string[] _configPaths;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigLoader()
    {
        var configDir = Environment.GetEnvironmentVariable("MORTY_CONFIG_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "morty");

        _configPaths = new[]
        {
            Path.Combine(configDir, "morty.json"),
            Path.Combine(Environment.CurrentDirectory, ".morty")
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
    }

    public MortyConfig Load()
    {
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
                    Console.Error.WriteLine($"Warning: Failed to load config from {path}: {ex.Message}");
                }
            }
        }

        return new MortyConfig();
    }

    public static string ExpandPath(string path)
    {
        if (path.StartsWith("~/"))
        {
            var home = Environment.GetEnvironmentVariable("HOME") ?? "/home/" + Environment.GetEnvironmentVariable("USER");
            return Path.Combine(home, path[2..]);
        }

        return path;
    }
}
