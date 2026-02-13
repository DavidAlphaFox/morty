using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Morty.Core.Interfaces;
using Serilog;

namespace Morty.Core.Providers;

/// <summary>
/// Claude 供应商基类
/// </summary>
public abstract class BaseClaudeProvider : IClaudeProvider
{
    public abstract string Name { get; }
    public abstract string Type { get; }

    /// <summary>
    /// 发送 HTTP 请求
    /// </summary>
    protected abstract Task<HttpResponseMessage> SendHttpRequestAsync(
        string apiUrl,
        HttpContent content,
        CancellationToken ct);

    /// <summary>
    /// 解析响应内容
    /// </summary>
    protected abstract string ParseResponseContent(string responseBody);

    /// <summary>
    /// 发送消息
    /// </summary>
    public virtual async Task<ProviderResponse> SendMessageAsync(ProviderRequest request, CancellationToken ct = default)
    {
        try
        {
            var body = BuildRequestBody(request);
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await SendHttpRequestAsync(GetApiUrl(), content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                return new ProviderResponse(
                    string.Empty,
                    $"HTTP {response.StatusCode}: {error}",
                    null, null, false);
            }

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var parsedContent = ParseResponseContent(responseContent);

            return new ProviderResponse(
                parsedContent,
                null,
                ExtractTokenUsage(responseContent),
                ExtractCost(responseContent),
                true);
        }
        catch (Exception ex)
        {
            return new ProviderResponse(string.Empty, ex.Message, null, null, false);
        }
    }

    /// <summary>
    /// 流式发送消息
    /// </summary>
    public virtual async IAsyncEnumerable<string> StreamMessageAsync(
        ProviderRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var response = await SendMessageAsync(request, ct);
        if (response.Success)
        {
            yield return response.Content;
        }
    }

    /// <summary>
    /// 获取 API URL
    /// </summary>
    protected abstract string GetApiUrl();
    /// <summary>
    /// 获取模型名称
    /// </summary>
    protected abstract string GetModel();
    /// <summary>
    /// 构建请求体
    /// </summary>
    protected abstract string BuildRequestBody(ProviderRequest request);
    /// <summary>
    /// 提取 Token 使用量
    /// </summary>
    protected virtual int? ExtractTokenUsage(string responseBody) => null;
    /// <summary>
    /// 提取成本
    /// </summary>
    protected virtual decimal? ExtractCost(string responseBody) => null;
}

/// <summary>
/// Anthropic API 供应商实现
/// </summary>
public class AnthropicProvider : BaseClaudeProvider
{
    private readonly string _apiUrl;
    private readonly string _model;
    private readonly string _apiKey;
    private readonly Dictionary<string, object> _config;

    public AnthropicProvider(string name, string apiUrl, string model, string apiKey, Dictionary<string, object>? config = null)
    {
        _apiUrl = apiUrl;
        _model = model;
        _apiKey = apiKey;
        _config = config ?? new();
    }

    public override string Name => "Anthropic";
    public override string Type => "anthropic";

    protected override string GetApiUrl() => _apiUrl;
    protected override string GetModel() => _model;

    /// <summary>
    /// 发送 HTTP 请求到 Anthropic API
    /// </summary>
    protected override async Task<HttpResponseMessage> SendHttpRequestAsync(
        string apiUrl,
        HttpContent content,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
        {
            Content = content
        };
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var httpClient = new HttpClient();
        return await httpClient.SendAsync(request, ct);
    }

    /// <summary>
    /// 构建 Anthropic API 请求体
    /// </summary>
    protected override string BuildRequestBody(ProviderRequest request)
    {
        var maxTokens = request.Parameters?.GetValueOrDefault("maxTokens") ?? _config.GetValueOrDefault("maxTokens", 4096);
        var temperature = request.Parameters?.GetValueOrDefault("temperature") ?? _config.GetValueOrDefault("temperature", 0.7);

        var body = new
        {
            model = _model,
            max_tokens = maxTokens,
            temperature = temperature,
            messages = new[]
            {
                new { role = "user", content = request.Message }
            }
        };

        return JsonSerializer.Serialize(body);
    }

    /// <summary>
    /// 解析 Anthropic API 响应
    /// </summary>
    protected override string ParseResponseContent(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("content", out var content) && content.GetArrayLength() > 0)
            {
                foreach (var item in content.EnumerateArray())
                {
                    if (item.TryGetProperty("type", out var type) && type.GetString() == "text")
                    {
                        return item.GetProperty("text").GetString() ?? string.Empty;
                    }
                }
            }
        }
        catch { }

        return responseBody;
    }

    /// <summary>
    /// 提取 Token 使用量
    /// </summary>
    protected override int? ExtractTokenUsage(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("usage", out var usage))
            {
                var inputTokens = usage.GetProperty("input_tokens").GetInt32();
                var outputTokens = usage.GetProperty("output_tokens").GetInt32();
                return inputTokens + outputTokens;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 提取成本
    /// </summary>
    protected override decimal? ExtractCost(string responseBody)
    {
        return null;
    }
}

/// <summary>
/// Claude CLI 供应商实现 (向后兼容)
/// </summary>
public class ClaudeCliProvider : IClaudeProvider
{
    private readonly string _command;
    private readonly string _args;
    private readonly TimeSpan _timeout;
    private readonly Serilog.ILogger _logger;

    public ClaudeCliProvider(
        string command = "claude",
        string args = "-p",
        TimeSpan? timeout = null,
        Serilog.ILogger? logger = null)
    {
        _command = command;
        _args = args;
        _timeout = timeout ?? TimeSpan.FromMinutes(10);
        _logger = logger ?? Log.Logger;
    }

    public string Name => "Claude CLI";
    public string Type => "cli";

    /// <summary>
    /// 发送消息到 Claude CLI
    /// </summary>
    public async Task<ProviderResponse> SendMessageAsync(ProviderRequest request, CancellationToken ct = default)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _command,
                Arguments = _args,
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

    /// <summary>
    /// 流式发送消息
    /// </summary>
    public async IAsyncEnumerable<string> StreamMessageAsync(
        ProviderRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = _command,
            Arguments = _args,
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

    /// <summary>
    /// 等待进程退出
    /// </summary>
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
