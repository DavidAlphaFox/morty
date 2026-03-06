# .NET Coding Agent 设计方案

## 概述

本文档描述了使用 .NET 和 Semantic Kernel 实现类似 pi (coding-agent) 的终端 AI 编程助手的完整设计方案。

### 设计目标

- **终端 UI**: CLI/TUI 交互界面
- **LLM 提供商**: 以 MiniMax 和智谱 (Zhipu) 为主
- **核心工具**: read, write, edit, bash, grep, find, ls
- **代码位置**: 独立仓库 (morty/design 中设计)

---

## 1. 项目架构

### 1.1 整体架构图

```
┌─────────────────────────────────────────────────────────────────────┐
│                          用户层                                       │
│                     CLI / TUI 交互界面                                │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        Agent 核心层                                   │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │  CodingAgent - 基于 Microsoft.AgentFramework IAgent             ││
│  │  - 消息管理 (会话历史)                                           ││
│  │  - 干预机制 (steer/followUp)                                    ││
│  │  - 上下文压缩                                                   ││
│  └─────────────────────────────────────────────────────────────────┘│
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        工具系统层                                     │
│  FileTools (read/write/edit) | SystemTools (bash/grep/find/ls)    │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                       LLM 提供商层                                    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐            │
│  │  Zhipu       │  │  MiniMax    │  │  Qianwen    │            │
│  │  (GLM)       │  │  Provider   │  │  (qwen3)    │            │
│  └──────────────┘  └──────────────┘  └──────────────┘            │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   Microsoft.AgentFramework                             │
│              (IAgent + MessagePipeline + TurnContext)               │
└─────────────────────────────────────────────────────────────────────┘
```
┌─────────────────────────────────────────────────────────────────────┐
│                   Microsoft.AgentFramework                             │
│              (IAgent + MessagePipeline + TurnContext)               │
└─────────────────────────────────────────────────────────────────────┘
```

### 1.2 项目结构

```
dotnet-coding-agent/
├── src/
│   ├── DotnetCodingAgent.sln
│   │
│   ├── cli/                          # CLI 入口
│   │   ├── Program.cs
│   │   ├── Commands/
│   │   │   ├── InteractiveCommand.cs
│   │   │   ├── ModelCommand.cs
│   │   │   ├── SessionCommand.cs
│   │   │   └── SettingsCommand.cs
│   │   └── Options/
│   │
│   ├── tui/                          # 终端 UI (Terminal.Gui)
│   │   ├── MortyApp.cs               # 主应用窗口
│   │   ├── Views/
│   │   │   ├── MessageListView.cs
│   │   │   ├── InputView.cs
│   │   │   └── StatusBarView.cs
│   │   └── Program.cs
│   │
│   ├── agent/                        # Agent 核心
│   │   ├── CodingAgent.cs           # 主 Agent 类
│   │   ├── AgentOptions.cs
│   │   ├── SessionManager.cs       # 会话管理
│   │   ├── ContextCompactor.cs     # 上下文压缩
│   │   └── Events/
│   │       └── AgentEvent.cs
│   │
│   ├── tools/                        # 工具实现
│   │   ├── ITool.cs
│   │   ├── ReadFileTool.cs
│   │   ├── WriteFileTool.cs
│   │   ├── EditFileTool.cs
│   │   ├── BashTool.cs
│   │   ├── GrepTool.cs
│   │   ├── FindTool.cs
│   │   └── ListDirectoryTool.cs
│   │
│   ├── llm/                          # LLM 提供商
│   │   ├── ILlmProvider.cs
│   │   ├── ZhipuProvider.cs
│   │   ├── MiniMaxProvider.cs
│   │   ├── QianwenProvider.cs
│   │   └── ProviderFactory.cs
│   │
│   └── config/                       # 配置管理
│       ├── ConfigLoader.cs
│       ├── ConfigOptions.cs
│       ├── ConfigValidator.cs
│       └── McpConfig.cs
│
│   └── auth/                          # 凭证管理
│       ├── AuthManager.cs
│       ├── AuthStore.cs
│       └── AuthCommands.cs
│
├── docs/
├── tests/
└── README.md
```
│
├── docs/
├── tests/
└── README.md
```

### 1.3 配置文件位置

```
~/.config/morty/morty.json    # 用户配置 (默认)
~/.morty/sessions/            # 会话存储
```
dotnet-coding-agent/
├── src/
│   ├── DotnetCodingAgent.sln
│   │
│   ├── cli/                          # CLI 入口
│   │   ├── Program.cs
│   │   ├── Commands/
│   │   │   ├── InteractiveCommand.cs
│   │   │   ├── ModelCommand.cs
│   │   │   ├── SessionCommand.cs
│   │   │   └── SettingsCommand.cs
│   │   └── Options/
│   │
│   ├── tui/                          # 终端 UI
│   │   ├── TuiEngine.cs
│   │   ├── Components/
│   │   │   ├── EditorComponent.cs
│   ├── MessageListComponent │   │  .cs
│   │   │   └── StatusBarComponent.cs
│   │   └── Rendering/
│   │       └── DiffRenderer.cs
│   │
│   ├── agent/                        # Agent 核心
│   │   ├── CodingAgent.cs           # 主 Agent 类
│   │   ├── AgentOptions.cs
│   │   ├── SessionManager.cs       # 会话管理
│   │   ├── ContextCompactor.cs     # 上下文压缩
│   │   └── Events/
│   │       └── AgentEvent.cs
│   │
│   ├── tools/                        # 工具实现
│   │   ├── ITool.cs
│   │   ├── ReadFileTool.cs
│   │   ├── WriteFileTool.cs
│   │   ├── EditFileTool.cs
│   │   ├── BashTool.cs
│   │   ├── GrepTool.cs
│   │   ├── FindTool.cs
│   │   └── ListDirectoryTool.cs
│   │
│   └── llm/                          # LLM 提供商
│       ├── ILlmProvider.cs
│       ├── ZhipuProvider.cs
│       ├── MiniMaxProvider.cs
│       ├── QianwenProvider.cs
│       └── ProviderFactory.cs
│
├── docs/
├── tests/
└── README.md
```

---

## 2. 技术选型

### 2.1 技术栈

| 层级 | 技术选型 | 说明 |
|------|---------|------|
| **Agent 框架** | Microsoft.AgentFramework | 基于 `IAgent` + `MessagePipeline` |
| **LLM 调用** | Microsoft.AgentFramework + 自定义 Provider | 支持 GLM/MiniMax/qwen3 |
| **TUI** | Terminal.Gui | 成熟 Linux TUI 框架 (10.8k stars) |
| **CLI** | System.CommandLine | 命令行解析 |
| **依赖注入** | Microsoft.Extensions.DependencyInjection | |
| **序列化** | System.Text.Json | |

### 2.2 依赖包

```xml
<!-- 核心依赖 -->
<PackageReference Include="Microsoft.AgentFramework" Version="0.1.x" />

<!-- MCP -->
<PackageReference Include="ModelContextProtocol" Version="0.1.x" />

<!-- TUI -->
<PackageReference Include="Terminal.Gui" Version="2.0.x" />

<!-- CLI -->
<PackageReference Include="System.CommandLine" Version="2.0.x" />

<!-- 依赖注入 -->
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.x" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.x" />

<!-- 日志 -->
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.x" />
<PackageReference Include="Serilog.Extensions.Hosting" Version="8.x" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.x" />
```

---

## 3. 核心模块设计

### 3.1 LLM 提供商 (重点)

支持三个主要的 LLM 提供商：智谱 (GLM)、MiniMax、百炼 (千问3)。

#### 3.1.1 Zhipu (智谱) Provider

```csharp
/// <summary>
/// 智谱 LLM 提供商
/// 
/// API 特点:
/// - baseUrl: https://open.bigmodel.cn/api/paas/v4
/// - 需要 API Key 认证
/// - 支持流式输出
/// - GLM-4/GLM-4-Vision 支持工具调用
/// </summary>
public class ZhipuProvider : ILlmProvider
{
    // API 端点
    // - Chat: POST https://open.bigmodel.cn/api/paas/v4/chat/completions
    // - Models: GET https://open.bigmodel.cn/api/paas/v4/models
    
    // 认证方式
    // Header: Authorization: Bearer <api_key>
    
    // 工具调用
    // 使用 function_call 格式
    
    // 支持模型
    // - glm-4
    // - glm-4-flash
    // - glm-4-plus
    // - glm-4v-plus (视觉)
}
```

#### 3.1.2 MiniMax Provider

```csharp
/// <summary>
/// MiniMax LLM 提供商
/// 
/// API 特点:
/// - 使用 OpenAI 兼容格式
/// - baseUrl: https://api.minimax.io/v1
/// - 需要 API 签名认证 (HMAC-SHA256)
/// - 支持流式输出 (Server-Sent Events)
/// </summary>
public class MiniMaxProvider : ILlmProvider
{
    // 核心实现思路:
    // 1. 使用 OpenAI 兼容的 ChatCompletions 格式
    // 2. 添加 MiniMax 特有的 API 签名认证
    // 3. 处理流式响应 (text/event-stream)
    // 4. 适配工具调用格式
    
    // API 端点
    // - Chat: POST https://api.minimax.io/v1/chat/completions
    // - Embeddings: POST https://api.minimax.io/v1/embeddings
    // - Models: GET https://api.minimax.io/v1/models
    
    // 认证方式
    // Header: Authorization: Bearer <签名>
    // Header: X-Minimax-Api-Version: 2024-05-01
    
    // 支持模型
    // - MiniMax-M2
    // - MiniMax-M2.1
    // - MiniMax-Text-01
}
```

**需要处理的问题:**
- API 签名计算 (HMAC-SHA256)
- 流式响应解析
- 工具调用格式适配

#### 3.1.3 Qianwen (百炼/千问) Provider

```csharp
/// <summary>
/// 百炼千问 LLM 提供商
/// 
/// API 特点:
/// - baseUrl: https://dashscope.aliyuncs.com/compatible-mode/v1
/// - 需要 API Key 认证
/// - 支持流式输出
/// - qwen3 支持工具调用 (function call)
/// </summary>
public class QianwenProvider : ILlmProvider
{
    // API 端点
    // - Chat: POST https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions
    // - Models: GET https://dashscope.aliyuncs.com/api/v1/models
    
    // 认证方式
    // Header: Authorization: Bearer <api_key>
    
    // 工具调用
    // 使用 function_call 格式
    
    // 支持模型
    // - qwen-turbo
    // - qwen-plus
    // - qwen-max
    // - qwen-long
    // - qwen2.5 系列
    // - qwen2.5-vl 系列 (视觉)
}
```

#### 3.1.4 Provider 工厂

```csharp
/// <summary>
/// LLM Provider 工厂
/// </summary>
public class ProviderFactory
{
    public static ILlmProvider Create(string providerName, string apiKey, string? baseUrl = null)
    {
        return providerName.ToLower() switch
        {
            "zhipu" or "glm" => new ZhipuProvider(apiKey, baseUrl),
            "minimax" => new MiniMaxProvider(apiKey, baseUrl),
            "qianwen" or "qwen" or "百炼" => new QianwenProvider(apiKey, baseUrl),
            _ => throw new NotSupportedException($"不支持的 LLM 提供商: {providerName}")
        };
    }
}
```

### 3.2 Agent 核心

```csharp
/// <summary>
/// Coding Agent 主类
/// 
/// 基于 Microsoft.AgentFramework 的 IAgent 构建，
/// 添加了 coding agent 特有的功能:
/// - 会话管理
/// - 干预机制
/// - 上下文压缩
/// - 工具执行
/// </summary>
public class CodingAgent
{
    private readonly IAgent _agent;
    private readonly MessagePipeline _pipeline;
    private readonly SessionManager _sessionManager;
    private readonly ContextCompactor _compactor;
    private readonly List<AgentTool> _tools;
    
    // 消息队列
    private readonly ConcurrentQueue<ChatMessageContent> _steeringQueue;
    private readonly ConcurrentQueue<ChatMessageContent> _followUpQueue;
    
    /// <summary>
    /// 发送提示消息
    /// </summary>
    public async Task PromptAsync(string message, CancellationToken ct = default);
    
    /// <summary>
    /// 发送提示消息 (带图片)
    /// </summary>
    public async Task PromptAsync(string message, byte[][] images, CancellationToken ct = default);
    
    /// <summary>
    /// 干预消息 - 在当前工具执行完成后送达
    /// </summary>
    public void Steer(string message);
    
    /// <summary>
    /// 跟进消息 - 在 Agent 完成后送达
    /// </summary>
    public void FollowUp(string message);
    
    /// <summary>
    /// 中止当前运行
    /// </summary>
    public void Abort();
    
    /// <summary>
    /// 订阅事件
    /// </summary>
    public IDisposable Subscribe(EventHandler<AgentEventArgs> handler);
    
    /// <summary>
    /// 继续执行 (处理队列消息)
    /// </summary>
    public async Task ContinueAsync(CancellationToken ct = default);
}
```

### 3.3 会话管理

```csharp
/// <summary>
/// 会话管理器
/// 
/// 功能:
/// - 会话存储 (JSONL 格式，与 pi 兼容)
/// - 会话分支
/// - 上下文压缩
/// </summary>
public class SessionManager
{
    // 会话文件格式 (与 pi 兼容)
    // {
    //   "id": "msg_xxx",
    //   "parentId": "msg_yyy",
    //   "role": "user|assistant|tool",
    //   "content": "...",
    //   "timestamp": 1234567890,
    //   "model": "...",
    //   "usage": { ... },
    //   "toolCalls": [...],
    //   "toolResults": [...]
    // }
    
    /// <summary>
    /// 创建新会话
    /// </summary>
    public Task<Session> CreateSessionAsync(string workingDirectory);
    
    /// <summary>
    /// 保存消息到会话
    /// </summary>
    public Task SaveMessageAsync(Session session, ChatMessageContent message);
    
    /// <summary>
    /// 加载会话历史
    /// </summary>
    public Task<List<ChatMessageContent>> LoadHistoryAsync(Session session);
    
    /// <summary>
    /// 创建分支
    /// </summary>
    public Task<Session> ForkAsync(Session session, string fromMessageId);
    
    /// <summary>
    /// 压缩上下文
    /// </summary>
    public Task CompressAsync(Session session, string? instructions = null);
}
```

### 3.4 上下文压缩

```csharp
### 3.3 上下文压缩

```csharp
/// <summary>
/// 上下文压缩器
/// 
/// 当上下文接近 token 限制时:
/// 1. 保留最近的消息和工具结果
/// 2. 使用 LLM 总结旧消息
/// 3. 保留关键信息 (文件修改、决策等)
/// </summary>
public class ContextCompactor
{
    /// <summary>
    /// 执行上下文压缩
    /// </summary>
    public async Task<MessageList> CompressAsync(
        MessageList messages,
        int maxTokens,
        IAgent agent,
        CancellationToken ct = default);
    
    /// <summary>
    /// 估算 token 数量
    /// </summary>
    public int EstimateTokens(Message message);
    
    /// <summary>
    /// 生成压缩提示
    /// </summary>
    public string BuildCompressionPrompt(MessageList oldMessages);
}
```

---

## 4. 工具系统

### 4.1 内置工具

使用 Microsoft.AgentFramework 的 `AgentTool` 特性定义工具：

```csharp
/// <summary>
/// 文件操作工具集
/// </summary>
public class FileTools
{
    private readonly string _workingDirectory;
    
    [AgentTool]
    [Description("读取文件内容")]
    public async Task<string> Read(
        [Description("文件路径")] string path)
    {
        // 安全检查：确保路径在工作目录内
        var fullPath = Path.GetFullPath(Path.Combine(_workingDirectory, path));
        if (!fullPath.StartsWith(_workingDirectory))
            throw new UnauthorizedAccessException("路径不在工作目录内");
        
        return await File.ReadAllTextAsync(fullPath);
    }
    
    [AgentTool]
    [Description("写入文件内容")]
    public async Task<string> Write(
        [Description("文件路径")] string path,
        [Description("文件内容")] string content)
    {
        // 安全检查
        var fullPath = Path.GetFullPath(Path.Combine(_workingDirectory, path));
        if (!fullPath.StartsWith(_workingDirectory))
            throw new UnauthorizedAccessException("路径不在工作目录内");
        
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        
        await File.WriteAllTextAsync(fullPath, content);
        return $"已写入文件: {path}";
    }
    
    [AgentTool]
    [Description("编辑文件内容")]
    public async Task<string> Edit(
        [Description("文件路径")] string path,
        [Description("需要替换的原文")] string oldString,
        [Description("替换后的内容")] string newString)
    {
        var content = await Read(path);
        
        // 简单替换（可以改进为更智能的 diff）
        if (!content.Contains(oldString))
            throw new InvalidOperationException("未找到需要替换的内容");
        
        content = content.Replace(oldString, newString);
        await Write(path, content);
        return $"已编辑文件: {path}";
    }
}

/// <summary>
/// 系统工具集
/// </summary>
public class SystemTools
{
    private readonly string _workingDirectory;
    
    [AgentTool]
    [Description("执行 bash 命令")]
    public async Task<string> Bash(
        [Description("要执行的 bash 命令")] string command)
    {
        // 安全检查：限制可执行的命令
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };
        
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        return string.IsNullOrEmpty(error) ? output : $"输出:\n{output}\n错误:\n{error}";
    }
    
    [AgentTool]
    [Description("搜索文件内容")]
    public async Task<string> Grep(
        [Description("正则表达式模式")] string pattern,
        [Description("搜索路径，默认当前目录")] string? path = null)
    {
        // 使用 grep 命令
        var searchPath = path ?? _workingDirectory;
        var result = await Bash($"grep -rn '{pattern}' {searchPath}");
        return result;
    }
    
    [AgentTool]
    [Description("查找文件")]
    public async Task<string> Find(
        [Description("文件名模式，支持 * 和 ?")] string pattern,
        [Description("搜索路径，默认当前目录")] string? path = null)
    {
        var searchPath = path ?? _workingDirectory;
        var result = await Bash($"find {searchPath} -name '{pattern}'");
        return result;
    }
    
    [AgentTool]
    [Description("列出目录内容")]
    public async Task<string> Ls(
        [Description("目录路径，默认当前目录")] string? path = null)
    {
        var targetPath = path ?? _workingDirectory;
        var result = await Bash($"ls -la {targetPath}");
        return result;
    }
}
```

### 4.2 MCP 工具

支持通过 MCP (Model Context Protocol) 扩展工具集，与 opencode 配置格式兼容。

```csharp
/// <summary>
/// MCP 工具管理器
/// </summary>
public class McpToolManager
{
    private readonly Dictionary<string, IMcpClient> _clients = new();
    
    /// <summary>
    /// 初始化 MCP 客户端
    /// </summary>
    public async Task InitializeAsync(McpConfig config, CancellationToken ct = default)
    {
        foreach (var (name, server) in config.Servers)
        {
            if (!server.Enabled) continue;
            
            var client = server.Type switch
            {
                "local" or "stdio" => new StdioMcpClient(server.Command, server.Args),
                "sse" => new SseMcpClient(server.Url),
                _ => throw new NotSupportedException($"不支持的 MCP 类型: {server.Type}")
            };
            
            await client.ConnectAsync(ct);
            _clients[name] = client;
        }
    }
    
    /// <summary>
    /// 获取所有 MCP 工具
    /// </summary>
    public IEnumerable<AgentTool> GetTools()
    {
        foreach (var (name, client) in _clients)
        {
            foreach (var tool in client.Tools)
            {
                yield return tool.WithName($"{name}_{tool.Name}");
            }
        }
    }
    
    /// <summary>
    /// 调用 MCP 工具
    /// </summary>
    public async Task<string> InvokeAsync(string toolName, Dictionary<string, object> args, CancellationToken ct = default);
}

/// <summary>
/// MCP 配置
/// </summary>
public class McpConfig
{
    public Dictionary<string, McpServerConfig> Servers { get; set; } = new();
}

public class McpServerConfig
{
    public string Type { get; set; } = "stdio";
    public string? Command { get; set; }
    public string[]? Args { get; set; }
    public string? Url { get; set; }
    public bool Enabled { get; set; } = true;
}
```

**MCP 配置示例**:
```json
{
  "mcp": {
    "playwright": {
      "type": "local",
      "command": ["npx", "@playwright/mcp@latest"],
      "enabled": true
    },
    "filesystem": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/home/david/workspace"]
    }
  }
}
```

---

## 5. TUI 设计

使用 Terminal.Gui 框架，它是 .NET 中成熟的跨平台 TUI 库。

### 5.1 应用架构

```csharp
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

public class MortyApp : Window
{
    private readonly MessageListView _messageList;
    private readonly InputView _inputView;
    private readonly StatusBarView _statusBar;
    
    public MortyApp()
    {
        Title = "morty - AI Coding Assistant";
        
        _messageList = new MessageListView();
        _inputView = new InputView();
        _statusBar = new StatusBarView();
        
        Add(_messageList);
        Add(_inputView);
        Add(_statusBar);
    }
}

public class Program
{
    public static void Main()
    {
        using var app = Application.Create();
        app.Init();
        
        using var window = new MortyApp();
        app.Run(window);
    }
}
```

### 5.2 组件设计

基于 Terminal.Gui 的视图组件：

```csharp
/// <summary>
/// 消息列表视图
/// </summary>
public class MessageListView : View
{
    private readonly List<Message> _messages = new();
    
    public void AddMessage(Message message);
    public void Clear();
    
    public override void Redraw(NormalizeCollection<View> bounds);
}

/// <summary>
/// 输入视图
/// </summary>
public class InputView : View
{
    public string Text { get; set; }
    public event EventHandler<string>? OnSubmit;
    public event EventHandler? OnCancel;
    
    public override bool ProcessKey(KeyEvent keyEvent);
}

/// <summary>
/// 状态栏视图
/// </summary>
public class StatusBarView : View
{
    public string WorkingDirectory { get; set; }
    public string SessionName { get; set; }
    public string Model { get; set; }
    public int TokenUsage { get; set; }
    public decimal Cost { get; set; }
}
```

### 5.3 Terminal.Gui 特性 (Linux 优先)

- **平台**: 重点支持 Linux (macOS 可选，暂不支持 Windows)
- **丰富组件**: Label, Button, TextField, TextView, ListView, Table, TreeView 等
- **布局系统**: 绝对定位 + 相对定位 (Pos, Dim)
- **键盘处理**: 完整的按键事件处理
- **主题支持**: 自定义颜色和样式
- **TrueColor**: 24 位颜色支持

---

## 6. CLI 命令设计

### 6.1 命令列表

| 命令 | 说明 |
|------|------|
| `morty` | 交互模式 |
| `morty "prompt"` | 单次对话 |
| `morty -p "prompt"` | 打印模式 (非交互) |
| `morty --model <name>` | 切换模型 |
| `morty -c` | 继续会话 |
| `morty -r` | 恢复会话 |
| `morty --session <id>` | 指定会话 |
| `morty auth login <provider>` | 登录 LLM 提供商 |
| `morty auth list` | 列出已登录的提供商 |
| `morty auth logout <provider>` | 登出提供商 |

### 6.2 交互模式命令

| 命令 | 说明 |
|------|------|
| `/model` | 切换模型 |
| `/auth` | 管理认证 |
| `/settings` | 设置 |
| `/new` | 新建会话 |
| `/resume` | 恢复会话 |
| `/compact` | 压缩上下文 |
| `/copy` | 复制回复 |
| `/export` | 导出会话 |
| `/quit` | 退出 |

---

## 7. 实现计划

### Phase 1: 基础设施 (1 周)
- [ ] 创建项目结构
- [ ] 配置依赖
- [ ] 实现日志系统

### Phase 2: LLM 提供商 (1 周)
- [ ] 实现 MiniMax Provider
- [ ] 实现 Zhipu Provider
- [ ] 集成 Semantic Kernel

### Phase 3: Agent 核心 (1 周)
- [ ] 实现 CodingAgent 类
- [ ] 实现会话管理 (JSONL)
- [ ] 实现上下文压缩

### Phase 4: 工具系统 (1 周)
- [ ] 实现 read/write/edit 工具
- [ ] 实现 bash/grep/find/ls 工具
- [ ] 添加工具参数验证

### Phase 5: TUI (1 周)
- [ ] 实现渲染引擎
- [ ] 实现消息列表组件
- [ ] 实现编辑器组件

### Phase 6: CLI (1 周)
- [ ] 实现交互模式
- [ ] 实现命令
- [ ] 实现快捷键

---

## 8. 与 pi-mono 的对比

| 模块 | pi-mono (TypeScript) | dotnet-coding-agent |
|------|---------------------|-------------------|
| **Agent 框架** | 自实现 | Microsoft.AgentFramework |
| **LLM 调用** | 自实现 20+ | MAF + 自定义 3 (GLM/MiniMax/qwen3) |
| **TUI** | 自实现 (差异化渲染) | Terminal.Gui |
| **工具系统** | TypeBox + 自定义 | AgentTool |
| **会话格式** | JSONL | JSONL (兼容) |

---

## 9. 关键挑战

1. **Microsoft.AgentFramework 成熟度**
   - 框架相对较新，API 可能变化
   - 文档和社区资源有限

2. **MiniMax/智谱 API 兼容性**
   - 需要适配其工具调用格式
   - API 签名认证 (MiniMax)

3. **流式输出与 TUI**
   - 需要正确处理 Server-Sent Events
   - 差异化渲染避免闪烁

4. **上下文压缩**
   - 中文总结需要合适的提示词
   - 保留关键信息 (文件修改、决策)

5. **终端兼容性**
   - ANSI 转义序列在不同 Linux 终端的兼容性
   - 终端类型检测 (vt100, xterm, etc.)

---

## 10. 配置文件

配置文件位于 `~/.config/morty/morty.json`，仿照 opencode 的配置方式。

**Key 存储方式**：参考 opencode，使用独立文件存储 API Key，不在配置文件中明文存储。

- 凭证文件：`~/.local/share/morty/auth.json`
- 登录命令：`morty auth login <provider>`

### A. 完整配置示例

```json
{
  "$schema": "https://morty.dev/config.json",
  "provider": {
    "type": "zhipu",
    "model": "glm-4-plus"
  },
  "tools": {
    "enabled": ["read", "write", "edit", "bash", "grep", "find", "ls"],
    "bash": {
      "allowedCommands": ["git", "npm", "dotnet", "cargo", "pnpm", "yarn"],
      "timeout": 300
    }
  },
  "mcp": {
    "playwright": {
      "type": "local",
      "command": ["npx", "@playwright/mcp@latest"],
      "enabled": true
    },
    "filesystem": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/home/david/workspace"]
    }
  },
  "session": {
    "dir": "~/.morty/sessions",
    "autoCompact": true,
    "compactThreshold": 0.8
  },
  "tui": {
    "theme": "dark",
    "syntaxHighlighting": true
  }
}
```

### A.1 凭证存储

凭证单独存储在 `~/.local/share/morty/auth.json`，参考 opencode：

```json
{
  "zhipu": {
    "type": "api",
    "key": "your_api_key_here"
  },
  "minimax": {
    "type": "api", 
    "key": "your_api_key_here"
  },
  "qianwen": {
    "type": "api",
    "key": "your_api_key_here"
  }
}
```

**管理命令**:
```bash
morty auth login zhipu      # 登录智谱
morty auth login minimax   # 登录 MiniMax
morty auth login qianwen   # 登录百炼
morty auth list            # 列出已登录的 provider
morty auth logout zhipu    # 登出
```

### B. 配置项说明

| 配置项 | 类型 | 说明 | 默认值 |
|--------|------|------|--------|
| `provider.type` | string | LLM 提供商: `zhipu`, `minimax`, `qianwen` | `zhipu` |
| `provider.model` | string | 模型名称 | `glm-4-plus` |
| `provider.baseUrl` | string | 自定义 API 地址 | - |
| `tools.enabled` | string[] | 启用的工具 | 全部 |
| `tools.bash.allowedCommands` | string[] | 允许的 bash 命令 | 全部 |
| `tools.bash.timeout` | number | 命令超时(秒) | 300 |
| `mcp.<name>.type` | string | MCP 类型: `local`, `stdio`, `sse` | - |
| `mcp.<name>.command` | string | 执行命令 | - |
| `mcp.<name>.args` | string[] | 命令参数 | - |
| `mcp.<name>.enabled` | bool | 是否启用 | `true` |
| `session.dir` | string | 会话存储目录 | `~/.morty/sessions` |
| `session.autoCompact` | bool | 自动压缩上下文 | `true` |
| `session.compactThreshold` | number | 压缩阈值 (0-1) | `0.8` |
| `tui.theme` | string | 主题: `dark`, `light` | `dark` |
| `tui.syntaxHighlighting` | bool | 语法高亮 | `true` |

**注意**: API Key 不在配置文件中指定，通过 `morty auth login` 命令登录后存储在 `~/.local/share/morty/auth.json`

### C. 环境变量

```bash
# 配置文件位置
export MORTY_CONFIG_DIR="~/.config/morty"
```

### D. 配置文件优先级

1. `~/.config/morty/morty.json` (默认)
2. 环境变量 `MORTY_CONFIG_DIR/morty.json`
3. 项目根目录 `.morty.json`

### E. 凭证文件位置

- 凭证存储: `~/.local/share/morty/auth.json`
- 登录状态查询: `morty auth list`

支持的模型:
- 智谱: `glm-4`, `glm-4-flash`, `glm-4-plus`, `glm-4v-plus`
- MiniMax: `MiniMax-M2`, `MiniMax-M2.1`
- 百炼: `qwen-turbo`, `qwen-plus`, `qwen-max`, `qwen-long`, `qwen2.5-vl`

---

## 11. 凭证管理系统

### 11.1 设计思路

参考 opencode 的凭证管理方式：
1. API Key 不存储在配置文件中
2. 使用 `morty auth login` 命令交互式输入 Key
3. 凭证安全存储在 `~/.local/share/morty/auth.json`

### 11.2 凭证管理命令

```bash
# 登录 (交互式输入 API Key)
morty auth login zhipu
morty auth login minimax
morty auth login qianwen

# 查看已登录的提供商
morty auth list

# 登出
morty auth logout zhipu
```

### 11.3 核心实现

```csharp
/// <summary>
/// 凭证管理器
/// </summary>
public class AuthManager
{
    private readonly string _authFilePath;
    
    public AuthManager()
    {
        _authFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "morty", "auth.json");
    }
    
    /// <summary>
    /// 登录提供商
    /// </summary>
    public async Task LoginAsync(string provider, string apiKey);
    
    /// <summary>
    /// 获取 API Key
    /// </summary>
    public string? GetApiKey(string provider);
    
    /// <summary>
    /// 列出所有已登录的提供商
    /// </summary>
    public IEnumerable<string> ListProviders();
    
    /// <summary>
    /// 登出提供商
    /// </summary>
    public Task LogoutAsync(string provider);
}
```

### 11.4 凭证文件格式

`~/.local/share/morty/auth.json`:
```json
{
  "zhipu": {
    "type": "api",
    "key": "your_api_key"
  },
  "minimax": {
    "type": "api",
    "key": "your_api_key"
  },
  "qianwen": {
    "type": "api",
    "key": "your_api_key"
  }
}
```

