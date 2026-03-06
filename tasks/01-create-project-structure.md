# 任务: 创建项目结构

## 阶段
Phase 1: 基础设施

## 描述
创建 morty 项目的目录结构和基础文件，包括 CLI、TUI、Agent、Tools、LLM、Config、Auth 等模块的目录和占位文件。

## 验收标准
- [ ] 目录结构完整
- [ ] .csproj 和 .sln 文件可编译
- [ ] 项目可成功 build

## 实现步骤

### 1.1 创建目录结构
```bash
mkdir -p src/cli/Commands
mkdir -p src/cli/Options
mkdir -p src/tui/Views
mkdir -p src/agent/Events
mkdir -p src/tools
mkdir -p src/llm
mkdir -p src/config
mkdir -p src/auth
mkdir -p tests
```

### 1.2 创建解决方案和项目文件
```bash
# 创建解决方案
dotnet new sln -n morty

# 创建各个项目
dotnet new console -n morty.cli -o src/cli
dotnet new classlib -n morty.agent -o src/agent
dotnet new classlib -n morty.tools -o src/tools
dotnet new classlib -n morty.llm -o src/llm
dotnet new classlib -n morty.config -o src/config
dotnet new classlib -n morty.auth -o src/auth

# 添加到解决方案
dotnet sln add src/cli/morty.cli.csproj
dotnet sln add src/agent/morty.agent.csproj
dotnet sln add src/tools/morty.tools.csproj
dotnet sln add src/llm/morty.llm.csproj
dotnet sln add src/config/morty.config.csproj
dotnet sln add src/auth/morty.auth.csproj
```

### 1.3 添加项目引用
```bash
# cli 引用其他所有项目
dotnet add src/cli/morty.cli.csproj reference src/agent/morty.agent.csproj
dotnet add src/cli/morty.cli.csproj reference src/tools/morty.tools.csproj
dotnet add src/cli/morty.cli.csproj reference src/llm/morty.llm.csproj
dotnet add src/cli/morty.cli.csproj reference src/config/morty.config.csproj
dotnet add src/cli/morty.cli.csproj reference src/auth/morty.auth.csproj
```

### 1.4 添加基础占位文件
- src/cli/Program.cs - CLI 入口
- src/agent/Agent.cs - Agent 占位
- src/tools/FileTools.cs - 工具占位
- src/llm/ILlmProvider.cs - Provider 接口

## 相关文件
- design/TASK.md
- design/dotnet-coding-agent.md (1.2 项目结构)
