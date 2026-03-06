# morty 开发任务清单

## 项目概述

使用 .NET 和 Microsoft.AgentFramework 实现类似 pi (coding-agent) 的终端 AI 编程助手。

- **LLM 提供商**: Zhipu (GLM), MiniMax, Qianwen (qwen3)
- **平台**: Linux (重点), macOS (可选)
- **TUI**: Terminal.Gui

---

## Phase 1: 基础设施

- [ ] 创建项目结构
  - [ ] `src/cli/` - CLI 入口
  - [ ] `src/tui/` - TUI 界面 (Terminal.Gui)
  - [ ] `src/agent/` - Agent 核心
  - [ ] `src/tools/` - 工具实现
  - [ ] `src/llm/` - LLM 提供商
  - [ ] `src/config/` - 配置管理
  - [ ] `src/auth/` - 凭证管理

- [ ] 配置依赖
  - [ ] Microsoft.AgentFramework
  - [ ] Terminal.Gui
  - [ ] System.CommandLine
  - [ ] Microsoft.Extensions.DependencyInjection
  - [ ] Serilog

- [ ] 实现日志系统
  - [ ] 配置 Serilog
  - [ ] 控制台输出
  - [ ] 日志文件

- [ ] 创建 .NET 项目文件 (.csproj, .sln)

---

## Phase 2: 配置与凭证

### 2.1 配置管理

- [ ] 实现配置加载器 (`ConfigLoader.cs`)
  - [ ] 支持 `~/.config/morty/morty.json`
  - [ ] 支持环境变量 `MORTY_CONFIG_DIR`
  - [ ] 支持项目根目录 `.morty.json`
  - [ ] 支持 JSON Schema 验证

- [ ] 实现配置选项 (`ConfigOptions.cs`)
  - [ ] Provider 配置
  - [ ] Tools 配置
  - [ ] MCP 配置
  - [ ] Session 配置
  - [ ] TUI 配置

### 2.2 凭证管理

- [ ] 实现凭证存储 (`AuthStore.cs`)
  - [ ] 凭证文件路径: `~/.local/share/morty/auth.json`
  - [ ] 读写凭证文件
  - [ ] 安全权限 (600)

- [ ] 实现凭证管理器 (`AuthManager.cs`)
  - [ ] 登录 API Key
  - [ ] 获取 API Key
  - [ ] 列出已登录的提供商
  - [ ] 登出

- [ ] 实现凭证命令 (`AuthCommands.cs`)
  - [ ] `morty auth login <provider>`
  - [ ] `morty auth list`
  - [ ] `morty auth logout <provider>`

---

## Phase 3: LLM 提供商

- [ ] 定义 LLM 提供商接口 (`ILlmProvider.cs`)
  - [ ] Chat 接口
  - [ ] 流式输出
  - [ ] 工具调用

- [ ] 实现 Zhipu Provider (`ZhipuProvider.cs`)
  - [ ] API: `https://open.bigmodel.cn/api/paas/v4`
  - [ ] 认证: Bearer Token
  - [ ] 支持模型: glm-4, glm-4-flash, glm-4-plus, glm-4v-plus

- [ ] 实现 MiniMax Provider (`MiniMaxProvider.cs`)
  - [ ] API: `https://api.minimax.io/v1`
  - [ ] 认证: HMAC-SHA256 签名
  - [ ] 支持模型: MiniMax-M2, MiniMax-M2.1

- [ ] 实现 Qianwen Provider (`QianwenProvider.cs`)
  - [ ] API: `https://dashscope.aliyuncs.com/compatible-mode/v1`
  - [ ] 认证: Bearer Token
  - [ ] 支持模型: qwen-turbo, qwen-plus, qwen-max, qwen-long, qwen2.5-vl

- [ ] 实现 Provider 工厂 (`ProviderFactory.cs`)
  - [ ] 根据配置创建 Provider

---

## Phase 4: Agent 核心

- [ ] 实现 Agent 核心 (`CodingAgent.cs`)
  - [ ] 基于 Microsoft.AgentFramework IAgent
  - [ ] 消息管理 (会话历史)
  - [ ] 干预机制 (steer/followUp)
  - [ ] 上下文压缩

- [ ] 实现会话管理 (`SessionManager.cs`)
  - [ ] 会话存储 (JSONL 格式)
  - [ ] 加载会话历史
  - [ ] 保存消息
  - [ ] 会话分支 (Fork)

- [ ] 实现上下文压缩 (`ContextCompactor.cs`)
  - [ ] Token 估算
  - [ ] 消息总结
  - [ ] 保留关键信息

- [ ] 实现事件系统 (`AgentEvent.cs`)
  - [ ] 消息事件
  - [ ] 工具调用事件
  - [ ] 错误事件

---

## Phase 5: 工具系统

### 5.1 内置工具

- [ ] 实现文件操作工具 (`FileTools.cs`)
  - [ ] `read` - 读取文件
  - [ ] `write` - 写入文件
  - [ ] `edit` - 编辑文件
  - [ ] 路径安全检查 (工作目录内)

- [ ] 实现系统工具 (`SystemTools.cs`)
  - [ ] `bash` - 执行命令
  - [ ] `grep` - 搜索内容
  - [ ] `find` - 查找文件
  - [ ] `ls` - 列出目录
  - [ ] 命令白名单
  - [ ] 超时控制

### 5.2 MCP 工具

- [ ] 实现 MCP 客户端抽象 (`IMcpClient.cs`)

- [ ] 实现 Stdio MCP 客户端 (`StdioMcpClient.cs`)
  - [ ] 子进程管理
  - [ ] JSON-RPC 通信

- [ ] 实现 SSE MCP 客户端 (`SseMcpClient.cs`)
  - [ ] HTTP 连接
  - [ ] 事件流处理

- [ ] 实现 MCP 工具管理器 (`McpToolManager.cs`)
  - [ ] 初始化 MCP 服务器
  - [ ] 工具注册
  - [ ] 工具调用

---

## Phase 6: TUI 界面

### 6.1 基础框架

- [ ] 实现 TUI 应用入口 (`Program.cs`)
  - [ ] 初始化 Terminal.Gui
  - [ ] 窗口布局

- [ ] 实现主窗口 (`MortyApp.cs`)
  - [ ] 菜单栏
  - [ ] 消息区域
  - [ ] 输入区域
  - [ ] 状态栏

### 6.2 视图组件

- [ ] 实现消息列表视图 (`MessageListView.cs`)
  - [ ] 显示用户消息
  - [ ] 显示 AI 回复
  - [ ] 显示工具调用
  - [ ] 滚动支持
  - [ ] 代码高亮

- [ ] 实现输入视图 (`InputView.cs`)
  - [ ] 文本输入
  - [ ] 命令历史
  - [ ] 自动补全

- [ ] 实现状态栏视图 (`StatusBarView.cs`)
  - [ ] 当前目录
  - [ ] 会话名称
  - [ ] 模型名称
  - [ ] Token 使用量
  - [ ] 费用

### 6.3 交互功能

- [ ] 实现键盘快捷键
  - [ ] Ctrl+C - 中断
  - [ ] Ctrl+L - 清除屏幕
  - [ ] Ctrl+O - 打开设置

- [ ] 实现交互模式命令
  - [ ] `/model` - 切换模型
  - [ ] `/auth` - 管理认证
  - [ ] `/settings` - 设置
  - [ ] `/new` - 新建会话
  - [ ] `/resume` - 恢复会话
  - [ ] `/compact` - 压缩上下文
  - [ ] `/copy` - 复制回复
  - [ ] `/export` - 导出会话
  - [ ] `/quit` - 退出

---

## Phase 7: CLI 命令

- [ ] 实现命令行入口 (`Program.cs`)
  - [ ] `morty` - 交互模式
  - [ ] `morty "prompt"` - 单次对话
  - [ ] `morty -p "prompt"` - 打印模式
  - [ ] `morty --model <name>` - 指定模型
  - [ ] `morty -c` - 继续会话
  - [ ] `morty --session <id>` - 指定会话

- [ ] 实现会话命令
  - [ ] `morty session list`
  - [ ] `morty session show <id>`
  - [ ] `morty session export <id>`

- [ ] 实现模型命令
  - [ ] `morty models`
  - [ ] `morty models <provider>`

---

## Phase 8: 测试与优化

- [ ] 单元测试
  - [ ] 配置加载测试
  - [ ] 凭证管理测试
  - [ ] LLM Provider 测试
  - [ ] 工具测试

- [ ] 集成测试
  - [ ] 完整对话流程
  - [ ] 工具调用流程
  - [ ] MCP 集成

- [ ] 性能优化
  - [ ] 上下文压缩优化
  - [ ] 流式输出优化

- [ ] 错误处理
  - [ ] API 错误重试
  - [ ] 网络超时处理
  - [ ] 异常捕获与日志

---

## 关键挑战

1. **Microsoft.AgentFramework 成熟度**
   - 框架相对较新，API 可能变化
   - 文档和社区资源有限

2. **MiniMax API 签名认证**
   - HMAC-SHA256 计算
   - 签名验证

3. **流式输出与 TUI**
   - Server-Sent Events 处理
   - 增量渲染

4. **上下文压缩**
   - 中文总结提示词
   - 保留关键信息

5. **终端兼容性**
   - ANSI 转义序列
   - 终端类型检测

---

## 依赖顺序

```
Phase 1 (基础设施)
    ↓
Phase 2 (配置与凭证) ←→ Phase 3 (LLM Provider)
    ↓
Phase 4 (Agent 核心) ← Phase 5 (工具系统)
    ↓
Phase 6 (TUI) ← Phase 7 (CLI)
    ↓
Phase 8 (测试)
```
