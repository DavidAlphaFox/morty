using System.Runtime.CompilerServices;
using System.Text;
using Morty.Core.Interfaces;
using Serilog;

namespace Morty.Core.Providers;

/// <summary>
/// Claude CLI 提供者
/// </summary>
public class ClaudeCliProvider : IClaudeProvider
{
    private readonly string _command;
    private readonly TimeSpan _timeout;
    private readonly Serilog.ILogger _logger;

    public ClaudeCliProvider(
        string command = "claude",
        TimeSpan? timeout = null,
        Serilog.ILogger? logger = null)
    {
        _command = command;
        _timeout = timeout ?? TimeSpan.FromMinutes(10);
        _logger = logger ?? Log.Logger;
    }

    public string Name => "Claude CLI";
    public string Type => "cli";

    public async Task<ProviderResponse> SendMessageAsync(ProviderRequest request, CancellationToken ct = default)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();

        try
        {
            // 构建命令行参数
            var arguments = request.UsePlanMode ? "--plan -p" : "-p";

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _command,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new System.Diagnostics.Process { StartInfo = startInfo };

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

            await process.StandardInput.WriteLineAsync(request.Message);
            process.StandardInput.Close();

            var completed = await WaitForExitAsync(process, _timeout, ct);

            if (!completed)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return new ProviderResponse(string.Empty, "进程超时", null, null, false);
            }

            return new ProviderResponse(
                output.ToString(),
                error.ToString(),
                null,
                null,
                process.ExitCode == 0);
        }
        catch (Exception ex)
        {
            return new ProviderResponse(string.Empty, ex.Message, null, null, false);
        }
    }

    public async IAsyncEnumerable<string> StreamMessageAsync(
        ProviderRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // 构建命令行参数
        var arguments = request.UsePlanMode ? "--plan -p" : "-p";

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = _command,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        process.Start();

        await process.StandardInput.WriteLineAsync(request.Message);
        process.StandardInput.Close();

        using var reader = process.StandardOutput;

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            yield return line;
        }

        try { process.Kill(entireProcessTree: true); } catch { }
    }

    private static async Task<bool> WaitForExitAsync(
        System.Diagnostics.Process process,
        TimeSpan timeout,
        CancellationToken ct)
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
