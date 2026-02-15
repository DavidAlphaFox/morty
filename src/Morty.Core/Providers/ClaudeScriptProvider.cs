using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Morty.Core.Entities;
using Morty.Core.Interfaces;
using Morty.Core.Repositories;
using Serilog;

namespace Morty.Core.Providers;

/// <summary>
/// Claude CLI 提供者实现
/// 使用 bash 脚本启动 Claude，确保工作目录限制
/// </summary>
public class ClaudeScriptProvider : IClaudeProvider
{
    private readonly string _scriptPath;
    private readonly TimeSpan _timeout;
    private readonly Serilog.ILogger _logger;
    private readonly IEnvConfigGroupRepository? _envConfigGroupRepository;
    private readonly IEnvConfigRuleRepository? _envConfigRuleRepository;
    private readonly int? _projectId;

    public string Name => "Claude CLI (Script)";
    public string Type => "cli-script";

    public ClaudeScriptProvider(
        IEnvConfigGroupRepository? envConfigGroupRepository = null,
        IEnvConfigRuleRepository? envConfigRuleRepository = null,
        string scriptPath = "./scripts/claude-launcher.sh",
        TimeSpan? timeout = null,
        Serilog.ILogger? logger = null,
        int? projectId = null)
    {
        _scriptPath = scriptPath;
        _timeout = timeout ?? TimeSpan.FromMinutes(10);
        _logger = logger ?? Log.Logger;
        _envConfigGroupRepository = envConfigGroupRepository;
        _envConfigRuleRepository = envConfigRuleRepository;
        _projectId = projectId;
    }

    public async Task<ProviderResponse> SendMessageAsync(ProviderRequest request, CancellationToken ct = default)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();

        try
        {
            // 获取环境变量配置
            var envVars = await GetEnvironmentVariablesAsync(ct);

            // 构建环境变量 JSON
            var envJson = BuildEnvJson(envVars);

            // 确定工作目录
            var workingDir = GetWorkingDirectory();

            // 构建命令行参数
            var planMode = request.UsePlanMode ? "true" : "false";

            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"\"{_scriptPath}\" \"{workingDir}\" \"{planMode}\" \"{EscapeForBash(envJson)}\" \"{EscapeForBash(request.Message)}\"",
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

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // 等待进程完成
            var completed = await WaitForProcessExitAsync(process, _timeout, ct);

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
            _logger.Error(ex, "执行 Claude CLI 失败");
            return new ProviderResponse(string.Empty, ex.Message, null, null, false);
        }
    }

    public async IAsyncEnumerable<string> StreamMessageAsync(
        ProviderRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var envVars = await GetEnvironmentVariablesAsync(ct);
        var envJson = BuildEnvJson(envVars);
        var workingDir = GetWorkingDirectory();
        var planMode = request.UsePlanMode ? "true" : "false";

        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"\"{_scriptPath}\" \"{workingDir}\" \"{planMode}\" \"{EscapeForBash(envJson)}\" \"{EscapeForBash(request.Message)}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

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
    /// 获取环境变量配置
    /// TODO: 实现基于规则的环境变量匹配
    /// </summary>
    private async Task<List<EnvVariable>> GetEnvironmentVariablesAsync(CancellationToken ct)
    {
        // 暂时返回空列表，后续实现规则匹配逻辑
        return new List<EnvVariable>();
    }

    /// <summary>
    /// 获取工作目录
    /// </summary>
    private string GetWorkingDirectory()
    {
        // 可以从配置或环境变量获取
        return Environment.GetEnvironmentVariable("MORTY_WORKING_DIR") ?? "/tmp";
    }

    /// <summary>
    /// 构建环境变量 JSON
    /// </summary>
    private static string BuildEnvJson(List<EnvVariable> variables)
    {
        if (variables.Count == 0)
            return "{}";

        var dict = new Dictionary<string, string?>();
        foreach (var variable in variables)
        {
            if (!string.IsNullOrEmpty(variable.Value))
            {
                dict[variable.Key] = variable.Value;
            }
            else if (!string.IsNullOrEmpty(variable.DefaultValue))
            {
                dict[variable.Key] = variable.DefaultValue;
            }
            // 如果值和默认值都为空，则不添加
        }

        return JsonSerializer.Serialize(dict);
    }

    /// <summary>
    /// 转义字符串用于 bash 参数
    /// </summary>
    private static string EscapeForBash(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("$", "\\$")
            .Replace("`", "\\`");
    }

    private static async Task<bool> WaitForProcessExitAsync(
        Process process,
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
