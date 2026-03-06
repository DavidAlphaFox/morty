// =============================================================================
// LSP 客户端
// =============================================================================
// Language Server Protocol 客户端实现 (JSON-RPC over stdio)
// 支持 goToDefinition, findReferences, hover, documentSymbol
// =============================================================================

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Morty.Tools;

/// <summary>
/// LSP 位置信息
/// </summary>
public class LspLocation
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();
}

public class LspRange
{
    [JsonPropertyName("start")]
    public LspPosition Start { get; set; } = new();

    [JsonPropertyName("end")]
    public LspPosition End { get; set; } = new();
}

public class LspPosition
{
    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("character")]
    public int Character { get; set; }
}

/// <summary>
/// LSP 客户端 — JSON-RPC over stdio
/// </summary>
public class LspClient : IDisposable
{
    private Process? _serverProcess;
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private int _requestId;
    private bool _initialized;

    /// <summary>
    /// 启动 LSP Server
    /// </summary>
    public async Task InitializeAsync(string command, string[] args, string rootPath)
    {
        _serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = string.Join(" ", args),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        _serverProcess.Start();
        _writer = _serverProcess.StandardInput;
        _reader = _serverProcess.StandardOutput;

        // Initialize request
        var initResult = await SendRequestAsync("initialize", new
        {
            processId = Environment.ProcessId,
            rootUri = $"file://{rootPath}",
            capabilities = new { }
        });

        // Initialized notification
        await SendNotificationAsync("initialized", new { });
        _initialized = true;
    }

    /// <summary>
    /// 跳转到定义
    /// </summary>
    public async Task<string> GoToDefinitionAsync(string file, int line, int character)
    {
        if (!_initialized) return "Error: LSP server not initialized";

        var result = await SendRequestAsync("textDocument/definition", new
        {
            textDocument = new { uri = $"file://{Path.GetFullPath(file)}" },
            position = new { line = line - 1, character = character - 1 }
        });

        return FormatLocations(result);
    }

    /// <summary>
    /// 查找引用
    /// </summary>
    public async Task<string> FindReferencesAsync(string file, int line, int character)
    {
        if (!_initialized) return "Error: LSP server not initialized";

        var result = await SendRequestAsync("textDocument/references", new
        {
            textDocument = new { uri = $"file://{Path.GetFullPath(file)}" },
            position = new { line = line - 1, character = character - 1 },
            context = new { includeDeclaration = true }
        });

        return FormatLocations(result);
    }

    /// <summary>
    /// 悬浮信息
    /// </summary>
    public async Task<string> HoverAsync(string file, int line, int character)
    {
        if (!_initialized) return "Error: LSP server not initialized";

        var result = await SendRequestAsync("textDocument/hover", new
        {
            textDocument = new { uri = $"file://{Path.GetFullPath(file)}" },
            position = new { line = line - 1, character = character - 1 }
        });

        if (result.ValueKind == JsonValueKind.Null)
            return "No hover information available";

        if (result.TryGetProperty("contents", out var contents))
        {
            if (contents.ValueKind == JsonValueKind.String)
                return contents.GetString() ?? "";
            if (contents.TryGetProperty("value", out var value))
                return value.GetString() ?? "";
        }

        return result.GetRawText();
    }

    /// <summary>
    /// 文档符号
    /// </summary>
    public async Task<string> DocumentSymbolAsync(string file)
    {
        if (!_initialized) return "Error: LSP server not initialized";

        var result = await SendRequestAsync("textDocument/documentSymbol", new
        {
            textDocument = new { uri = $"file://{Path.GetFullPath(file)}" }
        });

        if (result.ValueKind != JsonValueKind.Array)
            return "No symbols found";

        var sb = new StringBuilder();
        foreach (var symbol in result.EnumerateArray())
        {
            var name = symbol.GetProperty("name").GetString();
            var kind = symbol.TryGetProperty("kind", out var k) ? k.GetInt32() : 0;
            var kindName = SymbolKindName(kind);
            sb.AppendLine($"  {kindName} {name}");
        }

        return sb.Length > 0 ? sb.ToString().TrimEnd() : "No symbols found";
    }

    /// <summary>
    /// 自动检测项目对应的 LSP Server
    /// </summary>
    public static (string command, string[] args)? DetectLspServer(string cwd)
    {
        if (Directory.GetFiles(cwd, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0 ||
            Directory.GetFiles(cwd, "*.sln", SearchOption.TopDirectoryOnly).Length > 0)
            return ("omnisharp", new[] { "-lsp" });

        if (File.Exists(Path.Combine(cwd, "tsconfig.json")) ||
            File.Exists(Path.Combine(cwd, "package.json")))
            return ("typescript-language-server", new[] { "--stdio" });

        if (File.Exists(Path.Combine(cwd, "pyproject.toml")) ||
            File.Exists(Path.Combine(cwd, "setup.py")))
            return ("pyright-langserver", new[] { "--stdio" });

        if (File.Exists(Path.Combine(cwd, "go.mod")))
            return ("gopls", Array.Empty<string>());

        if (File.Exists(Path.Combine(cwd, "Cargo.toml")))
            return ("rust-analyzer", Array.Empty<string>());

        return null;
    }

    private async Task<JsonElement> SendRequestAsync(string method, object @params)
    {
        if (_writer == null || _reader == null)
            throw new InvalidOperationException("LSP server not started");

        var id = ++_requestId;
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params
        });

        var content = Encoding.UTF8.GetBytes(request);
        await _writer.WriteAsync($"Content-Length: {content.Length}\r\n\r\n");
        await _writer.WriteAsync(request);
        await _writer.FlushAsync();

        // 读取响应
        return await ReadResponseAsync(id);
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

        var content = Encoding.UTF8.GetBytes(notification);
        await _writer.WriteAsync($"Content-Length: {content.Length}\r\n\r\n");
        await _writer.WriteAsync(notification);
        await _writer.FlushAsync();
    }

    private async Task<JsonElement> ReadResponseAsync(int expectedId)
    {
        if (_reader == null)
            throw new InvalidOperationException("LSP server not started");

        // 读取 Content-Length header
        while (true)
        {
            var headerLine = await _reader.ReadLineAsync();
            if (headerLine == null)
                throw new EndOfStreamException("LSP server closed connection");

            if (headerLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                var length = int.Parse(headerLine["Content-Length:".Length..].Trim());
                await _reader.ReadLineAsync(); // empty line

                var buffer = new char[length];
                var read = 0;
                while (read < length)
                    read += await _reader.ReadAsync(buffer, read, length - read);

                var json = new string(buffer);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // 跳过通知，只处理带 id 的响应
                if (root.TryGetProperty("id", out var idProp) && idProp.GetInt32() == expectedId)
                {
                    if (root.TryGetProperty("result", out var result))
                        return result;
                    if (root.TryGetProperty("error", out var error))
                        throw new InvalidOperationException($"LSP error: {error.GetRawText()}");
                    return default;
                }
            }
        }
    }

    private static string FormatLocations(JsonElement result)
    {
        if (result.ValueKind == JsonValueKind.Null)
            return "No results";

        var sb = new StringBuilder();

        if (result.ValueKind == JsonValueKind.Array)
        {
            foreach (var loc in result.EnumerateArray())
                AppendLocation(sb, loc);
        }
        else if (result.ValueKind == JsonValueKind.Object)
        {
            AppendLocation(sb, result);
        }

        return sb.Length > 0 ? sb.ToString().TrimEnd() : "No results";
    }

    private static void AppendLocation(StringBuilder sb, JsonElement loc)
    {
        var uri = loc.TryGetProperty("uri", out var u) ? u.GetString() ?? "" : "";
        var file = uri.StartsWith("file://") ? uri[7..] : uri;

        if (loc.TryGetProperty("range", out var range) &&
            range.TryGetProperty("start", out var start))
        {
            var line = start.GetProperty("line").GetInt32() + 1;
            var col = start.GetProperty("character").GetInt32() + 1;
            sb.AppendLine($"  {file}:{line}:{col}");
        }
        else
        {
            sb.AppendLine($"  {file}");
        }
    }

    private static string SymbolKindName(int kind) => kind switch
    {
        1 => "File", 2 => "Module", 3 => "Namespace", 4 => "Package",
        5 => "Class", 6 => "Method", 7 => "Property", 8 => "Field",
        9 => "Constructor", 10 => "Enum", 11 => "Interface", 12 => "Function",
        13 => "Variable", 14 => "Constant", 23 => "Struct", 24 => "Event",
        _ => $"Symbol({kind})"
    };

    public void Dispose()
    {
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            try
            {
                SendNotificationAsync("shutdown", new { }).Wait();
                SendNotificationAsync("exit", new { }).Wait();
                _serverProcess.WaitForExit(3000);
            }
            catch { }

            if (!_serverProcess.HasExited)
                _serverProcess.Kill();
        }

        _serverProcess?.Dispose();
    }
}
