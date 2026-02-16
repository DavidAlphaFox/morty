using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Morty.Core.Interfaces;
using Serilog;

namespace Morty.Core.Providers;

/// <summary>
/// Claude CLI 提供者
/// 直接调用 claude -p 命令，无需启动脚本
/// </summary>
public class ClaudeCliProvider : IClaudeProvider
{
    private readonly string _workingDirectory;
    private readonly TimeSpan _timeout;
    private readonly Serilog.ILogger _logger;

    public ClaudeCliProvider(
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        Serilog.ILogger? logger = null)
    {
        _workingDirectory = workingDirectory ?? "/var/morty";
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
            // 使用请求中的工作目录或默认工作目录
            var workingDir = request.WorkingDirectory ?? _workingDirectory;

            // 构建 bash 命令：切换目录 -> 设置环境变量 -> 调用 claude
            var bashCommand = BuildBashCommand(workingDir, request.EnvironmentVariables, request.SystemPrompt);

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{bashCommand}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
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

            // 通过 stdin 发送消息
            await process.StandardInput.WriteLineAsync(request.Message);
            process.StandardInput.Close();

            var completed = await WaitForExitAsync(process, _timeout, ct);

            if (!completed)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                _logger.Warning("Claude CLI 进程超时");
                return new ProviderResponse(string.Empty, "进程超时", null, null, false);
            }

            var outputText = output.ToString();
            var errorText = error.ToString();
            var exitCode = process.ExitCode;

            // 检查是否有错误
            if (!string.IsNullOrEmpty(errorText) && exitCode != 0)
            {
                _logger.Warning("Claude CLI 返回错误 (exit code {ExitCode}): {Error}", exitCode, errorText);
            }

            // 尝试解析 JSON 输出（--output-format json）
            var (content, costUsd, isError) = ParseJsonOutput(outputText);

            // 综合 exit code 和 is_error 判断成功
            var success = exitCode == 0 && !isError;

            return new ProviderResponse(
                content,
                errorText,
                null,
                costUsd,
                success);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Claude CLI 调用失败");
            return new ProviderResponse(string.Empty, ex.Message, null, null, false);
        }
    }

    public async IAsyncEnumerable<string> StreamMessageAsync(
        ProviderRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var workingDir = request.WorkingDirectory ?? _workingDirectory;
        var bashCommand = BuildBashCommand(workingDir, request.EnvironmentVariables, request.SystemPrompt);

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{bashCommand}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        process.Start();

        // 通过 stdin 发送消息
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

    /// <summary>
    /// 构建 bash 命令
    /// </summary>
    private string BuildBashCommand(string workingDir, string? envJson, string? systemPrompt = null)
    {
        var sb = new StringBuilder();

        // 切换到工作目录
        sb.Append($"cd \"{EscapeForShell(workingDir)}\"");

        // 取消 CLAUDECODE 环境变量
        sb.Append(" && unset CLAUDECODE");

        // 解析并设置环境变量
        if (!string.IsNullOrEmpty(envJson) && envJson != "null")
        {
            sb.Append($" && {ParseEnvJsonToExport(envJson)}");
        }

        // 调用 claude，跳过权限确认（后台无人交互），使用 JSON 输出格式
        sb.Append(" && claude -p --dangerously-skip-permissions --output-format json");

        // 添加系统提示词（扩展系统提示）
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            sb.Append($" --append-system-prompt '{EscapeForSingleQuote(systemPrompt)}'");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 解析 JSON 环境变量并转换为 export 命令
    /// </summary>
    private string ParseEnvJsonToExport(string envJson)
    {
        try
        {
            // 简单解析 JSON: {"KEY":"value","KEY2":"value2"}
            var exports = new List<string>();

            // 移除首尾大括号
            var content = envJson.Trim().TrimStart('{').TrimEnd('}');

            if (string.IsNullOrWhiteSpace(content))
                return "";

            // 按逗号分割
            var pairs = content.Split(',');
            foreach (var pair in pairs)
            {
                var kv = pair.Trim();
                if (string.IsNullOrEmpty(kv)) continue;

                // 提取键值对
                var colonIndex = kv.IndexOf(':');
                if (colonIndex <= 0) continue;

                var key = kv.Substring(0, colonIndex).Trim().Trim('"').Trim('\'');
                var value = kv.Substring(colonIndex + 1).Trim().Trim('"').Trim('\'');

                if (!string.IsNullOrEmpty(key))
                {
                    // 转义单引号用于 bash export
                    var escapedValue = value.Replace("'", "'\\''");
                    exports.Add($"export {key}='{escapedValue}'");
                }
            }

            return string.Join(" && ", exports);
        }
        catch
        {
            _logger.Warning("解析环境变量 JSON 失败: {Json}", envJson);
            return "";
        }
    }

    /// <summary>
    /// 解析 Claude CLI 的 JSON 输出
    /// JSON 格式: {"type":"result","subtype":"success","cost_usd":0.xx,"is_error":false,"result":"..."}
    /// </summary>
    private (string content, decimal? costUsd, bool isError) ParseJsonOutput(string rawOutput)
    {
        var trimmed = rawOutput.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return (rawOutput, null, false);

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            // 提取 result 字段作为实际内容
            var content = root.TryGetProperty("result", out var resultElem)
                ? resultElem.GetString() ?? rawOutput
                : rawOutput;

            // 提取 cost_usd
            decimal? costUsd = root.TryGetProperty("cost_usd", out var costElem)
                ? costElem.GetDecimal()
                : null;

            // 提取 is_error
            var isError = root.TryGetProperty("is_error", out var errorElem)
                && errorElem.GetBoolean();

            _logger.Debug("Claude CLI JSON 解析成功: cost={CostUsd}, is_error={IsError}, content_length={Length}",
                costUsd, isError, content.Length);

            return (content, costUsd, isError);
        }
        catch (JsonException)
        {
            // JSON 解析失败，fallback 到原始文本
            _logger.Debug("Claude CLI 输出非 JSON 格式，使用原始文本");
            return (rawOutput, null, false);
        }
    }

    private static string EscapeForShell(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static string EscapeForSingleQuote(string input)
    {
        // 在单引号字符串中，用 '\'' 来转义单引号
        return input
            .Replace("'", "'\\''")
            .Replace("\n", " ")
            .Replace("\r", "");
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
