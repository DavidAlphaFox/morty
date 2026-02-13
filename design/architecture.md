# Morty 架构设计

## 系统概述

Morty 是一个由 AI 驱动的开发管理系统，它通过编排 Claude CLI 来自动实现用户故事，通过 Kanban/Gantt 跟踪进度，并将所有数据持久化到 SQLite 中。

## 高级架构

```
┌─────────────────────────────────────────────────────────────┐
│                      Morty.Web                             │
│                  (ASP.NET Core API)                         │
│                                                             │
│  ┌─────────────────┐  ┌─────────────────────────────┐   │
│  │ 后台服务        │  │ Web API + SignalR            │   │
│  │ (MortyLoop)    │──│ (Controllers + Hubs)         │   │
│  └────────┬────────┘  └──────────────┬──────────────┘   │
│           │                           │                   │
│           │                    ┌──────▼──────┐            │
│           │                    │ 静态前端    │            │
│           │                    │ (HTML/JS)   │            │
│           │                    └─────────────┘            │
└───────────┼───────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────┐
│              Morty.Infrastructure                           │
│                  (EF Core + SQLite)                         │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ DbContext, 仓储, 迁移                                 │  │
│  └─────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────┐
│                    SQLite 数据库                            │
└─────────────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────┐
│                    claude -p (外部进程)                     │
└─────────────────────────────────────────────────────────────┘
```

## 组件设计

### 1. MortyLoopService (后台服务)

作为后台服务运行的核心编排引擎。

**职责：**
- 按优先级从数据库加载故事
- 为每个故事迭代调用 Claude CLI
- 分析 Claude 的响应以确定下一步操作
- 根据结果更新故事状态
- 触发验证测试
- 通过 SignalR 广播更新

**状态机：**
```
待处理 → 规划中 → 进行中 → 验证中 → 已完成
                    ↓              ↓
                  失败 ←───────────┘
```

### 2. ClaudeClient

围绕 `System.Diagnostics.Process` 的封装，用于与 Claude CLI 交互。

**特性：**
- 启动 `claude -p` 进程
- 流式处理 stdin/stdout
- 处理超时和取消
- 解析 JSON 响应

### 3. ResponseAnalyzer

分析 Claude 的输出来确定：
- 实现是否完成
- 测试是否通过
- 代码是否编译
- 修改了哪些文件

### 4. CircuitBreaker

防止 Claude 反复失败时陷入无限重试循环。

**状态：**
- 关闭：正常操作
- 打开：近期失败次数超过阈值
- 半开：测试是否可能恢复

### 5. RateLimiter

遵守 Claude API 速率限制。

**配置：**
- 每分钟请求数
- 每天请求数
- Token 限制（未来）

## 数据库架构

### 项目表 (Projects)
| 列名 | 类型 | 描述 |
|------|------|------|
| Id | INTEGER PK | 自增 |
| Name | TEXT | 项目名称 |
| PrdJson | TEXT | 完整 PRD 内容 |
| WorkingDirectory | TEXT | 项目工作目录 |
| CreatedAt | TEXT | ISO8601 时间戳 |

### 故事表 (Stories)
| 列名 | 类型 | 描述 |
|------|------|------|
| Id | INTEGER PK | 自增 |
| ProjectId | INTEGER FK | 关联项目 |
| StoryId | TEXT | PRD 中的用户故事 ID |
| Title | TEXT | 故事标题 |
| Priority | TEXT | 高/中/低 |
| Status | TEXT | 当前状态 |
| CreatedAt | TEXT | ISO8601 时间戳 |
| CompletedAt | TEXT | 完成时间 |

### 迭代表 (Iterations)
| 列名 | 类型 | 描述 |
|------|------|------|
| Id | INTEGER PK | 自增 |
| StoryId | INTEGER FK | 关联故事 |
| IterationNum | INTEGER | 迭代编号 |
| ProviderId | INTEGER FK | 关联供应商 |
| StartedAt | TEXT | ISO8601 时间戳 |
| CompletedAt | TEXT | ISO8601 时间戳 |
| DurationMs | INTEGER | 执行时间(毫秒) |
| CostUsd | TEXT | 美元成本 |
| Output | TEXT | Claude 输出 |

### 计划表 (Plans)
| 列名 | 类型 | 描述 |
|------|------|------|
| Id | INTEGER PK | 自增 |
| StoryId | INTEGER FK | 关联故事 |
| PlanContent | TEXT | 实现计划 |
| ProviderId | INTEGER FK | 关联供应商 |
| Type | TEXT | 计划类型(规划/执行) |
| Output | TEXT | 完整输出 |
| CreatedAt | TEXT | ISO8601 时间戳 |

### 验证表 (Verifications)
| 列名 | 类型 | 描述 |
|------|------|------|
| Id | INTEGER PK | 自增 |
| IterationId | INTEGER FK | 关联迭代 |
| Type | TEXT | 测试/构建/检查 |
| Passed | INTEGER | 0 或 1 |
| Output | TEXT | 验证输出 |
| CreatedAt | TEXT | ISO8601 时间戳 |

### 故事事件表 (StoryEvents)
| 列名 | 类型 | 描述 |
|------|------|------|
| Id | INTEGER PK | 自增 |
| StoryId | INTEGER FK | 关联故事 |
| EventType | TEXT | 事件类型 |
| Timestamp | TEXT | ISO8601 时间戳 |
| DataJson | TEXT | 事件数据 |

### 供应商表 (Providers)
| 列名 | 类型 | 描述 |
|------|------|------|
| Id | INTEGER PK | 自增 |
| Name | TEXT | 供应商名称 |
| Type | TEXT | 供应商类型 |
| ApiUrl | TEXT | API 端点 URL |
| Model | TEXT | 模型名称 |
| Token | TEXT | API 令牌 |
| ConfigJson | TEXT | 额外配置 |
| IsDefault | INTEGER | 是否默认 |
| CreatedAt | TEXT | ISO8601 时间戳 |

### 执行输出表 (ExecutionOutputs)
| 列名 | 类型 | 描述 |
|------|------|------|
| Id | INTEGER PK | 自增 |
| IterationId | INTEGER FK | 关联迭代 |
| ProviderId | INTEGER FK | 关联供应商 |
| Prompt | TEXT | 发送的内容 |
| Response | TEXT | 原始响应 |
| ParsedOutput | TEXT | 解析结果 |
| DurationMs | INTEGER | 执行时间 |
| CostUsd | REAL | 美元成本 |
| CreatedAt | TEXT | ISO8601 时间戳 |

## API 设计

### REST 端点

#### 项目 (Projects)
- `GET /api/projects` - 获取所有项目
- `GET /api/projects/{id}` - 获取项目详情
- `POST /api/projects` - 创建项目
- `DELETE /api/projects/{id}` - 删除项目

#### 故事 (Stories)
- `GET /api/projects/{projectId}/stories` - 获取故事列表
- `GET /api/stories/{id}` - 获取故事详情
- `PATCH /api/stories/{id}` - 更新故事状态

#### 迭代 (Iterations)
- `GET /api/stories/{storyId}/iterations` - 获取迭代列表

#### 供应商 (Providers)
- `GET /api/providers` - 获取所有供应商
- `GET /api/providers/{id}` - 获取供应商详情
- `POST /api/providers` - 创建供应商
- `PUT /api/providers/{id}` - 更新供应商
- `DELETE /api/providers/{id}` - 删除供应商

#### 仪表盘 (Dashboard)
- `GET /api/dashboard/stats` - 获取项目统计

### SignalR Hub

**Hub: MortyHub**

方法：
- `JoinProject(int projectId)` - 订阅项目更新
- `OnStoryUpdated(Story story)` - 广播故事变更
- `OnIterationComplete(Iteration iteration)` - 广播迭代结果
- `OnProjectStatsUpdated(Stats stats)` - 广播仪表盘更新

## 配置

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=morty.db"
  },
  "Morty": {
    "ClaudeCommand": "claude",
    "ClaudeArgs": "-p",
    "WorkingDirectory": "./workspace",
    "MaxIterations": 10,
    "TimeoutMinutes": 15,
    "RateLimit": {
      "RequestsPerMinute": 10
    },
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "ResetMinutes": 5
    }
  }
}
```

## 部署

### 构建
```bash
dotnet publish -c Release --self-contained -r linux-x64
```

### 运行
```bash
./Morty.Web --urls "http://localhost:5000"
```

## 未来考虑

### 前端选项
1. **当前**：静态 HTML/JS 调用 API
2. **未来**：React/Vue SPA
3. **未来**：Blazor WASM

### 扩展
- 多项目支持
- Worker 角色分离
- Redis 用于 SignalR 扩展

### 监控
- 健康检查
- 指标 (OpenTelemetry)
- 分布式追踪
