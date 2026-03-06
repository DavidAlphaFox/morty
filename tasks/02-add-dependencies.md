# 任务: 配置依赖

## 阶段
Phase 1: 基础设施

## 描述
为 morty 项目添加所有必需的 NuGet 依赖包，包括 Microsoft.AgentFramework、Terminal.Gui、System.CommandLine、Serilog 等。

## 验收标准
- [ ] 所有依赖包正确安装
- [ ] 版本兼容
- [ ] 项目可成功 build

## 实现步骤

### 2.1 添加核心依赖
```bash
# 添加所有项目共用的依赖到 src/cli/morty.cli.csproj
dotnet add src/cli/morty.cli.csproj package Microsoft.AgentFramework
dotnet add src/cli/morty.cli.csproj package Terminal.Gui
dotnet add src/cli/morty.cli.csproj package System.CommandLine
dotnet add src/cli/morty.cli.csproj package Microsoft.Extensions.DependencyInjection
dotnet add src/cli/morty.cli.csproj package Microsoft.Extensions.Hosting
dotnet add src/cli/morty.cli.csproj package Serilog
dotnet add src/cli/morty.cli.csproj package Serilog.Extensions.Hosting
dotnet add src/cli/morty.cli.csproj package Serilog.Sinks.Console
```

### 2.2 验证依赖版本
检查各依赖包的最新稳定版本，确保兼容。

### 2.3 添加到各子项目
```bash
# llm 项目需要 HTTP 客户端
dotnet add src/llm/morty.llm.csproj package System.Net.Http.Json

# config 项目需要配置验证
dotnet add src/config/morty.config.csproj package Microsoft.Extensions.Configuration.Json
```

## 依赖清单
| 包名 | 版本 | 用途 |
|------|------|------|
| Microsoft.AgentFramework | 0.1.x | Agent 框架 |
| Terminal.Gui | 2.0.x | TUI 界面 |
| System.CommandLine | 2.0.x | CLI 解析 |
| Microsoft.Extensions.DependencyInjection | 8.x | DI |
| Microsoft.Extensions.Hosting | 8.x | 主机 |
| Serilog | 3.x | 日志 |
| Serilog.Extensions.Hosting | 8.x | 日志集成 |
| Serilog.Sinks.Console | 5.x | 控制台输出 |
| System.Net.Http.Json | 8.x | HTTP JSON |

## 相关文件
- tasks/01-create-project-structure.md
- design/dotnet-coding-agent.md (2.2 依赖包)
