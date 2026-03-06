using System.Text.Json;

namespace Morty.Auth;

public class AuthStore
{
    private readonly string _authFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuthStore()
    {
        var shareDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "morty");
        Directory.CreateDirectory(shareDir);
        _authFilePath = Path.Combine(shareDir, "auth.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    public Dictionary<string, Credential> Load()
    {
        if (!File.Exists(_authFilePath))
            return new Dictionary<string, Credential>();

        try
        {
            var json = File.ReadAllText(_authFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, Credential>>(json) 
                ?? new Dictionary<string, Credential>();
        }
        catch
        {
            return new Dictionary<string, Credential>();
        }
    }

    public void Save(Dictionary<string, Credential> credentials)
    {
        var json = JsonSerializer.Serialize(credentials, _jsonOptions);
        File.WriteAllText(_authFilePath, json);

        if (OperatingSystem.IsLinux())
        {
            SetFilePermissions();
        }
    }

    private void SetFilePermissions()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/chmod",
                Arguments = "600 \"\"",
                UseShellExecute = false
            };
        }
        catch
        {
            // Ignore chmod errors
        }
    }
}
