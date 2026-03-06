# 任务: 测试与优化

## 阶段
Phase 8: 测试与优化

## 描述
实现单元测试、集成测试、性能优化和错误处理。

## 验收标准
- [ ] 单元测试覆盖主要模块
- [ ] 集成测试覆盖完整流程
- [ ] 错误处理完善
- [ ] 性能满足要求

## 实现步骤

### 18.1 创建测试项目
```bash
dotnet new xunit -n morty.tests -o tests/morty.tests
dotnet add tests/morty.tests.csproj reference src/cli/morty.cli.csproj
dotnet add tests/morty.tests.csproj reference src/config/morty.config.csproj
dotnet add tests/morty.tests.csproj reference src/llm/morty.llm.csproj
dotnet add tests/morty.tests.csproj reference src/auth/morty.auth.csproj
```

### 18.2 单元测试 - 配置加载
创建 `tests/ConfigTests.cs`:
```csharp
public class ConfigTests
{
    [Fact]
    public void Load_DefaultConfig_ReturnsDefaultValues()
    {
        // Arrange
        var loader = new ConfigLoader();
        
        // Act
        var config = loader.Load();
        
        // Assert
        Assert.Equal("zhipu", config.Provider.Type);
        Assert.Equal("glm-4-plus", config.Provider.Model);
    }
    
    [Fact]
    public void Load_WithCustomConfig_OverridesDefaults()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, @"{
            ""provider"": { ""type"": ""minimax"", ""model"": ""MiniMax-M2"" }
        }");
        
        // Act
        var config = new ConfigLoader(new[] { tempFile }).Load();
        
        // Assert
        Assert.Equal("minimax", config.Provider.Type);
        
        // Cleanup
        File.Delete(tempFile);
    }
}
```

### 18.3 单元测试 - 凭证管理
创建 `tests/AuthTests.cs`:
```csharp
public class AuthTests
{
    [Fact]
    public async Task Login_SavesCredential()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var store = new AuthStore(tempFile);
        var manager = new AuthManager(store);
        
        // Act
        await manager.LoginAsync("zhipu", "test-key-123");
        
        // Assert
        var key = manager.GetApiKey("zhipu");
        Assert.Equal("test-key-123", key);
        
        // Cleanup
        File.Delete(tempFile);
    }
    
    [Fact]
    public async Task Logout_RemovesCredential()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var store = new AuthStore(tempFile);
        var manager = new AuthManager(store);
        await manager.LoginAsync("zhipu", "test-key");
        
        // Act
        await manager.LogoutAsync("zhipu");
        
        // Assert
        var key = manager.GetApiKey("zhipu");
        Assert.Null(key);
        
        // Cleanup
        File.Delete(tempFile);
    }
}
```

### 18.4 集成测试 - 完整对话流程
创建 `tests/IntegrationTests.cs`:
```csharp
public class IntegrationTests
{
    [Fact]
    public async Task FullConversation_Works()
    {
        // Arrange
        var config = new MortyConfig
        {
            Provider = new ProviderConfig { Type = "zhipu", Model = "glm-4-flash" }
        };
        
        var auth = new AuthManager();
        var apiKey = auth.GetApiKey("zhipu");
        
        var provider = ProviderFactory.Create(config.Provider.Type, apiKey!);
        var sessionManager = new SessionManager("~/.morty/sessions");
        var agent = new CodingAgent(provider, sessionManager);
        
        // Act
        var response = await agent.PromptAsync("Hello, what is 1+1?");
        
        // Assert
        Assert.NotNull(response.Content);
        Assert.Contains("2", response.Content);
    }
}
```

### 18.5 错误处理测试
创建 `tests/ErrorHandlingTests.cs`:
```csharp
public class ErrorHandlingTests
{
    [Fact]
    public async Task InvalidApiKey_ThrowsException()
    {
        // Arrange
        var provider = new ZhipuProvider("invalid-key");
        
        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.ChatAsync(new ChatRequest
            {
                Model = "glm-4-flash",
                Messages = new List<ChatMessage>
                {
                    new() { Role = "user", Content = "hi" }
                }
            }));
    }
    
    [Fact]
    public void UnauthorizedPath_ThrowsException()
    {
        // Arrange
        var tools = new FileTools("/home/user/project");
        
        // Act & Assert
        Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tools.Read("/etc/passwd"));
    }
}
```

### 18.6 性能测试
创建 `tests/PerformanceTests.cs`:
```csharp
public class PerformanceTests
{
    [Fact]
    public async Task ContextCompression_ReducesTokens()
    {
        // Arrange
        var compactor = new ContextCompactor();
        var history = GenerateLargeHistory(100);
        
        // Act
        var compressed = await compactor.CompressAsync(history, 10000, mockAgent);
        
        // Assert
        Assert.True(EstimateTokens(compressed) < EstimateTokens(history));
    }
}
```

## 测试覆盖率目标

| 模块 | 目标覆盖率 |
|------|-----------|
| Config | 80% |
| Auth | 90% |
| LLM Providers | 70% |
| Tools | 80% |
| Agent | 60% |

## 相关文件
- tasks/01-create-project-structure.md
- design/dotnet-coding-agent.md (7. 实现计划 Phase 8)
