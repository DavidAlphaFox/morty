// =============================================================================
// 系统工具
// =============================================================================
// 提供 bash、grep、find、ls 等系统命令
// 支持命令白名单和超时控制
// =============================================================================

using System.Diagnostics;

namespace Morty.Tools;

/// <summary>
/// 系统工具
/// </summary>
public class SystemTools
{
    /// <summary>
    /// 工作目录
    /// </summary>
    private readonly string _workingDirectory;

    /// <summary>
    /// 允许执行的命令白名单
    /// </summary>
    private readonly HashSet<string> _allowedCommands;

    /// <summary>
    /// 命令超时时间 (秒)
    /// </summary>
    private readonly int _timeout;

    /// <summary>
    /// 初始化系统工具
    /// </summary>
    /// <param name="workingDirectory">工作目录</param>
    /// <param name="allowedCommands">允许的命令列表 (空表示允许所有)</param>
    /// <param name="timeout">超时时间 (秒)</param>
    public SystemTools(string workingDirectory, List<string>? allowedCommands = null, int timeout = 300)
    {
        _workingDirectory = Path.GetFullPath(workingDirectory);
        _allowedCommands = allowedCommands != null
            ? new HashSet<string>(allowedCommands, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _timeout = timeout;
    }

    /// <summary>
    /// 执行 bash 命令
    /// </summary>
    /// <param name="command">命令</param>
    /// <returns>命令输出</returns>
    /// <exception cref="UnauthorizedAccessException">命令不在白名单中</exception>
    /// <exception cref="TimeoutException">命令执行超时</exception>
    public async Task<string> Bash(string command)
    {
        // 检查白名单
        if (_allowedCommands.Count > 0)
        {
            var firstWord = command.Split(' ')[0];
            if (!_allowedCommands.Contains(firstWord))
                throw new UnauthorizedAccessException($"命令不在白名单中: {firstWord}");
        }

        // 设置超时
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

    /// <summary>
    /// 搜索文件内容 (grep)
    /// </summary>
    /// <param name="pattern">正则表达式</param>
    /// <param name="path">搜索路径 (可选)</param>
    /// <returns>搜索结果</returns>
    public async Task<string> Grep(string pattern, string? path = null)
    {
        var searchPath = path ?? _workingDirectory;
        return await Bash($"grep -rn '{Escape(pattern)}' {searchPath}");
    }

    /// <summary>
    /// 查找文件 (find)
    /// </summary>
    /// <param name="pattern">文件名模式</param>
    /// <param name="path">搜索路径 (可选)</param>
    /// <returns>搜索结果</returns>
    public async Task<string> Find(string pattern, string? path = null)
    {
        var searchPath = path ?? _workingDirectory;
        return await Bash($"find {searchPath} -name '{Escape(pattern)}'");
    }

    /// <summary>
    /// 列出目录内容 (ls)
    /// </summary>
    /// <param name="path">目录路径 (可选)</param>
    /// <returns>目录列表</returns>
    public async Task<string> Ls(string? path = null)
    {
        var targetPath = path ?? _workingDirectory;
        return await Bash($"ls -la {targetPath}");
    }

    /// <summary>
    /// 转义单引号
    /// </summary>
    private static string Escape(string s) => s.Replace("'", "\\'");
}
