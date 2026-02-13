# Morty 项目任务

## 项目概述
基于 Claude API 的 AI 驱动开发管理系统，支持多供应商、SQLite 持久化、Kanban 和 Gantt 可视化。

## 阶段 1：基础架构 (已完成)
- [x] 创建 .NET 10 解决方案结构
- [x] 设置项目层级：Core、Infrastructure、Web
- [x] 配置 EF Core + SQLite
- [x] 添加 Serilog 日志

## 阶段 2：数据层 (已完成)
- [x] 2.1 定义完整的实体模型
- [x] 2.2 运行 EF Core 迁移
- [x] 2.3 创建仓储接口和实现

## 阶段 3：核心循环引擎 (已完成)
- [x] 3.1 实现 ClaudeClient (进程管理)
- [x] 3.2 实现 ResponseAnalyzer
- [x] 3.3 实现 CircuitBreaker
- [x] 3.4 实现 RateLimiter
- [x] 3.5 实现 MortyLoopService (后台服务)

## 阶段 3.1：项目独立工作目录 (已完成)
- [x] 向 Project 实体添加 WorkingDirectory 字段
- [x] 更新 MortyLoopService 使用项目独立工作目录
- [x] 如果工作目录不存在则自动创建
- [x] 创建 WorkingDirectory 迁移

## 阶段 4：Web API 层
- [x] 4.1 为 Projects、Stories、Iterations 创建 API 控制器
- [x] 4.2 添加 SignalR Hub 用于实时更新
- [x] 4.3 配置 CORS

## 阶段 5：前端 (静态 + API)
- [x] 5.1 设置静态文件服务
- [x] 5.2 实现 Kanban 看板 UI
- [ ] 5.3 实现 Gantt 甘特图
- [x] 5.4 实现仪表盘 Dashboard

## 阶段 6：多供应商 Claude 支持 (新增)
- [x] 6.1 添加 Provider 实体和迁移
- [x] 6.2 添加 ExecutionOutput 实体和迁移
- [x] 6.3 创建 IClaudeProvider 接口
- [x] 6.4 实现 AnthropicProvider (API 方式)
- [x] 6.5 实现 ClaudeCliProvider (向后兼容)
- [x] 6.6 实现 ProviderFactory
- [x] 6.7 添加从环境变量加载配置
- [x] 6.8 更新 Iteration 追踪 ProviderId
- [x] 6.9 更新 Plan 实体 (添加 ProviderId、Type、Output)
- [x] 6.10 添加 Providers API 控制器
- [x] 6.11 更新 MortyLoopService 使用供应商

## 阶段 7：CLI 入口点
- [x] 7.1 添加 CLI 命令支持
- [x] 7.2 配置 CLI 命令

## 阶段 8：测试
- [ ] 8.1 核心服务单元测试
- [ ] 8.2 集成测试

## 阶段 9：部署
- [ ] 9.1 配置发布配置文件
- [ ] 9.2 自包含单文件构建

---

## 技术栈

| 组件 | 技术 |
|--------|------------|
| 框架 | .NET 10 + ASP.NET Core |
| 数据库 | EF Core + SQLite |
| 日志 | Serilog |
| 实时通信 | SignalR |
| 前端 | 静态 HTML/JS |
| CLI | System.CommandLine |
| 供应商 | Anthropic, Azure OpenAI, OpenAI |

## 实体模型 (已更新)

```
Project (1) ──→ (*) Story (1) ──→ (*) Iteration ──→ (*) ExecutionOutput
                      │                    │
                      ├──→ (*) Plan        └──→ (*) Verification
                      │    (Type: Planning/Execution)
                      └──→ (*) Event

Provider (1) ──→ (*) Iteration
       │            (ProviderId)
       └──→ (*) Plan
       └──→ (*) ExecutionOutput
```

### Kanban 看板列
| 列名 | 状态 |
|--------|--------|
| 待处理 | Pending |
| 规划中 | Planning |
| 进行中 | InProgress |
| 验证中 | Verifying |
| 已完成 | Completed |
| 失败 | Failed |

## 项目结构 (已更新)

```
morty/
├── Morty.slnx
├── TASK.md
├── design/
│   ├── architecture.md
│   └── multi-vendor-support.md
└── src/
    ├── Morty.Core/
    │   ├── Entities/
    │   │   └── Entities.cs
    │   ├── Interfaces/
    │   │   └── Interfaces.cs
    │   ├── Repositories/
    │   │   └── IRepositories.cs
    │   ├── Services/
    │   │   ├── ClaudeClient.cs
    │   │   ├── ResponseAnalyzer.cs
    │   │   ├── CircuitBreaker.cs
    │   │   └── RateLimiter.cs
    │   └── Morty.Core.csproj
    │
    ├── Morty.Infrastructure/
    │   ├── Data/
    │   │   ├── MortyDbContext.cs
    │   │   └── Migrations/
    │   └── Repositories/
    │       └── Repositories.cs
    │   └── Morty.Infrastructure.csproj
    │
    └── Morty.Web/
        ├── Program.cs
        ├── Services/
        │   └── MortyLoopService.cs
        ├── Controllers/
        ├── Hubs/
        ├── wwwroot/
        │   └── index.html
        ├── appsettings.json
        └── Morty.Web.csproj
```

## 已实现组件

### 核心服务
- **ClaudeClient**: 封装 `claude -p` 进程，处理 stdin/stdout 流式传输
- **ResponseAnalyzer**: 分析 Claude 输出，判断完成状态、错误、修改的文件
- **CircuitBreaker**: 防止无限重试循环 (关闭/打开/半开状态)
- **RateLimiter**: 遵守 API 速率限制 (每分钟请求数)
- **MortyLoopService**: 编排整个循环的后台服务

### 仓储
- ProjectRepository, StoryRepository, IterationRepository
- PlanRepository, VerificationRepository, StoryEventRepository

### 数据库
- SQLite + EF Core 迁移已应用
- 表：Projects, Stories, Iterations, Plans, Verifications, StoryEvents
