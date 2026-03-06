using System.Diagnostics;

namespace Morty.Tools;

public class SystemTools
{
    private readonly string _workingDirectory;
    private readonly HashSet<string> _allowedCommands;
    private readonly int _timeout;

    public SystemTools(string workingDirectory, List<string>? allowedCommands = null, int timeout = 300)
    {
        _workingDirectory = Path.GetFullPath(workingDirectory);
        _allowedCommands = allowedCommands != null
            ? new HashSet<string>(allowedCommands, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _timeout = timeout;
    }

    public async Task<string> Bash(string command)
    {
        if (_allowedCommands.Count > 0)
        {
            var firstWord = command.Split(' ')[0];
            if (!_allowedCommands.Contains(firstWord))
                throw new UnauthorizedAccessException($"命令不在白名单中: {firstWord}");
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeout));

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            },
            EnableRaisingEvents = true
        };

        process.Start();

        try
        {
            var output = await process.StandardOutput.ReadToEndAsync(cts.Token);
            var error = await process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);

            return string.IsNullOrEmpty(error)
                ? output
                : $"输出:\n{output}\n错误:\n{error}";
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            throw new TimeoutException($"命令执行超时: {command}");
        }
    }

    public async Task<string> Grep(string pattern, string? path = null)
    {
        var searchPath = path ?? _workingDirectory;
        return await Bash($"grep -rn '{Escape(pattern)}' {searchPath}");
    }

    public async Task<string> Find(string pattern, string? path = null)
    {
        var searchPath = path ?? _workingDirectory;
        return await Bash($"find {searchPath} -name '{Escape(pattern)}'");
    }

    public async Task<string> Ls(string? path = null)
    {
        var targetPath = path ?? _workingDirectory;
        return await Bash($"ls -la {targetPath}");
    }

    private static string Escape(string s) => s.Replace("'", "\\'");
}
