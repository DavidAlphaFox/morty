# 多供应商 Claude 支持设计

## 1. 需求概述

### 1.1 当前状态
- 单一的 Claude CLI 集成 (`claude -p`)
- 硬编码的进程执行

### 1.2 新需求

1. **多供应商支持**
   - 支持多种 Claude API 供应商 (Anthropic, OpenAI, Azure 等)
   - 每个供应商有不同的：API URL、认证令牌、模型、参数

2. **环境变量配置**
   - 通过环境变量配置供应商
   - 支持多个供应商配置

3. **规划/执行分离**
   - 规划阶段：使用一个供应商 (例如：更便宜/更快的模型)
   - 执行阶段：使用可能不同的供应商

4. **增强数据持久化**
   - 将生成的计划保存到 SQLite
   - 将每个任务的执行输出保存到 SQLite
   - 一个任务可以有多个输出 (计划 + 执行)

---

## 2. 架构变更

### 2.1 新增实体：供应商 (Provider)

```csharp
public class Provider
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;        // 例如："Anthropic", "Azure OpenAI"
    public string Type { get; set; } = string.Empty;          // 例如："anthropic", "openai", "azure"
    public string ApiUrl { get; set; } = string.Empty;        // API 端点 URL
    public string Model { get; set; } = string.Empty;         // 模型名称
    public string Token { get; set; } = string.Empty;         // API 令牌 (加密)
    public string ConfigJson { get; set; } = string.Empty;    // 额外配置 (temperature 等)
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### 2.2 增强实体：迭代 (Iteration)

添加 `ProviderId` 来追踪使用了哪个供应商：

```csharp
public class Iteration
{
    // ... 现有字段 ...
    public int? ProviderId { get; set; }  // 使用了哪个供应商
    public Provider? Provider { get; set; }
}
```

### 2.3 增强实体：计划 (Plan)

```csharp
public class Plan
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public string PlanContent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 新增字段
    public int? ProviderId { get; set; }           // 生成计划的供应商
    public PlanType Type { get; set; }             // 规划或执行
    public string Output { get; set; } = string.Empty;  // 完整输出内容

    public Story Story { get; set; } = null!;
    public Provider? Provider { get; set; }
}

public enum PlanType
{
    Planning,    // 初始分析和计划
    Execution    // 实现细节
}
```

### 2.4 新增实体：执行输出 (ExecutionOutput)

存储详细的执行输出：

```csharp
public class ExecutionOutput
{
    public int Id { get; set; }
    public int IterationId { get; set; }
    public int? ProviderId { get; set; }

    public string Prompt { get; set; } = string.Empty;      // 发送的内容
    public string Response { get; set; } = string.Empty;     // 原始响应
    public string ParsedOutput { get; set; } = string.Empty; // 解析结果

    public int? DurationMs { get; set; }
    public decimal? CostUsd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Iteration Iteration { get; set; } = null!;
    public Provider? Provider { get; set; }
}
```

---

## 3. 配置

### 3.1 环境变量

```bash
# 供应商配置 (JSON 数组)
MORTY_PROVIDERS='[
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
    "name": "Azure OpenAI",
    "type": "azure",
    "apiUrl": "https://${RESOURCE_NAME}.openai.azure.com/openai/deployments/${DEPLOYMENT_NAME}/chat/completions",
    "model": "gpt-4",
    "token": "${AZURE_OPENAI_API_KEY}",
    "isDefault": false,
    "config": { "apiVersion": "2024-02-01" }
  }
]'

# 默认供应商类型
MORTY_DEFAULT_PLAN_PROVIDER=Anthropic
MORTY_DEFAULT_EXECUTION_PROVIDER=Anthropic
```

### 3.2 配置加载

```csharp
public class ProviderConfig
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public Dictionary<string, object> Config { get; set; } = new();
}

public interface IProviderConfigLoader
{
    Task<List<ProviderConfig>> LoadFromEnvironmentAsync();
    Task InitializeDefaultProvidersAsync();
}
```

---

## 4. 供应商抽象

### 4.1 接口

```csharp
public interface IClaudeProvider
{
    string Name { get; }
    Task<ProviderResponse> SendMessageAsync(ProviderRequest request, CancellationToken ct);
    IAsyncEnumerable<string> StreamMessageAsync(ProviderRequest request, CancellationToken ct);
}

public record ProviderRequest(
    string Message,
    string? SystemPrompt = null,
    Dictionary<string, object>? Parameters = null
);

public record ProviderResponse(
    string Content,
    string? Error,
    int? TokenUsage,
    decimal? CostUsd,
    bool Success
);
```

### 4.2 实现

| 供应商 | 描述 |
|--------|------|
| `AnthropicProvider` | 直接使用 Anthropic API (`api.anthropic.com`) |
| `AzureOpenAIProvider` | Azure OpenAI 服务 |
| `OpenAIProvider` | OpenAI API |
| `ClaudeCliProvider` | 现有 CLI 封装 (向后兼容) |

### 4.3 工厂

```csharp
public interface IClaudeProviderFactory
{
    IClaudeProvider GetProvider(string name);
    IClaudeProvider GetProvider(ProviderType type);
    IClaudeProvider GetDefaultProvider(PlanType planType);
}

public enum PlanType
{
    Planning,
    Execution
}
```

---

## 5. 数据流

### 5.1 规划阶段

```
故事 (待处理)
    ↓
MortyLoopService.GetNextPendingStory()
    ↓
ClaudeProviderFactory.GetProvider(PlanType.Planning)
    ↓
provider.SendMessageAsync("分析 PRD 并创建计划")
    ↓
保存计划 (Type=规划, ProviderId, Output)
    ↓
故事 → 规划中
```

### 5.2 执行阶段

```
故事 (规划中)
    ↓
MortyLoopService.ProcessNextIteration()
    ↓
ClaudeProviderFactory.GetProvider(PlanType.Execution)
    ↓
provider.SendMessageAsync("实现计划")
    ↓
保存迭代 (ProviderId)
保存执行输出 (ProviderId, Prompt, Response)
    ↓
故事 → 进行中/已完成/失败
```

---

## 6. 数据库架构

### 新增表

| 表名 | 描述 |
|------|------|
| `Providers` | 供应商配置 |
| `ExecutionOutputs` | 详细执行结果 |

### 修改表

| 表名 | 变更 |
|------|------|
| `Iterations` | 添加 `ProviderId` |
| `Plans` | 添加 `ProviderId`, `Type`, `Output` |

---

## 7. API 变更

### 新增端点

```
GET    /api/providers           - 获取所有供应商
POST   /api/providers           - 创建供应商
GET    /api/providers/{id}      - 获取供应商详情
PUT    /api/providers/{id}      - 更新供应商
DELETE /api/providers/{id}      - 删除供应商
POST   /api/providers/initialize - 从环境变量初始化
```

---

## 8. 实现计划

1. **添加供应商实体和迁移**
2. **创建供应商接口和实现**
3. **实现从环境变量加载配置**
4. **更新迭代以追踪供应商 ID**
5. **添加执行输出实体**
6. **更新 MortyLoopService 使用供应商**
7. **添加供应商管理 API**
8. **更新前端以显示供应商信息**
