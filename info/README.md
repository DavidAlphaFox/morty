# Morty CLI 使用指南

## 概述

Morty CLI 是 Morty 项目的命令行工具，提供项目管理、故事管理、供应商管理等功能的命令行界面。

## 快速开始

```bash
# 进入项目目录
cd /home/david/workspace/morty/src/Morty.Web

# 查看帮助
dotnet run -- help
```

## 命令列表

### 帮助命令

```bash
dotnet run -- help
# 或
dotnet run -- -h
```

### 服务器命令

```bash
# 启动服务器 (显示提示信息)
dotnet run -- server --urls=http://localhost:5000
```

### 项目命令

```bash
# 列出所有项目
dotnet run -- project list

# 创建项目
dotnet run -- project create --name="项目名称" --working-dir="./workspace"

# 获取项目详情
dotnet run -- project get --id=1

# 删除项目
dotnet run -- project delete --id=1
```

### 故事命令

```bash
# 列出项目中的所有故事
dotnet run -- story list --project-id=1

# 创建故事
dotnet run -- story create --project-id=1 --title="实现登录功能" --priority=High
```

### 供应商命令

```bash
# 列出所有供应商
dotnet run -- provider list

# 添加供应商 (显示提示)
dotnet run -- provider add --name=Anthropic --type=anthropic
```

## REST API 参考

CLI 命令会打印对应的 REST API 调用示例：

### 项目 API

```bash
# 获取所有项目
curl http://localhost:5000/api/projects

# 获取单个项目
curl http://localhost:5000/api/projects/1

# 创建项目
curl -X POST http://localhost:5000/api/projects \
  -H 'Content-Type: application/json' \
  -d '{"name":"项目名称","workingDirectory":"./workspace","prdJson":"{}"}'

# 删除项目
curl -X DELETE http://localhost:5000/api/projects/1
```

### 故事 API

```bash
# 获取项目的所有故事
curl http://localhost:5000/api/projects/1/stories

# 获取单个故事
curl http://localhost:5000/api/stories/1

# 创建故事
curl -X POST http://localhost:5000/api/projects/1/stories \
  -H 'Content-Type: application/json' \
  -d '{"title":"实现登录功能","storyId":"US-001","priority":"High"}'

# 更新故事状态
curl -X PATCH http://localhost:5000/api/stories/1 \
  -H 'Content-Type: application/json' \
  -d '{"status":"InProgress"}'
```

### 供应商 API

```bash
# 获取所有供应商
curl http://localhost:5000/api/providers

# 创建供应商
curl -X POST http://localhost:5000/api/providers \
  -H 'Content-Type: application/json' \
  -d '{"name":"Anthropic","type":"anthropic","apiUrl":"https://api.anthropic.com/v1/messages","model":"claude-sonnet-4-20250514","token":"sk-...","isDefault":true}'
```

### 迭代 API

```bash
# 获取故事的迭代列表
curl http://localhost:5000/api/stories/1/iterations
```

### 仪表盘 API

```bash
# 获取项目统计
curl http://localhost:5000/api/dashboard/stats
```

## 环境变量配置

### 供应商配置

通过环境变量配置多个 Claude 供应商：

```bash
# 单个供应商
export MORTY_PROVIDERS='[{"name":"Anthropic","type":"anthropic","apiUrl":"https://api.anthropic.com/v1/messages","model":"claude-sonnet-4-20250514","token":"${ANTHROPIC_API_KEY}","isDefault":true}]'

# 多个供应商
export MORTY_PROVIDERS='[
  {
    "name": "Anthropic",
    "type": "anthropic",
    "apiUrl": "https://api.anthropic.com/v1/messages",
    "model": "claude-sonnet-4-20250514",
    "token": "${ANTHROPIC_API_KEY}",
    "isDefault": true,
    "config": { "maxTokens": 4096, "temperature": 0.7 }
  },
  {
    "name": "Claude CLI",
    "type": "cli",
    "token": "claude",
    "isDefault": false
  }
]'

# 设置默认供应商类型
export MORTY_DEFAULT_PLAN_PROVIDER=Anthropic
export MORTY_DEFAULT_EXECUTION_PROVIDER=Anthropic
```

## 启动服务器

```bash
# 方式一：直接运行
dotnet run --project Morty.Web

# 方式二：指定 URL
dotnet run --project Morty.Web -- --urls="http://localhost:8080"

# 方式三：发布后运行
dotnet publish -c Release -o ./publish
./publish/Morty.Web --urls="http://0.0.0.0:5000"
```

## Web 界面

启动服务器后，可以通过浏览器访问：

- **主页**: http://localhost:5000
- **API 端点**: http://localhost:5000/api
- **SignalR Hub**: http://localhost:5000/morty-hub

## 常见问题

### Q: 如何查看所有命令？

```bash
dotnet run -- help
```

### Q: CLI 命令和 Web 服务器可以同时运行吗？

不能同时运行。如果传入 CLI 命令参数（如 `project list`），程序会进入 CLI 模式并退出，不会启动 Web 服务器。

### Q: 如何使用 CLI 创建完整的项目？

目前 CLI 提供提示信息，实际操作需要配合 REST API 或 Web 界面使用。
