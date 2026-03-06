# 任务 3.7: 数据库存储

## 阶段
Phase 3 — 高级特性

## 目标
会话存储从 JSONL 文件迁移到 SQLite，提高查询效率和数据可靠性。

## 设计方案

### 表结构

```sql
CREATE TABLE sessions (
    id TEXT PRIMARY KEY,
    cwd TEXT NOT NULL,
    name TEXT,
    parent_id TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE messages (
    id TEXT PRIMARY KEY,
    session_id TEXT NOT NULL REFERENCES sessions(id),
    parent_id TEXT,
    role TEXT NOT NULL,
    content TEXT,
    timestamp TEXT NOT NULL
);

CREATE TABLE parts (
    id TEXT PRIMARY KEY,
    message_id TEXT NOT NULL REFERENCES messages(id),
    type TEXT NOT NULL,  -- text, tool_call, tool_result, reasoning
    data TEXT NOT NULL,  -- JSON
    created_at TEXT NOT NULL
);

CREATE TABLE permissions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    project_path TEXT NOT NULL,
    permission TEXT NOT NULL,
    pattern TEXT NOT NULL,
    action TEXT NOT NULL,
    created_at TEXT NOT NULL
);
```

### 使用 Microsoft.Data.Sqlite

```csharp
// src/agent/SessionStore.cs

public class SessionStore : IDisposable
{
    private readonly SqliteConnection _db;

    public SessionStore(string dbPath)
    {
        _db = new SqliteConnection($"Data Source={dbPath}");
        _db.Open();
        Migrate();
    }

    public async Task SaveMessageAsync(string sessionId, SessionMessageEntry entry) { ... }
    public async Task<List<ChatMessageContent>> LoadHistoryAsync(string sessionId) { ... }
    public async Task<List<SessionInfo>> ListSessionsAsync(string? cwd = null) { ... }
}
```

### 迁移策略
- 保留 JSONL 读取能力 (向后兼容)
- 新会话默认用 SQLite
- 提供 `morty session migrate` 命令批量迁移

## 实现步骤

1. [ ] 添加 `Microsoft.Data.Sqlite` NuGet 包
2. [ ] 创建 `src/agent/SessionStore.cs` — SQLite 存储
3. [ ] 实现表创建和迁移
4. [ ] 实现 CRUD 操作
5. [ ] `SessionManager.cs` — 支持 SQLite 后端
6. [ ] JSONL → SQLite 迁移工具
7. [ ] `morty session migrate` CLI 命令

## 验收标准
- [ ] 新会话使用 SQLite 存储
- [ ] 会话查询比 JSONL 快
- [ ] 旧 JSONL 会话仍可读取
- [ ] 迁移命令工作正常

## 参考
- opencode: `src/session/session.sql.ts`

## 相关文件
- `src/agent/SessionStore.cs` — 新建
- `src/agent/SessionManager.cs` — 改造
- `src/cli/Program.cs` — 迁移命令
