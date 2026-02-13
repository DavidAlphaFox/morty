using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Morty.Core.Interfaces;
using Serilog;

namespace Morty.Core.Services;

/// <summary>
/// Claude CLI 客户端封装
/// </summary>
public class ClaudeClient : IClaudeClient
{
    private readonly string _command;
    private readonly string _args;
    private readonly TimeSpan _timeout;
    private readonly ILogger _logger;

    public ClaudeClient(
        string command = "claude",
        string args = "-p",
        TimeSpan? timeout = null,
        ILogger? logger = null)
    {
        _command = command;
        _args = args;
        _timeout = timeout ?? TimeSpan.FromMinutes(10);
        _logger = logger ?? Log.Logger;
    }

    /// <summary>
    /// 发送消息到 Claude CLI
    /// </summary>
    public async Task<ClaudeResponse> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        return await SendMessageWithContextAsync(message, ".", cancellationToken);
    }

    /// <summary>
    /// 使用指定项目路径发送消息到 Claude CLI
    /// </summary>
    public async Task<ClaudeResponse> SendMessageWithContextAsync(
        string message,
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _command,
                Arguments = _args,
                WorkingDirectory = projectPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };

            // 捕获标准输出
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    output.AppendLine(e.Data);
            };

            // 捕获标准错误
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    error.AppendLine(e.Data);
            };

            _logger.Debug("启动 Claude 进程，消息: {Message}", message.Substring(0, Math.Min(100, message.Length)));

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.StandardInput.WriteLineAsync(message);
            process.StandardInput.Close();

            var completed = await WaitForExitAsync(process, _timeout, cancellationToken);

            if (!completed)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                _logger.Warning("Claude 进程超时: {Timeout}", _timeout);
                return new ClaudeResponse(output.ToString(), "进程超时", -1, false);
            }

            var exitCode = process.ExitCode;
            var success = exitCode == 0;

            if (!success)
            {
                _logger.Warning("Claude 进程退出码: {ExitCode}", exitCode);
            }

            return new ClaudeResponse(output.ToString(), error.ToString(), exitCode, success);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "执行 Claude 进程失败");
            return new ClaudeResponse(output.ToString(), ex.Message, -1, false);
        }
    }

    /// <summary>
    /// 流式发送消息到 Claude CLI
    /// </summary>
    public async IAsyncEnumerable<string> StreamMessageAsync(
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _command,
            Arguments = _args,
            WorkingDirectory = ".",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        await process.StandardInput.WriteLineAsync(message);
        process.StandardInput.Close();

        using var reader = process.StandardOutput;

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;
            yield return line;
        }

        try { process.Kill(entireProcessTree: true); } catch { }
    }

    /// <summary>
    /// 等待进程退出
    /// </summary>
    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
