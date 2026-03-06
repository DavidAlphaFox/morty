# 任务 4.1: 更多 LLM 提供商

## 阶段
Phase 4 — 体验优化

## 目标
支持 OpenAI 兼容接口作为通用 Provider，自定义 BaseUrl 支持代理/私有部署。

## 设计方案

### 1. OpenAI 兼容 Provider

```csharp
// src/llm/OpenAICompatProvider.cs

/// <summary>
/// 通用 OpenAI 兼容 Provider
/// 支持任何兼容 OpenAI Chat Completions API 的服务
/// </summary>
public class OpenAICompatProvider : IChatClient
{
    // 复用现有的 OpenAISerializer
    // 构造函数: (apiKey, baseUrl, defaultModel)
    // 支持自定义 headers (如 x-api-key)
}
```

### 2. 配置扩展

```json
{
  "provider": {
    "type": "openai-compat",
    "baseUrl": "https://my-proxy.example.com/v1",
    "model": "gpt-4o",
    "headers": {
      "x-custom-header": "value"
    }
  }
}
```

### 3. ProviderFactory 扩展

```csharp
"openai" or "openai-compat" => new OpenAICompatProvider(apiKey, baseUrl, headers),
"deepseek" => new OpenAICompatProvider(apiKey, "https://api.deepseek.com/v1"),
"ollama" => new OpenAICompatProvider("", baseUrl ?? "http://localhost:11434/v1"),
```

## 实现步骤

1. [ ] 创建 `src/llm/OpenAICompatProvider.cs`
2. [ ] `ProviderFactory.cs` — 添加 openai-compat, deepseek, ollama 预设
3. [ ] `ConfigOptions.cs` — ProviderConfig 新增 headers 字段
4. [ ] 测试: 连接 Ollama 本地服务

## 验收标准
- [ ] 可连接任意 OpenAI 兼容 API
- [ ] deepseek, ollama 等预设可用
- [ ] 自定义 headers 支持

## 相关文件
- `src/llm/OpenAICompatProvider.cs` — 新建
- `src/llm/ProviderFactory.cs` — 扩展
- `src/config/ConfigOptions.cs` — headers 字段
