using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Serilog;

namespace Morty.Services;

/// <summary>
/// 进程执行结果
/// </summary>
/// <param name="Output">标准输出内容</param>
/// <param name="Error">标准错误内容</param>
/// <param name="ExitCode">进程退出码</param>
/// <param name="TimedOut">是否因超时被终止</param>
public record ProcessResult(string Output, string Error, int ExitCode, bool TimedOut);

/// <summary>
/// 通用进程执行器 - 封装外部 CLI 进程的启动、输入输出和超时处理
/// 所有需要调用外部命令行工具的服务都应使用此类，避免重复的进程管理代码
/// </summary>
public class ProcessRunner
{
    private readonly Serilog.ILogger _logger;

    public ProcessRunner(Serilog.ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;
    }

    /// <summary>
    /// 执行外部进程并等待其完成
    /// </summary>
    /// <param name="fileName">可执行文件路径（如 /bin/bash）</param>
    /// <param name="arguments">命令行参数</param>
    /// <param name="stdinInput">通过 stdin 发送给进程的文本，null 表示不写入</param>
    /// <param name="timeout">最大等待时间，超过后强制终止进程树</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>包含输出、错误、退出码和是否超时的结果</returns>
    public async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string? stdinInput,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = stdinInput != null,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    output.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    error.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (stdinInput != null)
            {
                await process.StandardInput.WriteLineAsync(stdinInput);
                process.StandardInput.Close();
            }

            var completed = await WaitForExitAsync(process, timeout, ct);

            if (!completed)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                _logger.Warning("进程超时被终止: {FileName} {Arguments}", fileName, arguments);
                return new ProcessResult(output.ToString(), "进程超时", -1, TimedOut: true);
            }

            return new ProcessResult(output.ToString(), error.ToString(), process.ExitCode, TimedOut: false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "执行进程失败: {FileName}", fileName);
            return new ProcessResult(output.ToString(), ex.Message, -1, TimedOut: false);
        }
    }

    /// <summary>
    /// 以流式方式执行外部进程，逐行返回标准输出
    /// 适用于需要实时获取输出的场景（如流式 AI 响应）
    /// </summary>
    /// <param name="fileName">可执行文件路径</param>
    /// <param name="arguments">命令行参数</param>
    /// <param name="stdinInput">通过 stdin 发送给进程的文本</param>
    /// <param name="ct">取消令牌，取消时强制终止进程</param>
    /// <returns>逐行产生的标准输出内容</returns>
    public async IAsyncEnumerable<string> RunStreamingAsync(
        string fileName,
        string arguments,
        string? stdinInput,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdinInput != null,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        if (stdinInput != null)
        {
            await process.StandardInput.WriteLineAsync(stdinInput);
            process.StandardInput.Close();
        }

        using var reader = process.StandardOutput;

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            yield return line;
        }

        try { process.Kill(entireProcessTree: true); } catch { }
    }

    /// <summary>
    /// 等待进程退出，支持超时和取消
    /// </summary>
    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
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
