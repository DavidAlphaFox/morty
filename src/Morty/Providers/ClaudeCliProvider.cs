using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Morty.Interfaces;
using Morty.Services;
using Serilog;

namespace Morty.Providers;

/// <summary>
/// Claude CLI 提供者 - 通过 bash 调用 claude 命令行工具
/// 使用 --dangerously-skip-permissions 跳过权限确认（后台无人交互场景必需）
/// 使用 --output-format json 获取结构化输出（含 cost_usd、is_error 等字段）
/// </summary>
public class ClaudeCliProvider : IClaudeProvider
{
    private readonly ProcessRunner _processRunner;
    private readonly string _workingDirectory;
    private readonly TimeSpan _timeout;
    private readonly Serilog.ILogger _logger;

    public ClaudeCliProvider(
        ProcessRunner processRunner,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        Serilog.ILogger? logger = null)
    {
        _processRunner = processRunner;
        _workingDirectory = workingDirectory ?? "/var/morty";
        _timeout = timeout ?? TimeSpan.FromMinutes(10);
        _logger = logger ?? Log.Logger;
    }

    public string Name => "Claude CLI";
    public string Type => "cli";

    /// <summary>
    /// 发送消息到 Claude CLI 并等待完整响应
    /// 流程：构建 bash 命令 → ProcessRunner 执行 → 解析 JSON 输出
    /// </summary>
    public async Task<ProviderResponse> SendMessageAsync(ProviderRequest request, CancellationToken ct = default)
    {
        var workingDir = request.WorkingDirectory ?? _workingDirectory;
        var bashCommand = BuildBashCommand(workingDir, request.EnvironmentVariables, request.SystemPrompt);

        _logger.Debug("执行 Claude CLI 命令, 工作目录: {WorkingDir}", workingDir);

        var result = await _processRunner.RunAsync(
            "/bin/bash",
            $"-c \"{bashCommand}\"",
            stdinInput: request.Message,
            _timeout,
            ct);

        if (result.TimedOut)
        {
            _logger.Warning("Claude CLI 进程超时");
            return new ProviderResponse(string.Empty, "进程超时", null, null, false);
        }

        if (!string.IsNullOrEmpty(result.Error) && result.ExitCode != 0)
        {
            _logger.Warning("Claude CLI 返回错误 (exit code {ExitCode}): {Error}", result.ExitCode, result.Error);
        }

        // 解析 JSON 输出（--output-format json 产生的结构化响应）
        var (content, costUsd, isError) = ParseJsonOutput(result.Output);
        var success = result.ExitCode == 0 && !isError;

        return new ProviderResponse(content, result.Error, null, costUsd, success);
    }

    /// <summary>
    /// 流式发送消息到 Claude CLI，逐行返回输出
    /// </summary>
    public async IAsyncEnumerable<string> StreamMessageAsync(
        ProviderRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var workingDir = request.WorkingDirectory ?? _workingDirectory;
        var bashCommand = BuildBashCommand(workingDir, request.EnvironmentVariables, request.SystemPrompt);

        await foreach (var line in _processRunner.RunStreamingAsync(
            "/bin/bash",
            $"-c \"{bashCommand}\"",
            stdinInput: request.Message,
            ct))
        {
            yield return line;
        }
    }

    /// <summary>
    /// 构建 bash 命令：切换目录 → 取消 CLAUDECODE 环境变量 → 设置自定义环境变量 → 调用 claude
    /// </summary>
    private string BuildBashCommand(string workingDir, string? envJson, string? systemPrompt = null)
    {
        var sb = new StringBuilder();

        sb.Append($"cd \"{EscapeForShell(workingDir)}\"");
        sb.Append(" && unset CLAUDECODE");

        if (!string.IsNullOrEmpty(envJson) && envJson != "null")
        {
            sb.Append($" && {ParseEnvJsonToExport(envJson)}");
        }

        // --dangerously-skip-permissions：后台无人交互，必须跳过权限确认
        // --output-format json：获取含 cost_usd、is_error 的结构化输出
        sb.Append(" && claude -p --dangerously-skip-permissions --output-format json");

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            sb.Append($" --append-system-prompt '{EscapeForSingleQuote(systemPrompt)}'");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 解析 JSON 环境变量并转换为 export 命令
    /// 输入格式: {"KEY":"value","KEY2":"value2"}
    /// 输出格式: export KEY='value' && export KEY2='value2'
    /// </summary>
    private string ParseEnvJsonToExport(string envJson)
    {
        try
        {
            var exports = new List<string>();
            var content = envJson.Trim().TrimStart('{').TrimEnd('}');

            if (string.IsNullOrWhiteSpace(content))
                return "";

            var pairs = content.Split(',');
            foreach (var pair in pairs)
            {
                var kv = pair.Trim();
                if (string.IsNullOrEmpty(kv)) continue;

                var colonIndex = kv.IndexOf(':');
                if (colonIndex <= 0) continue;

                var key = kv.Substring(0, colonIndex).Trim().Trim('"').Trim('\'');
                var value = kv.Substring(colonIndex + 1).Trim().Trim('"').Trim('\'');

                if (!string.IsNullOrEmpty(key))
                {
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
    /// 解析失败时回退到原始文本
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

            var content = root.TryGetProperty("result", out var resultElem)
                ? resultElem.GetString() ?? rawOutput
                : rawOutput;

            decimal? costUsd = root.TryGetProperty("cost_usd", out var costElem)
                ? costElem.GetDecimal()
                : null;

            var isError = root.TryGetProperty("is_error", out var errorElem)
                && errorElem.GetBoolean();

            _logger.Debug("Claude CLI JSON 解析成功: cost={CostUsd}, is_error={IsError}, content_length={Length}",
                costUsd, isError, content.Length);

            return (content, costUsd, isError);
        }
        catch (JsonException)
        {
            _logger.Debug("Claude CLI 输出非 JSON 格式，使用原始文本");
            return (rawOutput, null, false);
        }
    }

    /// <summary>双引号字符串内的 shell 转义</summary>
    private static string EscapeForShell(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>单引号字符串内的转义（用 '\'' 闭合-转义-重开）</summary>
    private static string EscapeForSingleQuote(string input)
    {
        return input
            .Replace("'", "'\\''")
            .Replace("\n", " ")
            .Replace("\r", "");
    }
}
