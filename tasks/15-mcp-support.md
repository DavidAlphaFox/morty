# 任务 3.4: MCP 协议支持

## 阶段
Phase 3 — 高级特性

## 目标
实现 Model Context Protocol 客户端，支持外部 MCP Server 提供工具、资源和 prompt。

## 背景
MCP 是标准化的协议，允许 AI agent 连接外部工具服务器。morty 配置中已预留 `mcp` 字段但未实现。

## 设计方案

### 1. MCP 客户端

```csharp
// src/tools/McpClient.cs

public class McpClient : IDisposable
{
    private Process? _serverProcess;
    private readonly JsonRpcConnection _connection;

    /// <summary>通过 stdio 连接 MCP Server</summary>
    public async Task ConnectStdioAsync(string command, string[] args) { ... }

    /// <summary>通过 SSE 连接 MCP Server</summary>
    public async Task ConnectSseAsync(string url) { ... }

    /// <summary>列出服务器提供的工具</summary>
    public async Task<List<McpToolDefinition>> ListToolsAsync() { ... }

    /// <summary>调用工具</summary>
    public async Task<string> CallToolAsync(string name, Dictionary<string, object?> args) { ... }

    /// <summary>列出资源</summary>
    public async Task<List<McpResource>> ListResourcesAsync() { ... }
}
```

### 2. MCP 工具桥接

```csharp
// src/agent/McpToolBridge.cs

/// <summary>将 MCP Server 的工具转为 AIFunction</summary>
public static List<AIFunction> BridgeTools(McpClient client, List<McpToolDefinition> tools)
{
    return tools.Select(tool => AIFunctionFactory.Create(
        (Dictionary<string, object?> args) => client.CallToolAsync(tool.Name, args),
        $"mcp_{tool.Name}",
        tool.Description
    )).ToList();
}
```

### 3. 集成到 Agent 创建流程

```csharp
// Program.cs CreateAgent 中
if (config.Mcp != null)
{
    foreach (var (name, serverConfig) in config.Mcp)
    {
        if (!serverConfig.Enabled) continue;
        var mcpClient = new McpClient();
        if (serverConfig.Type == "stdio")
            await mcpClient.ConnectStdioAsync(serverConfig.Command!, serverConfig.Args?.ToArray());
        else
            await mcpClient.ConnectSseAsync(serverConfig.Url!);

        var mcpTools = await mcpClient.ListToolsAsync();
        var bridged = McpToolBridge.BridgeTools(mcpClient, mcpTools);
        foreach (var tool in bridged) agent.RegisterTool(tool);
    }
}
```

## 实现步骤

1. [ ] 创建 `src/tools/McpClient.cs` — JSON-RPC 客户端 (stdio + SSE)
2. [ ] 创建 `src/agent/McpToolBridge.cs` — MCP→AIFunction 桥接
3. [ ] `Program.cs` — Agent 创建时连接 MCP Server
4. [ ] 实现 MCP tools/list 和 tools/call
5. [ ] 实现 MCP resources/list 和 resources/read (可选)
6. [ ] MCP Server 生命周期管理

## 验收标准
- [ ] 可通过配置连接 stdio MCP Server
- [ ] MCP Server 工具在 Agent 中可用
- [ ] Server 异常时优雅降级

## 参考
- opencode: `src/mcp/`
- MCP 规范: https://spec.modelcontextprotocol.io/

## 相关文件
- `src/tools/McpClient.cs` — 新建
- `src/agent/McpToolBridge.cs` — 新建
- `src/cli/Program.cs` — 集成
- `src/config/ConfigOptions.cs` — 已有 McpServerConfig
