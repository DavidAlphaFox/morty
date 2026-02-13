using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Morty.Core.Interfaces;
using Serilog;

namespace Morty.Core.Services;

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

    public async Task<ClaudeResponse> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        return await SendMessageWithContextAsync(message, ".", cancellationToken);
    }

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

            _logger.Debug("Starting Claude process with message: {Message}", message.Substring(0, Math.Min(100, message.Length)));

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.StandardInput.WriteLineAsync(message);
            process.StandardInput.Close();

            var completed = await WaitForExitAsync(process, _timeout, cancellationToken);

            if (!completed)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                _logger.Warning("Claude process timed out after {Timeout}", _timeout);
                return new ClaudeResponse(output.ToString(), "Process timed out", -1, false);
            }

            var exitCode = process.ExitCode;
            var success = exitCode == 0;

            if (!success)
            {
                _logger.Warning("Claude process exited with code {ExitCode}", exitCode);
            }

            return new ClaudeResponse(output.ToString(), error.ToString(), exitCode, success);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to execute Claude process");
            return new ClaudeResponse(output.ToString(), ex.Message, -1, false);
        }
    }

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
