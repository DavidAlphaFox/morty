// =============================================================================
// MCP 客户端
// =============================================================================
// Model Context Protocol 客户端 (JSON-RPC over stdio)
// 支持连接外部 MCP Server 提供工具
// =============================================================================

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Morty.Tools;

/// <summary>
/// MCP 工具定义
/// </summary>
public class McpToolDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("inputSchema")]
    public JsonElement? InputSchema { get; set; }
}

/// <summary>
/// MCP 客户端 — JSON-RPC over stdio
/// </summary>
public class McpClient : IDisposable
{
    private Process? _serverProcess;
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private int _requestId;
    private bool _initialized;
    private readonly string _name;

    public McpClient(string name = "mcp")
    {
        _name = name;
    }

    /// <summary>
    /// 通过 stdio 连接 MCP Server
    /// </summary>
    public async Task ConnectStdioAsync(string command, string[]? args = null)
    {
        _serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args != null ? string.Join(" ", args) : "",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        _serverProcess.Start();
        _writer = _serverProcess.StandardInput;
        _reader = _serverProcess.StandardOutput;

        // Initialize
        await SendRequestAsync("initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "morty", version = "1.0" }
        });

        await SendNotificationAsync("notifications/initialized", new { });
        _initialized = true;
    }

    /// <summary>
    /// 列出服务器提供的工具
    /// </summary>
    public async Task<List<McpToolDefinition>> ListToolsAsync()
    {
        if (!_initialized) return new();

        var result = await SendRequestAsync("tools/list", new { });
        if (result.TryGetProperty("tools", out var tools))
        {
            return JsonSerializer.Deserialize<List<McpToolDefinition>>(
                tools.GetRawText()) ?? new();
        }

        return new();
    }

    /// <summary>
    /// 调用工具
    /// </summary>
    public async Task<string> CallToolAsync(string name, Dictionary<string, object?> args)
    {
        if (!_initialized) return "Error: MCP server not initialized";

        var result = await SendRequestAsync("tools/call", new
        {
            name,
            arguments = args
        });

        if (result.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var item in content.EnumerateArray())
            {
                if (item.TryGetProperty("text", out var text))
                    sb.AppendLine(text.GetString());
            }
            return sb.ToString().TrimEnd();
        }

        return result.GetRawText();
    }

    private async Task<JsonElement> SendRequestAsync(string method, object @params)
    {
        if (_writer == null || _reader == null)
            throw new InvalidOperationException("MCP server not started");

        var id = ++_requestId;
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params
        });

        var content = Encoding.UTF8.GetBytes(request);
        await _writer.WriteLineAsync(request);
        await _writer.FlushAsync();

        // 读取响应 (MCP 使用换行分隔)
        while (true)
        {
            var line = await _reader.ReadLineAsync();
            if (line == null) throw new EndOfStreamException("MCP server closed");

            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                if (root.TryGetProperty("id", out var idProp) &&
                    idProp.GetInt32() == id)
                {
                    if (root.TryGetProperty("result", out var result))
                        return result;
                    if (root.TryGetProperty("error", out var error))
                        throw new InvalidOperationException($"MCP error: {error.GetRawText()}");
                    return default;
                }
            }
            catch (JsonException)
            {
                // 跳过非 JSON 行
            }
        }
    }

    private async Task SendNotificationAsync(string method, object @params)
    {
        if (_writer == null) return;

        var notification = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method,
            @params
        });

        await _writer.WriteLineAsync(notification);
        await _writer.FlushAsync();
    }

    public void Dispose()
    {
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            try
            {
                _serverProcess.WaitForExit(3000);
            }
            catch { }

            if (!_serverProcess.HasExited)
                _serverProcess.Kill();
        }

        _serverProcess?.Dispose();
    }
}
