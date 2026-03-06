# 任务 4.4: 测试覆盖

## 阶段
Phase 4 — 体验优化

## 目标
为核心模块添加单元测试和集成测试。

## 设计方案

### 测试项目

```bash
dotnet new xunit -n morty.tests -o tests
dotnet sln add tests/morty.tests.csproj
dotnet add tests/morty.tests.csproj reference src/tools/morty.tools.csproj
dotnet add tests/morty.tests.csproj reference src/agent/morty.agent.csproj
```

### 测试范围

#### 工具测试 (单元)
```csharp
public class FileToolsTests
{
    [Fact] public async Task Read_ReturnsContentWithLineNumbers() { ... }
    [Fact] public async Task Read_WithOffset_StartsFromLine() { ... }
    [Fact] public async Task Read_OutsideWorkDir_Throws() { ... }
    [Fact] public async Task Edit_ExactMatch_Replaces() { ... }
    [Fact] public async Task Edit_MultipleMatches_Throws() { ... }
    [Fact] public async Task Edit_NoMatch_Throws() { ... }
    [Fact] public async Task Write_CreatesDirectories() { ... }
}

public class OutputTruncatorTests
{
    [Fact] public void TruncateHead_LargeContent_Truncates() { ... }
    [Fact] public void TruncateTail_LargeContent_KeepsEnd() { ... }
    [Fact] public void SmallContent_NotTruncated() { ... }
}
```

#### Agent 测试 (集成, mock LLM)
```csharp
public class CodingAgentTests
{
    [Fact] public async Task PromptAsync_SimpleResponse_Works() { ... }
    [Fact] public async Task PromptAsync_ToolCall_ExecutesAndContinues() { ... }
    [Fact] public async Task Abort_CancelsOperation() { ... }
    [Fact] public async Task DoomLoop_DetectedAndInterrupted() { ... }
}
```

#### 权限测试
```csharp
public class PermissionCheckerTests
{
    [Fact] public async Task AllowRule_Matches() { ... }
    [Fact] public async Task DenyRule_Blocks() { ... }
    [Fact] public async Task GlobPattern_Matches() { ... }
}

public class BashSafetyTests
{
    [Fact] public void DangerousCommand_DetectedAsHigh() { ... }
    [Fact] public void ReadOnlyCommand_DetectedAsLow() { ... }
    [Fact] public void ForkBomb_DetectedAsCritical() { ... }
}
```

#### 会话测试
```csharp
public class SessionManagerTests
{
    [Fact] public async Task Create_CreatesSessionFile() { ... }
    [Fact] public async Task AddMessage_Persisted() { ... }
    [Fact] public async Task LoadHistory_ReturnsMessages() { ... }
    [Fact] public async Task ContinueRecent_FindsLatest() { ... }
}
```

#### Edit 策略测试
```csharp
public class EditStrategyTests
{
    [Fact] public void ExactMatch_Works() { ... }
    [Fact] public void LineTrimmed_IgnoresTrailingSpaces() { ... }
    [Fact] public void IndentationFlexible_HandlesOffsets() { ... }
    [Fact] public void NoMatch_ReturnsFalse() { ... }
}
```

### Mock LLM Client

```csharp
public class MockChatClient : IChatClient
{
    private readonly Queue<ChatResponse> _responses = new();

    public void EnqueueResponse(string text) { ... }
    public void EnqueueToolCall(string name, Dictionary<string, object?> args) { ... }

    public Task<ChatResponse> GetResponseAsync(...) => Task.FromResult(_responses.Dequeue());
}
```

## 实现步骤

1. [ ] 创建测试项目 `tests/morty.tests.csproj`
2. [ ] 创建 `MockChatClient` 用于 Agent 集成测试
3. [ ] FileTools 单元测试 (临时目录)
4. [ ] OutputTruncator 单元测试
5. [ ] BashSafety 单元测试
6. [ ] PermissionChecker 单元测试
7. [ ] EditStrategy 单元测试
8. [ ] CodingAgent 集成测试
9. [ ] SessionManager 测试
10. [ ] CI 集成 (`dotnet test`)

## 验收标准
- [ ] 核心模块测试覆盖率 > 70%
- [ ] `dotnet test` 全绿
- [ ] Mock LLM 可复用

## 相关文件
- `tests/morty.tests.csproj` — 新建
- `tests/*.cs` — 测试文件
