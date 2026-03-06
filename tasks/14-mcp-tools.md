# 任务: 实现 MCP 工具

## 阶段
Phase 5: 工具系统

## 描述
实现 MCP (Model Context Protocol) 支持，支持 stdio 和 sse 两种连接方式，集成 Playwright、Filesystem 等 MCP 服务器。

## 验收标准
- [ ] 可连接 MCP 服务器
- [ ] 支持 stdio 模式
- [ ] 支持 sse 模式
- [ ] 可调用 MCP 工具

## 实现步骤

### 14.1 定义 MCP 客户端接口
创建 `src/tools/mcp/IMcpClient.cs`:
```csharp
public interface IMcpClient : IDisposable
{
    string Name { get; }
    bool IsConnected { get; }
    
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    
    Task InitializeAsync(CancellationToken ct = default);
    IReadOnlyList<McpTool> GetTools();
    
    Task<McpCallResult> CallToolAsync(string name, Dictionary<string, object> args, CancellationToken ct = default);
}

public class McpTool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public object? InputSchema { get; set; }
}

public class McpCallResult
{
    public bool IsError { get; set; }
    public string Content { get; set; } = "";
}
```

### 14.2 实现 Stdio MCP 客户端
创建 `src/tools/mcp/StdioMcpClient.cs`:
```csharp
public class StdioMcpClient : IMcpClient
{
    private Process? _process;
    private readonly string _command;
    private readonly string[] _args;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public string Name { get; }
    public bool IsConnected { get; private set; }
    
    public StdioMcpClient(string name, string command, string[] args)
    {
        Name = name;
        _command = command;
        _args = args;
    }
    
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _command,
                Arguments = string.Join(" ", _args),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        
        _process.Start();
        
        // 等待进程启动
        await Task.Delay(500, ct);
        IsConnected = true;
    }
    
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // 发送 initialize 请求
        var request = new JsonRpcRequest
        {
            Id = 1,
            Method = "initialize",
            Params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "morty", version = "0.1.0" }
            }
        };
        
        await SendAsync(request, ct);
        
        // 发送 initialized 通知
        await SendAsync(new JsonRpcNotification { Method = "initialized" }, ct);
    }
    
    private async Task SendAsync(JsonRpcMessage message, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var json = JsonSerializer.Serialize(message);
            await _process!.StandardInput.WriteLineAsync(json + "\n");
            await _process.StandardInput.FlushAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }
}
```

### 14.3 实现 SSE MCP 客户端
创建 `src/tools/mcp/SseMcpClient.cs`:
```csharp
public class SseMcpClient : IMcpClient
{
    private readonly HttpClient _httpClient;
    private readonly string _url;
    
    public string Name { get; }
    public bool IsConnected { get; private set; }
    
    public SseMcpClient(string name, string url)
    {
        Name = name;
        _url = url;
        _httpClient = new HttpClient();
    }
    
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        // SSE 连接通常需要先发送 POST 请求建立连接
        var response = await _httpClient.PostAsync(_url, new StringContent(""), ct);
        response.EnsureSuccessStatusCode();
        IsConnected = true;
    }
    
    public async Task<McpCallResult> CallToolAsync(
        string name, 
        Dictionary<string, object> args, 
        CancellationToken ct = default)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString("N"),
            method = "tools/call",
            @params = new
            {
                name,
                arguments = args
            }
        };
        
        var response = await _httpClient.PostAsJsonAsync(_url, request, ct);
        var result = await response.Content.ReadFromJsonAsync<JsonRpcResponse>(cancellationToken: ct);
        
        return new McpCallResult
        {
            IsError = result.Error != null,
            Content = result.Result?.ToString() ?? result.Error?.Message ?? ""
        };
    }
}
```

### 14.4 实现 MCP 工具管理器
创建 `src/tools/mcp/McpToolManager.cs`:
```csharp
public class McpToolManager
{
    private readonly Dictionary<string, IMcpClient> _clients = new();
    private readonly ConfigOptions _config;
    
    public McpToolManager(ConfigOptions config)
    {
        _config = config;
    }
    
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_config.Mcp == null) return;
        
        foreach (var (name, server) in _config.Mcp)
        {
            if (!server.Enabled) continue;
            
            var client = CreateClient(name, server);
            await client.ConnectAsync(ct);
            await client.InitializeAsync(ct);
            
            _clients[name] = client;
        }
    }
    
    private IMcpClient CreateClient(string name, McpServerConfig config)
    {
        return config.Type.ToLower() switch
        {
            "local" or "stdio" => new StdioMcpClient(name, config.Command!, config.Args?.ToArray() ?? Array.Empty<string>()),
            "sse" => new SseMcpClient(name, config.Url!),
            _ => throw new NotSupportedException($"不支持的 MCP 类型: {config.Type}")
        };
    }
    
    public IEnumerable<AgentTool> GetAllTools()
    {
        foreach (var (name, client) in _clients)
        {
            foreach (var tool in client.GetTools())
            {
                yield return new AgentTool
                {
                    Name = $"{name}_{tool.Name}",
                    Description = tool.Description,
                    Parameters = tool.InputSchema
                };
            }
        }
    }
}
```

## 配置示例
```json
{
  "mcp": {
    "playwright": {
      "type": "local",
      "command": ["npx", "@playwright/mcp@latest"],
      "enabled": true
    }
  }
}
```

## 相关文件
- tasks/13-builtin-tools.md
- design/dotnet-coding-agent.md (4.2 MCP 工具)
