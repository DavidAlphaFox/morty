// =============================================================================
// SQLite 会话存储
// =============================================================================
// 会话存储后端，使用 SQLite 提供高效查询
// 保留 JSONL 读取能力 (向后兼容)
// =============================================================================

using Microsoft.Data.Sqlite;
using Morty.LLM;

namespace Morty.Agent;

/// <summary>
/// SQLite 会话存储
/// </summary>
public class SessionStore : IDisposable
{
    private readonly SqliteConnection _db;

    public SessionStore(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _db = new SqliteConnection($"Data Source={dbPath}");
        _db.Open();
        Migrate();
    }

    /// <summary>
    /// 创建表结构
    /// </summary>
    private void Migrate()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                cwd TEXT NOT NULL,
                name TEXT,
                parent_id TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS messages (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL REFERENCES sessions(id),
                parent_id TEXT,
                role TEXT NOT NULL,
                content TEXT,
                timestamp TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_messages_session ON messages(session_id);

            CREATE TABLE IF NOT EXISTS permissions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                project_path TEXT NOT NULL,
                permission TEXT NOT NULL,
                pattern TEXT NOT NULL,
                action TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_permissions_project ON permissions(project_path);
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 创建会话
    /// </summary>
    public async Task<string> CreateSessionAsync(string cwd, string? name = null)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var now = DateTime.UtcNow.ToString("o");

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT INTO sessions (id, cwd, name, created_at, updated_at) VALUES ($id, $cwd, $name, $now, $now)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$cwd", cwd);
        cmd.Parameters.AddWithValue("$name", (object?)name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$now", now);
        await cmd.ExecuteNonQueryAsync();

        return id;
    }

    /// <summary>
    /// 保存消息
    /// </summary>
    public async Task SaveMessageAsync(string sessionId, ChatMessageContent message, string? parentId = null)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var now = DateTime.UtcNow.ToString("o");

        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO messages (id, session_id, parent_id, role, content, timestamp)
            VALUES ($id, $sessionId, $parentId, $role, $content, $timestamp)
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$sessionId", sessionId);
        cmd.Parameters.AddWithValue("$parentId", (object?)parentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$role", message.Role);
        cmd.Parameters.AddWithValue("$content", (object?)message.Content ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$timestamp", now);
        await cmd.ExecuteNonQueryAsync();

        // 更新会话的 updated_at
        using var updateCmd = _db.CreateCommand();
        updateCmd.CommandText = "UPDATE sessions SET updated_at = $now WHERE id = $id";
        updateCmd.Parameters.AddWithValue("$now", now);
        updateCmd.Parameters.AddWithValue("$id", sessionId);
        await updateCmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 加载会话消息历史
    /// </summary>
    public async Task<List<ChatMessageContent>> LoadHistoryAsync(string sessionId)
    {
        var messages = new List<ChatMessageContent>();

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT role, content FROM messages WHERE session_id = $id ORDER BY timestamp ASC";
        cmd.Parameters.AddWithValue("$id", sessionId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(new ChatMessageContent
            {
                Role = reader.GetString(0),
                Content = reader.IsDBNull(1) ? "" : reader.GetString(1)
            });
        }

        return messages;
    }

    /// <summary>
    /// 列出会话
    /// </summary>
    public async Task<List<SessionInfo>> ListSessionsAsync(string? cwd = null)
    {
        var sessions = new List<SessionInfo>();

        using var cmd = _db.CreateCommand();
        cmd.CommandText = cwd != null
            ? "SELECT id, cwd, name, created_at, updated_at FROM sessions WHERE cwd = $cwd ORDER BY updated_at DESC"
            : "SELECT id, cwd, name, created_at, updated_at FROM sessions ORDER BY updated_at DESC";

        if (cwd != null)
            cmd.Parameters.AddWithValue("$cwd", cwd);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(new SessionInfo
            {
                Id = reader.GetString(0),
                Cwd = reader.GetString(1),
                Name = reader.IsDBNull(2) ? null : reader.GetString(2),
                Created = DateTime.Parse(reader.GetString(3)),
                Modified = DateTime.Parse(reader.GetString(4))
            });
        }

        return sessions;
    }

    /// <summary>
    /// 查找最近的会话
    /// </summary>
    public async Task<string?> FindRecentSessionAsync(string cwd)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT id FROM sessions WHERE cwd = $cwd ORDER BY updated_at DESC LIMIT 1";
        cmd.Parameters.AddWithValue("$cwd", cwd);

        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    /// <summary>
    /// 保存权限决策
    /// </summary>
    public async Task SavePermissionAsync(string projectPath, string permission, string pattern, string action)
    {
        var now = DateTime.UtcNow.ToString("o");

        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO permissions (project_path, permission, pattern, action, created_at)
            VALUES ($projectPath, $permission, $pattern, $action, $now)
            """;
        cmd.Parameters.AddWithValue("$projectPath", projectPath);
        cmd.Parameters.AddWithValue("$permission", permission);
        cmd.Parameters.AddWithValue("$pattern", pattern);
        cmd.Parameters.AddWithValue("$action", action);
        cmd.Parameters.AddWithValue("$now", now);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 加载项目权限决策
    /// </summary>
    public async Task<List<(string permission, string pattern, string action)>> LoadPermissionsAsync(string projectPath)
    {
        var permissions = new List<(string, string, string)>();

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT permission, pattern, action FROM permissions WHERE project_path = $path";
        cmd.Parameters.AddWithValue("$path", projectPath);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            permissions.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return permissions;
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
