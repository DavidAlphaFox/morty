# 任务 3.1: LSP 集成

## 阶段
Phase 3 — 高级特性

## 目标
新增 `lsp` 工具，通过 Language Server Protocol 提供代码智能：跳转定义、查找引用、悬浮信息等。

## 背景
Agent 目前依赖 grep/find 来理解代码结构，不如 LSP 精确。opencode 集成了 LSP 支持多种操作。

## 设计方案

### 1. LSP 客户端

```csharp
// src/tools/LspClient.cs

public class LspClient : IDisposable
{
    private Process? _serverProcess;
    private readonly JsonRpcConnection _connection;

    /// <summary>启动 LSP Server</summary>
    public async Task InitializeAsync(string command, string[] args, string rootPath)
    {
        _serverProcess = new Process { ... };
        _serverProcess.Start();
        _connection = new JsonRpcConnection(_serverProcess.StandardInput, _serverProcess.StandardOutput);
        await _connection.SendRequestAsync("initialize", new { rootUri = $"file://{rootPath}" });
        await _connection.SendNotificationAsync("initialized", new { });
    }

    public async Task<Location[]> GoToDefinitionAsync(string file, int line, int character) { ... }
    public async Task<Location[]> FindReferencesAsync(string file, int line, int character) { ... }
    public async Task<Hover> HoverAsync(string file, int line, int character) { ... }
    public async Task<DocumentSymbol[]> DocumentSymbolAsync(string file) { ... }
}
```

### 2. LSP 工具

```csharp
// ToolRegistry.cs
tools.Add(AIFunctionFactory.Create(
    ([Description("Operation: goToDefinition, findReferences, hover, documentSymbol")] string operation,
     [Description("File path")] string file,
     [Description("Line number (1-indexed)")] int? line,
     [Description("Column number (1-indexed)")] int? character) =>
        lspTools.ExecuteAsync(operation, file, line, character),
    "lsp",
    "Perform code intelligence operations via Language Server Protocol."));
```

### 3. 自动检测语言和 LSP Server

```csharp
public static (string command, string[] args)? DetectLspServer(string cwd)
{
    // C# → OmniSharp
    if (Directory.GetFiles(cwd, "*.csproj").Any())
        return ("omnisharp", new[] { "-lsp" });
    // TypeScript → typescript-language-server
    if (File.Exists(Path.Combine(cwd, "tsconfig.json")))
        return ("typescript-language-server", new[] { "--stdio" });
    // Python → pyright
    if (File.Exists(Path.Combine(cwd, "pyproject.toml")))
        return ("pyright-langserver", new[] { "--stdio" });
    return null;
}
```

## 实现步骤

1. [ ] 创建 `src/tools/LspClient.cs` — LSP 协议客户端 (JSON-RPC over stdio)
2. [ ] 创建 `src/tools/LspTools.cs` — LSP 工具封装
3. [ ] 实现 goToDefinition, findReferences, hover, documentSymbol
4. [ ] 自动检测项目语言和 LSP Server
5. [ ] `ToolRegistry.cs` — 注册 lsp 工具
6. [ ] LSP Server 生命周期管理 (启动/关闭)

## 验收标准
- [ ] C# 项目可使用 OmniSharp 提供代码智能
- [ ] goToDefinition 返回准确位置
- [ ] findReferences 返回所有引用
- [ ] LSP Server 随 Agent 关闭而清理

## 参考
- opencode: `src/lsp/`, `src/tool/lsp.ts`

## 相关文件
- `src/tools/LspClient.cs` — 新建
- `src/tools/LspTools.cs` — 新建
- `src/agent/ToolRegistry.cs` — 注册
