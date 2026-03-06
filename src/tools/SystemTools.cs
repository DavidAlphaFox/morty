// =============================================================================
// 系统工具
// =============================================================================
// 提供 bash、grep、find、ls 等系统命令
// 支持命令白名单、超时控制和输出截断
// 参考 pi-mono 的 bash/grep/find/ls 工具:
//   - bash: 尾部截断输出 (保留最后 N 行)
//   - grep: 使用 grep -rn，匹配数量限制
// =============================================================================

using System.Diagnostics;
using System.Text;

namespace Morty.Tools;

/// <summary>
/// 系统工具
/// </summary>
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

    /// <summary>
    /// 执行 bash 命令 (输出自动截断，保留尾部)
    /// </summary>
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
                Arguments = $"-c \"{EscapeCommand(command)}\"",
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

            var fullOutput = string.IsNullOrEmpty(error)
                ? output
                : $"{output}\n{error}";

            if (process.ExitCode != 0)
                fullOutput += $"\n\nCommand exited with code {process.ExitCode}";

            // 应用尾部截断
            var truncation = OutputTruncator.TruncateTail(fullOutput);
            if (truncation.Truncated)
            {
                var startLine = truncation.TotalLines - truncation.OutputLines + 1;
                return truncation.Content +
                    $"\n\n[Showing lines {startLine}-{truncation.TotalLines} of {truncation.TotalLines}. Output truncated.]";
            }

            return string.IsNullOrEmpty(truncation.Content) ? "(no output)" : truncation.Content;
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            throw new TimeoutException($"Command timed out after {_timeout} seconds: {command}");
        }
    }

    /// <summary>
    /// 搜索文件内容 (grep -rn)
    /// </summary>
    public async Task<string> Grep(string pattern, string? path = null)
    {
        var searchPath = path ?? ".";
        // 使用 grep -rn 带行号，限制最大匹配数
        var result = await RunCommand($"grep -rn --color=never '{Escape(pattern)}' {searchPath} | head -100");

        var truncation = OutputTruncator.TruncateHead(result);
        if (truncation.Truncated)
            return truncation.Content + "\n\n[Output truncated. Refine your search pattern for more specific results.]";

        return string.IsNullOrEmpty(result) ? "No matches found" : result;
    }

    /// <summary>
    /// Glob 文件查找 (自动排除常见目录，按修改时间排序)
    /// </summary>
    public async Task<string> Glob(string pattern, string? path = null)
    {
        var searchPath = path ?? ".";
        var excludeDirs = new[] { ".git", "node_modules", "bin", "obj", ".vs", "__pycache__", ".cache" };

        // 使用 find + glob 模式
        var excludeArgs = string.Join(" ",
            excludeDirs.Select(d => $"! -path '*/{d}/*'"));
        var command = $"find {searchPath} -name '{Escape(pattern)}' {excludeArgs} -type f 2>/dev/null | head -200";

        var result = await RunCommand(command);
        if (string.IsNullOrEmpty(result))
            return "No files found";

        // 按修改时间排序 (最近修改的在前)
        var files = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var sorted = files
            .Select(f =>
            {
                var fullPath = Path.IsPathRooted(f) ? f : Path.Combine(_workingDirectory, f);
                var mtime = File.Exists(fullPath) ? File.GetLastWriteTimeUtc(fullPath) : DateTime.MinValue;
                return (Path: f, MTime: mtime);
            })
            .OrderByDescending(f => f.MTime)
            .Select(f => f.Path)
            .ToArray();

        return string.Join('\n', sorted);
    }

    /// <summary>
    /// 列出目录内容 (ls -la)
    /// </summary>
    public async Task<string> Ls(string? path = null)
    {
        var targetPath = path ?? ".";
        return await RunCommand($"ls -la {targetPath}");
    }

    /// <summary>
    /// 执行命令并返回输出 (内部方法，不截断)
    /// </summary>
    private async Task<string> RunCommand(string command)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeout));

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{EscapeCommand(command)}\"",
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();

        try
        {
            var output = await process.StandardOutput.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);
            return output.TrimEnd();
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            throw new TimeoutException($"Command timed out after {_timeout} seconds");
        }
    }

    private static string Escape(string s) => s.Replace("'", "'\\''");

    private static string EscapeCommand(string s) => s.Replace("\"", "\\\"");
}
