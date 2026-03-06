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
│  │  CodingAgent - 基于 Semantic Kernel ChatCompletionAgent         ││
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
│  │  MiniMax     │  │   Zhipu     │  │   Others    │            │
│  │  Provider    │  │   Provider  │  │   (SK)     │            │
│  └──────────────┘  └──────────────┘  └──────────────┘            │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      Semantic Kernel                                  │
│              (ChatCompletionAgent + KernelFunction)                 │
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
│       ├── MiniMaxProvider.cs
│       ├── ZhipuProvider.cs
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
| **Agent 框架** | Semantic Kernel | `ChatCompletionAgent` |
| **LLM 调用** | Semantic Kernel + 自定义 Provider | 支持 MiniMax/智谱 |
| **TUI** | Spectre.Console + 自定义渲染 | 差异化渲染 |
| **CLI** | System.CommandLine 或 Spectre.Console | 命令行解析 |
| **依赖注入** | Microsoft.Extensions.DependencyInjection | |
| **序列化** | System.Text.Json | |

### 2.2 依赖包

```xml
<!-- 核心依赖 -->
<PackageReference Include="Microsoft.SemanticKernel" Version="1.x" />
<PackageReference Include="Microsoft.SemanticKernel.Agents.Core" Version="1.x" />

<!-- TUI -->
<PackageReference Include="Spectre.Console" Version="0.49.x" />

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

由于 Semantic Kernel 默认不支持 MiniMax 和智谱，需要实现自定义 Provider。

#### 3.1.1 MiniMax Provider

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
}
```

**需要处理的问题:**
- API 签名计算 (HMAC-SHA256)
- 流式响应解析
- 工具调用格式适配

#### 3.1.2 Zhipu (智谱) Provider

```csharp
/// <summary>
/// 智谱 LLM 提供商
/// 
/// API 特点:
/// - baseUrl: https://open.bigmodel.cn/api/paas/v4
/// - 需要 API Key 认证
/// - 支持流式输出
/// - GLM-4 支持工具调用
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
}
```

### 3.2 Agent 核心

```csharp
/// <summary>
/// Coding Agent 主类
/// 
/// 基于 Semantic Kernel 的 ChatCompletionAgent 构建，
/// 添加了 coding agent 特有的功能:
/// - 会话管理
/// - 干预机制
/// - 上下文压缩
/// - 工具执行
/// </summary>
public class CodingAgent
{
    private readonly ChatCompletionAgent _agent;
    private readonly Kernel _kernel;
    private readonly SessionManager _sessionManager;
    private readonly ContextCompactor _compactor;
    private readonly List<KernelFunction> _tools;
    
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
    public async Task<ChatHistory> CompressAsync(
        ChatHistory history,
        int maxTokens,
        Kernel kernel,
        CancellationToken ct = default);
    
    /// <summary>
    /// 估算 token 数量
    /// </summary>
    public int EstimateTokens(ChatMessageContent message);
    
    /// <summary>
    /// 生成压缩提示
    /// </summary>
    public string BuildCompressionPrompt(ChatHistory oldMessages);
}
```

---

## 4. 工具系统

### 4.1 工具定义

使用 Semantic Kernel 的 `KernelFunction` 属性定义工具：

```csharp
/// <summary>
/// 文件操作工具集
/// </summary>
public class FileTools
{
    private readonly string _workingDirectory;
    
    [KernelFunction]
    [Description("读取文件内容")]
    [ParameterDescription("path", "文件的完整路径")]
    public async Task<string> Read(
        [Description("文件路径")] string path)
    {
        // 安全检查：确保路径在工作目录内
        var fullPath = Path.GetFullPath(Path.Combine(_workingDirectory, path));
        if (!fullPath.StartsWith(_workingDirectory))
            throw new UnauthorizedAccessException("路径不在工作目录内");
        
        return await File.ReadAllTextAsync(fullPath);
    }
    
    [KernelFunction]
    [Description("写入文件内容")]
    [ParameterDescription("path", "文件路径")]
    [ParameterDescription("content", "文件内容")]
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
    
    [KernelFunction]
    [Description("编辑文件内容")]
    [ParameterDescription("path", "文件路径")]
    [ParameterDescription("oldString", "需要替换的原文")]
    [ParameterDescription("newString", "替换后的内容")]
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
    
    [KernelFunction]
    [Description("执行 bash 命令")]
    [ParameterDescription("command", "要执行的命令")]
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
    
    [KernelFunction]
    [Description("搜索文件内容")]
    [ParameterDescription("pattern", "正则表达式模式")]
    [ParameterDescription("path", "搜索路径 (可选)")]
    public async Task<string> Grep(
        [Description("正则表达式模式")] string pattern,
        [Description("搜索路径，默认当前目录")] string? path = null)
    {
        // 使用 grep 命令
        var searchPath = path ?? _workingDirectory;
        var result = await Bash($"grep -rn '{pattern}' {searchPath}");
        return result;
    }
    
    [KernelFunction]
    [Description("查找文件")]
    [ParameterDescription("pattern", "文件名模式")]
    [ParameterDescription("path", "搜索路径 (可选)")]
    public async Task<string> Find(
        [Description("文件名模式，支持 * 和 ?")] string pattern,
        [Description("搜索路径，默认当前目录")] string? path = null)
    {
        var searchPath = path ?? _workingDirectory;
        var result = await Bash($"find {searchPath} -name '{pattern}'");
        return result;
    }
    
    [KernelFunction]
    [Description("列出目录内容")]
    [ParameterDescription("path", "目录路径 (可选)")]
    public async Task<string> Ls(
        [Description("目录路径，默认当前目录")] string? path = null)
    {
        var targetPath = path ?? _workingDirectory;
        var result = await Bash($"ls -la {targetPath}");
        return result;
    }
}
```

---

## 5. TUI 设计

### 5.1 终端渲染引擎

```csharp
/// <summary>
/// 差异化 TUI 渲染引擎
/// 
/// 核心思路:
/// 1. 记录上一帧的输出
/// 2. 计算当前帧与上一帧的差异
/// 3. 只更新变化的行 (使用 ANSI 转义序列)
/// </summary>
public class TuiEngine
{
    private readonly List<string> _previousFrame;
    private readonly int _terminalHeight;
    private readonly int _terminalWidth;
    
    /// <summary>
    /// 渲染帧
    /// </summary>
    public void Render(Frame frame);
    
    /// <summary>
    /// 计算差异并输出
    /// </summary>
    private void RenderDiff(List<string> newFrame);
    
    /// <summary>
    /// 清除屏幕
    /// </summary>
    public void Clear();
    
    /// <summary>
    /// 移动光标
    /// </summary>
    public void MoveCursor(int row, int col);
}
```

### 5.2 组件设计

```csharp
/// <summary>
/// 消息列表组件
/// </summary>
public class MessageListComponent : IComponent
{
    public void Render(MessageList messages, int startRow, int height);
}

/// <summary>
/// 编辑器组件
/// </summary>
public class EditorComponent : IComponent
{
    public string Content { get; set; }
    public int CursorPosition { get; set; }
    public event EventHandler<string>? OnSubmit;
    public event EventHandler? OnCancel;
}

/// <summary>
/// 状态栏组件
/// </summary>
public class StatusBarComponent : IComponent
{
    public string WorkingDirectory { get; set; }
    public string SessionName { get; set; }
    public string Model { get; set; }
    public int TokenUsage { get; set; }
    public decimal Cost { get; set; }
}
```

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

### 6.2 交互模式命令

| 命令 | 说明 |
|------|------|
| `/model` | 切换模型 |
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
| **Agent 框架** | 自实现 | Semantic Kernel |
| **LLM 调用** | 自实现 20+ | SK + 自定义 2 |
| **TUI** | 自实现 (差异化渲染) | Spectre.Console + 自定义 |
| **工具系统** | TypeBox + 自定义 | KernelFunction |
| **会话格式** | JSONL | JSONL (兼容) |

---

## 9. 关键挑战

1. **MiniMax/智谱 API 兼容性**
   - 需要适配其工具调用格式
   - API 签名认证 (MiniMax)
   
2. **流式输出与 TUI**
   - 需要正确处理 Server-Sent Events
   - 差异化渲染避免闪烁

3. **上下文压缩**
   - 中文总结需要合适的提示词
   - 保留关键信息 (文件修改、决策)

4. **Windows 兼容性**
   - ANSI 转义序列在不同终端的兼容性

---

## 10. 附录

### A. 配置示例

```json
{
  "model": "minimax/MiniMax-M2.1",
  "thinking": "medium",
  "tools": ["read", "write", "edit", "bash", "grep", "find", "ls"],
  "autoCompact": true,
  "compactThreshold": 0.8,
  "sessionDir": "~/.morty/sessions"
}
```

### B. 环境变量

```bash
# MiniMax
export MINIMAX_API_KEY="your_key"

# Zhipu
export ZHIPU_API_KEY="your_key"

# 配置
export MORTY_CONFIG_DIR="~/.morty"
```
