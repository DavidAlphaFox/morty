# Multi-Vendor Claude Support Design

## 1. Requirements Overview

### 1.1 Current State
- Single Claude CLI integration (`claude -p`)
- Hardcoded process execution

### 1.2 New Requirements

1. **Multi-Vendor Support**
   - Support multiple Claude API providers (Anthropic, OpenAI, Azure, etc.)
   - Each vendor has different: API URL, Auth Token, Model, Parameters

2. **Environment Variable Configuration**
   - Provider configs via environment variables
   - Support for multiple provider configurations

3. **Plan/Execution Separation**
   - Planning phase: Uses one provider (e.g., cheaper/faster model)
   - Execution phase: Uses potentially different provider

4. **Enhanced Data Persistence**
   - Save generated plans to SQLite
   - Save each task's execution output to SQLite
   - A Task can have multiple outputs (plan + execution)

---

## 2. Architecture Changes

### 2.1 New Entity: Provider

```csharp
public class Provider
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;        // e.g., "Anthropic", "Azure OpenAI"
    public string Type { get; set; } = string.Empty;        // e.g., "anthropic", "openai", "azure"
    public string ApiUrl { get; set; } = string.Empty;      // API endpoint URL
    public string Model { get; set; } = string.Empty;       // Model name
    public string Token { get; set; } = string.Empty;      // API token (encrypted)
    public string ConfigJson { get; set; } = string.Empty;  // Additional config (temperature, etc.)
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### 2.2 Enhanced Entity: Iteration

Add `ProviderId` to track which provider was used:

```csharp
public class Iteration
{
    // ... existing fields ...
    public int? ProviderId { get; set; }  // Which provider was used
    public Provider? Provider { get; set; }
}
```

### 2.3 Enhanced Entity: Plan

```csharp
public class Plan
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public string PlanContent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // New fields
    public int? ProviderId { get; set; }           // Which provider generated the plan
    public PlanType Type { get; set; }           // planning or execution
    public string Output { get; set; } = string.Empty;  // Full output content

    public Story Story { get; set; } = null!;
    public Provider? Provider { get; set; }
}

public enum PlanType
{
    Planning,    // Initial analysis and plan
    Execution    // Implementation details
}
```

### 2.4 New Entity: ExecutionOutput

Store detailed execution output:

```csharp
public class ExecutionOutput
{
    public int Id { get; set; }
    public int IterationId { get; set; }
    public int? ProviderId { get; set; }

    public string Prompt { get; set; } = string.Empty;      // What was sent
    public string Response { get; set; } = string.Empty;     // Raw response
    public string ParsedOutput { get; set; } = string.Empty; // Parsed result

    public int? DurationMs { get; set; }
    public decimal? CostUsd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Iteration Iteration { get; set; } = null!;
    public Provider? Provider { get; set; }
}
```

---

## 3. Configuration

### 3.1 Environment Variables

```bash
# Provider configuration (JSON array)
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

# Default provider types
MORTY_DEFAULT_PLAN_PROVIDER=Anthropic
MORTY_DEFAULT_EXECUTION_PROVIDER=Anthropic
```

### 3.2 Configuration Loading

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

## 4. Provider Abstraction

### 4.1 Interface

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

### 4.2 Implementations

| Provider | Description |
|----------|-------------|
| `AnthropicProvider` | Direct Anthropic API (`api.anthropic.com`) |
| `AzureOpenAIProvider` | Azure OpenAI Service |
| `OpenAIProvider` | OpenAI API |
| `ClaudeCliProvider` | Existing CLI wrapper (for backward compatibility) |

### 4.3 Factory

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

## 5. Data Flow

### 5.1 Planning Phase

```
Story (Pending)
    ↓
MortyLoopService.GetNextPendingStory()
    ↓
ClaudeProviderFactory.GetProvider(PlanType.Planning)
    ↓
provider.SendMessageAsync("Analyze PRD and create plan")
    ↓
Save Plan (Type=Planning, ProviderId, Output)
    ↓
Story → Planning
```

### 5.2 Execution Phase

```
Story (Planning)
    ↓
MortyLoopService.ProcessNextIteration()
    ↓
ClaudeProviderFactory.GetProvider(PlanType.Execution)
    ↓
provider.SendMessageAsync("Implement the plan")
    ↓
Save Iteration (ProviderId)
Save ExecutionOutput (ProviderId, Prompt, Response)
    ↓
Story → InProgress/Completed/Failed
```

---

## 6. Database Schema

### New Tables

| Table | Description |
|-------|-------------|
| `Providers` | Provider configurations |
| `ExecutionOutputs` | Detailed execution results |

### Modified Tables

| Table | Changes |
|-------|---------|
| `Iterations` | Add `ProviderId` |
| `Plans` | Add `ProviderId`, `Type`, `Output` |

---

## 7. API Changes

### New Endpoints

```
GET    /api/providers           - List all providers
POST   /api/providers           - Create provider
GET    /api/providers/{id}      - Get provider details
DELETE /api/providers/{id}      - Delete provider
POST   /api/providers/initialize - Initialize from environment
```

---

## 8. Implementation Plan

1. **Add Provider entity and migration**
2. **Create provider interfaces and implementations**
3. **Implement configuration loader from environment**
4. **Update Iteration to track ProviderId**
5. **Add ExecutionOutput entity**
6. **Update MortyLoopService to use providers**
7. **Add provider management API**
8. **Update frontend to show provider info**
