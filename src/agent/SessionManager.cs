// =============================================================================
// 会话管理器
// =============================================================================
// 负责会话的创建、保存、加载和分支
// 会话存储格式: JSONL (每行一个 JSON 消息)
// 支持树形结构的会话条目（用于分支和上下文压缩）
// =============================================================================

using System.Text.Json;
using System.Text.RegularExpressions;
using Morty.Config;
using Morty.LLM;

namespace Morty.Agent;

/// <summary>
/// 会话管理器 - 以追加式树形结构管理对话会话（JSONL 文件）
/// 
/// 每个条目有 id 和 parentId 形成树结构，"叶节点"指针跟踪当前位置。
/// 追加操作创建当前叶节点的子节点，分支操作将叶节点移到早期条目，
/// 在不修改历史的前提下创建新分支。
/// 
/// 使用 BuildSessionContext() 获取发送给 LLM 的已解析消息列表。
/// </summary>
public class SessionManager
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    private string _sessionId = "";

    /// <summary>
    /// 会话文件路径
    /// </summary>
    private string? _sessionFile;

    /// <summary>
    /// 会话存储目录
    /// </summary>
    private readonly string _sessionDir;

    /// <summary>
    /// 工作目录
    /// </summary>
    private readonly string _cwd;

    /// <summary>
    /// 是否持久化
    /// </summary>
    private readonly bool _persist;

    /// <summary>
    /// 是否已刷新到磁盘
    /// </summary>
    private bool _flushed = false;

    /// <summary>
    /// 文件条目列表
    /// </summary>
    private List<object> _fileEntries = new();

    /// <summary>
    /// ID 到条目的映射
    /// </summary>
    private Dictionary<string, SessionEntry> _byId = new();

    /// <summary>
    /// 标签映射 (targetId -> label)
    /// </summary>
    private Dictionary<string, string> _labelsById = new();

    /// <summary>
    /// 当前叶节点 ID
    /// </summary>
    private string? _leafId;

    /// <summary>
    /// JSON 序列化选项
    /// </summary>
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// 生成唯一短 ID（8 位十六进制字符，带碰撞检测）
    /// </summary>
    private string GenerateId()
    {
        for (int i = 0; i < 100; i++)
        {
            var id = Guid.NewGuid().ToString("N")[..8];
            if (!_byId.ContainsKey(id)) return id;
        }
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 初始化会话管理器
    /// </summary>
    /// <param name="cwd">工作目录</param>
    /// <param name="sessionDir">会话存储目录</param>
    /// <param name="sessionFile">会话文件路径 (可选)</param>
    /// <param name="persist">是否持久化</param>
    private SessionManager(string cwd, string sessionDir, string? sessionFile, bool persist)
    {
        _cwd = cwd;
        _sessionDir = sessionDir;
        _persist = persist;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        if (persist && !string.IsNullOrEmpty(sessionDir) && !Directory.Exists(sessionDir))
        {
            Directory.CreateDirectory(sessionDir);
        }

        if (!string.IsNullOrEmpty(sessionFile))
        {
            SetSessionFile(sessionFile);
        }
        else
        {
            NewSession();
        }
    }

    /// <summary>
    /// 切换到不同的会话文件（用于恢复和分支）
    /// </summary>
    /// <param name="sessionFile">会话文件路径</param>
    public void SetSessionFile(string sessionFile)
    {
        _sessionFile = Path.GetFullPath(sessionFile);

        if (File.Exists(_sessionFile))
        {
            _fileEntries = LoadEntriesFromFile(_sessionFile);

            if (_fileEntries.Count == 0)
            {
                var explicitPath = _sessionFile;
                NewSession();
                _sessionFile = explicitPath;
                RewriteFile();
                _flushed = true;
                return;
            }

            var header = _fileEntries.OfType<SessionHeader>().FirstOrDefault();
            _sessionId = header?.Id ?? Guid.NewGuid().ToString("N");

            if (MigrateToCurrentVersion())
            {
                RewriteFile();
            }

            BuildIndex();
            _flushed = true;
        }
        else
        {
            var explicitPath = _sessionFile;
            NewSession();
            _sessionFile = explicitPath;
        }
    }

    /// <summary>
    /// 创建新会话
    /// </summary>
    /// <param name="parentSession">父会话路径 (可选)</param>
    /// <returns>会话文件路径</returns>
    public string NewSession(string? parentSession = null)
    {
        _sessionId = Guid.NewGuid().ToString("N");
        var timestamp = DateTime.UtcNow.ToString("o");

        var header = new SessionHeader
        {
            Type = "session",
            Version = SessionConstants.CurrentVersion,
            Id = _sessionId,
            Timestamp = timestamp,
            Cwd = _cwd,
            ParentSession = parentSession
        };

        _fileEntries = new List<object> { header };
        _byId.Clear();
        _labelsById.Clear();
        _leafId = null;
        _flushed = false;

        if (_persist)
        {
            var fileTimestamp = timestamp.Replace(":", "-").Replace(".", "-");
            _sessionFile = Path.Combine(_sessionDir, $"{fileTimestamp}_{_sessionId}.jsonl");
        }

        return _sessionFile ?? "";
    }

    /// <summary>
    /// 构建 ID 索引
    /// </summary>
    private void BuildIndex()
    {
        _byId.Clear();
        _labelsById.Clear();
        _leafId = null;

        foreach (var entry in _fileEntries)
        {
            if (entry is SessionHeader) continue;

            if (entry is SessionEntry se)
            {
                _byId[se.Id] = se;
                _leafId = se.Id;

                if (entry is LabelEntry le)
                {
                    if (!string.IsNullOrEmpty(le.Label))
                    {
                        _labelsById[le.TargetId] = le.Label;
                    }
                    else
                    {
                        _labelsById.Remove(le.TargetId);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 重写文件
    /// </summary>
    private void RewriteFile()
    {
        if (!_persist || string.IsNullOrEmpty(_sessionFile)) return;

        var lines = _fileEntries.Select(e => JsonSerializer.Serialize(e, _jsonOptions));
        File.WriteAllLines(_sessionFile, lines);
    }

    /// <summary>
    /// 是否持久化
    /// </summary>
    public bool IsPersisted() => _persist;

    /// <summary>
    /// 获取工作目录
    /// </summary>
    public string GetCwd() => _cwd;

    /// <summary>
    /// 获取会话目录
    /// </summary>
    public string GetSessionDir() => _sessionDir;

    /// <summary>
    /// 获取会话 ID
    /// </summary>
    public string GetSessionId() => _sessionId;

    /// <summary>
    /// 获取会话文件路径
    /// </summary>
    public string? GetSessionFile() => _sessionFile;

    // =========================================================================
    // 向后兼容方法 (供 CodingAgent 使用)
    // =========================================================================

    /// <summary>
    /// 获取当前会话 (向后兼容方法)
    /// </summary>
    /// <returns>会话信息</returns>
    public Session GetCurrentSession()
    {
        return new Session
        {
            Id = _sessionId,
            WorkingDirectory = _cwd,
            CreatedAt = !string.IsNullOrEmpty(GetHeader()?.Timestamp) 
                ? DateTimeOffset.Parse(GetHeader()!.Timestamp!).ToUnixTimeSeconds() 
                : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            LastMessageId = _leafId
        };
    }

    /// <summary>
    /// 添加消息到会话 (向后兼容方法)
    /// </summary>
    /// <param name="session">会话</param>
    /// <param name="message">消息内容</param>
    public Task AddMessageAsync(Session session, ChatMessageContent message)
    {
        AppendMessage(message);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 加载会话历史 (向后兼容方法)
    /// </summary>
    /// <param name="session">会话</param>
    /// <returns>消息列表</returns>
    public Task<List<ChatMessageContent>> LoadHistoryAsync(Session session)
    {
        var context = BuildSessionContext();
        return Task.FromResult(context.Messages);
    }

    /// <summary>
    /// 持久化条目
    /// </summary>
    private void Persist(SessionEntry entry)
    {
        if (!_persist || string.IsNullOrEmpty(_sessionFile)) return;

        var hasAssistant = _fileEntries.OfType<SessionEntry>()
            .Any(e => e.Type == "message" && ((SessionMessageEntryWrapper)e).Base.Message?.Role == "assistant");

        if (!hasAssistant)
        {
            _flushed = false;
            return;
        }

        if (!_flushed)
        {
            var lines = _fileEntries.Select(e => JsonSerializer.Serialize(e, _jsonOptions));
            File.AppendAllLines(_sessionFile, lines);
            _flushed = true;
        }
        else
        {
            var line = JsonSerializer.Serialize(entry, _jsonOptions);
            File.AppendAllText(_sessionFile, line + "\n");
        }
    }

    /// <summary>
    /// 添加条目
    /// </summary>
    private void AppendEntry(SessionEntry entry)
    {
        _fileEntries.Add(entry);
        _byId[entry.Id] = entry;
        _leafId = entry.Id;
        Persist(entry);
    }

    /// <summary>
    /// 添加消息作为当前叶节点的子节点，然后推进叶节点
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <returns>条目 ID</returns>
    public string AppendMessage(ChatMessageContent message)
    {
        var entry = new SessionMessageEntryWrapper(new SessionMessageEntry
        {
            Id = GenerateId(),
            ParentId = _leafId,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Message = message
        });

        AppendEntry(entry);
        return entry.Id;
    }

    /// <summary>
    /// 添加思考级别变更作为当前叶节点的子节点
    /// </summary>
    /// <param name="thinkingLevel">思考级别</param>
    /// <returns>条目 ID</returns>
    public string AppendThinkingLevelChange(string thinkingLevel)
    {
        var entry = new ThinkingLevelChangeEntryWrapper(new ThinkingLevelChangeEntry
        {
            Id = GenerateId(),
            ParentId = _leafId,
            Timestamp = DateTime.UtcNow.ToString("o"),
            ThinkingLevel = thinkingLevel
        });

        AppendEntry(entry);
        return entry.Id;
    }

    /// <summary>
    /// 添加模型变更作为当前叶节点的子节点
    /// </summary>
    /// <param name="provider">提供商</param>
    /// <param name="modelId">模型 ID</param>
    /// <returns>条目 ID</returns>
    public string AppendModelChange(string provider, string modelId)
    {
        var entry = new ModelChangeEntryWrapper(new ModelChangeEntry
        {
            Id = GenerateId(),
            ParentId = _leafId,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Provider = provider,
            ModelId = modelId
        });

        AppendEntry(entry);
        return entry.Id;
    }

    /// <summary>
    /// 添加压缩摘要作为当前叶节点的子节点
    /// </summary>
    /// <param name="summary">摘要内容</param>
    /// <param name="firstKeptEntryId">保留的第一个条目 ID</param>
    /// <param name="tokensBefore">压缩前的 token 数量</param>
    /// <param name="details">扩展特定数据</param>
    /// <param name="fromHook">是否来自扩展</param>
    /// <returns>条目 ID</returns>
    public string AppendCompaction(string summary, string firstKeptEntryId, int tokensBefore, object? details = null, bool fromHook = false)
    {
        var entry = new CompactionEntryWrapper(new CompactionEntry
        {
            Id = GenerateId(),
            ParentId = _leafId,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Summary = summary,
            FirstKeptEntryId = firstKeptEntryId,
            TokensBefore = tokensBefore,
            Details = details,
            FromHook = fromHook
        });

        AppendEntry(entry);
        return entry.Id;
    }

    /// <summary>
    /// 添加自定义条目（用于扩展）
    /// </summary>
    /// <param name="customType">扩展类型标识</param>
    /// <param name="data">自定义数据</param>
    /// <returns>条目 ID</returns>
    public string AppendCustomEntry(string customType, object? data = null)
    {
        var entry = new CustomEntryWrapper(new CustomEntry
        {
            Id = GenerateId(),
            ParentId = _leafId,
            Timestamp = DateTime.UtcNow.ToString("o"),
            CustomType = customType,
            Data = data
        });

        AppendEntry(entry);
        return entry.Id;
    }

    /// <summary>
    /// 添加会话信息条目（如显示名称）
    /// </summary>
    /// <param name="name">显示名称</param>
    /// <returns>条目 ID</returns>
    public string AppendSessionInfo(string name)
    {
        var entry = new SessionInfoEntryWrapper(new SessionInfoEntry
        {
            Id = GenerateId(),
            ParentId = _leafId,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Name = name.Trim()
        });

        AppendEntry(entry);
        return entry.Id;
    }

    /// <summary>
    /// 获取当前会话名称（最新的 session_info 条目）
    /// </summary>
    public string? GetSessionName()
    {
        var entries = GetEntries();
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry is SessionInfoEntryWrapper sie && !string.IsNullOrEmpty(sie.Base.Name))
            {
                return sie.Base.Name;
            }
        }
        return null;
    }

    /// <summary>
    /// 添加自定义消息条目（用于扩展，会参与 LLM 上下文）
    /// </summary>
    /// <param name="customType">扩展类型标识</param>
    /// <param name="content">消息内容</param>
    /// <param name="display">是否在 TUI 中显示</param>
    /// <param name="details">扩展特定元数据</param>
    /// <returns>条目 ID</returns>
    public string AppendCustomMessageEntry(string customType, string content, bool display, object? details = null)
    {
        var entry = new CustomMessageEntryWrapper(new CustomMessageEntry
        {
            Id = GenerateId(),
            ParentId = _leafId,
            Timestamp = DateTime.UtcNow.ToString("o"),
            CustomType = customType,
            Content = content,
            Display = display,
            Details = details
        });

        AppendEntry(entry);
        return entry.Id;
    }

    // =========================================================================
    // 树遍历
    // =========================================================================

    /// <summary>
    /// 获取当前叶节点 ID
    /// </summary>
    public string? GetLeafId() => _leafId;

    /// <summary>
    /// 获取当前叶节点条目
    /// </summary>
    public SessionEntry? GetLeafEntry() => _leafId != null && _byId.TryGetValue(_leafId, out var entry) ? entry : null;

    /// <summary>
    /// 获取指定 ID 的条目
    /// </summary>
    public SessionEntry? GetEntry(string id) => _byId.TryGetValue(id, out var entry) ? entry : null;

    /// <summary>
    /// 获取指定条目的所有直接子节点
    /// </summary>
    public List<SessionEntry> GetChildren(string parentId)
    {
        return _byId.Values.Where(e => e.ParentId == parentId).ToList();
    }

    /// <summary>
    /// 获取指定条目的标签
    /// </summary>
    public string? GetLabel(string id) => _labelsById.TryGetValue(id, out var label) ? label : null;

    /// <summary>
    /// 设置或清除条目的标签
    /// </summary>
    /// <param name="targetId">目标条目 ID</param>
    /// <param name="label">标签内容（传递 null 或空字符串以清除）</param>
    /// <returns>条目 ID</returns>
    public string AppendLabelChange(string targetId, string? label)
    {
        if (!_byId.ContainsKey(targetId))
        {
            throw new InvalidOperationException($"Entry {targetId} not found");
        }

        var entry = new LabelEntryWrapper(new LabelEntry
        {
            Id = GenerateId(),
            ParentId = _leafId,
            Timestamp = DateTime.UtcNow.ToString("o"),
            TargetId = targetId,
            Label = label
        });

        AppendEntry(entry);

        if (!string.IsNullOrEmpty(label))
        {
            _labelsById[targetId] = label;
        }
        else
        {
            _labelsById.Remove(targetId);
        }

        return entry.Id;
    }

    /// <summary>
    /// 从指定条目（或叶节点）到根节点遍历，返回路径上的所有条目
    /// </summary>
    /// <param name="fromId">起始条目 ID（可选，默认从叶节点开始）</param>
    /// <returns>路径上的条目列表（按时间顺序）</returns>
    public List<SessionEntry> GetBranch(string? fromId = null)
    {
        var path = new List<SessionEntry>();
        var startId = fromId ?? _leafId;

        if (string.IsNullOrEmpty(startId)) return path;

        var current = _byId.GetValueOrDefault(startId);
        while (current != null)
        {
            path.Insert(0, current);
            current = !string.IsNullOrEmpty(current.ParentId) ? _byId.GetValueOrDefault(current.ParentId) : null;
        }

        return path;
    }

    /// <summary>
    /// 构建会话上下文（发送给 LLM 的消息列表）
    /// </summary>
    public SessionContext BuildSessionContext()
    {
        return BuildSessionContext(GetEntries(), _leafId, _byId);
    }

    /// <summary>
    /// 获取会话头部
    /// </summary>
    public SessionHeader? GetHeader()
    {
        return _fileEntries.OfType<SessionHeader>().FirstOrDefault();
    }

    /// <summary>
    /// 获取所有会话条目（排除头部）
    /// </summary>
    public List<SessionEntry> GetEntries()
    {
        return _fileEntries.OfType<SessionEntry>().ToList();
    }

    /// <summary>
    /// 获取会话的树形结构
    /// </summary>
    public List<SessionTreeNode> GetTree()
    {
        var entries = GetEntries();
        var nodeMap = new Dictionary<string, SessionTreeNode>();
        var roots = new List<SessionTreeNode>();

        foreach (var entry in entries)
        {
            var label = _labelsById.GetValueOrDefault(entry.Id);
            nodeMap[entry.Id] = new SessionTreeNode { Entry = entry, Children = new List<SessionTreeNode>(), Label = label };
        }

        foreach (var entry in entries)
        {
            var node = nodeMap[entry.Id];
            if (string.IsNullOrEmpty(entry.ParentId) || entry.ParentId == entry.Id)
            {
                roots.Add(node);
            }
            else if (nodeMap.TryGetValue(entry.ParentId, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        var stack = new Stack<SessionTreeNode>(roots);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            node.Children.Sort((a, b) => DateTime.Parse(a.Entry.Timestamp).CompareTo(DateTime.Parse(b.Entry.Timestamp)));
            foreach (var child in node.Children)
            {
                stack.Push(child);
            }
        }

        return roots;
    }

    // =========================================================================
    // 分支操作
    // =========================================================================

    /// <summary>
    /// 从早期条目开始新分支
    /// 将叶节点指针移动到指定条目，下次追加操作将创建该条目的子节点
    /// </summary>
    /// <param name="branchFromId">分支起始条目 ID</param>
    public void Branch(string branchFromId)
    {
        if (!_byId.ContainsKey(branchFromId))
        {
            throw new InvalidOperationException($"Entry {branchFromId} not found");
        }
        _leafId = branchFromId;
    }

    /// <summary>
    /// 重置叶节点指针到 null（第一个条目之前）
    /// </summary>
    public void ResetLeaf()
    {
        _leafId = null;
    }

    /// <summary>
    /// 创建带摘要的新分支
    /// 与 Branch() 相同，但还会添加一个 branch_summary 条目来捕获被放弃的对话路径
    /// </summary>
    /// <param name="branchFromId">分支起始条目 ID（null 表示从根开始）</param>
    /// <param name="summary">摘要内容</param>
    /// <param name="details">扩展特定数据</param>
    /// <param name="fromHook">是否来自扩展</param>
    /// <returns>条目 ID</returns>
    public string BranchWithSummary(string? branchFromId, string summary, object? details = null, bool fromHook = false)
    {
        if (branchFromId != null && !_byId.ContainsKey(branchFromId))
        {
            throw new InvalidOperationException($"Entry {branchFromId} not found");
        }

        _leafId = branchFromId;

        var entry = new BranchSummaryEntryWrapper(new BranchSummaryEntry
        {
            Id = GenerateId(),
            ParentId = branchFromId,
            Timestamp = DateTime.UtcNow.ToString("o"),
            FromId = branchFromId ?? "root",
            Summary = summary,
            Details = details,
            FromHook = fromHook
        });

        AppendEntry(entry);
        return entry.Id;
    }

    /// <summary>
    /// 创建新会话文件，只包含从根到指定叶节点的路径
    /// </summary>
    /// <param name="leafId">叶节点 ID</param>
    /// <returns>新会话文件路径（如果不持久化则返回 null）</returns>
    public string? CreateBranchedSession(string leafId)
    {
        var previousSessionFile = _sessionFile;
        var path = GetBranch(leafId);

        if (path.Count == 0)
        {
            throw new InvalidOperationException($"Entry {leafId} not found");
        }

        var pathWithoutLabels = path.Where(e => e.Type != "label").ToList();

        var newSessionId = Guid.NewGuid().ToString("N");
        var timestamp = DateTime.UtcNow.ToString("o");
        var fileTimestamp = timestamp.Replace(":", "-").Replace(".", "-");
        var newSessionFile = Path.Combine(_sessionDir, $"{fileTimestamp}_{newSessionId}.jsonl");

        var header = new SessionHeader
        {
            Type = "session",
            Version = SessionConstants.CurrentVersion,
            Id = newSessionId,
            Timestamp = timestamp,
            Cwd = _cwd,
            ParentSession = _persist ? previousSessionFile : null
        };

        var pathEntryIds = new HashSet<string>(pathWithoutLabels.Select(e => e.Id));
        var labelsToWrite = _labelsById.Where(kv => pathEntryIds.Contains(kv.Key)).ToList();

        if (_persist)
        {
            var labelEntries = new List<LabelEntry>();
            var lastEntryId = pathWithoutLabels.LastOrDefault()?.Id;
            var parentId = lastEntryId;

            foreach (var (targetId, label) in labelsToWrite)
            {
                var labelEntry = new LabelEntry
                {
                    Id = GenerateId(),
                    ParentId = parentId,
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    TargetId = targetId,
                    Label = label
                };
                pathEntryIds.Add(labelEntry.Id);
                labelEntries.Add(labelEntry);
                parentId = labelEntry.Id;
            }

            _fileEntries = new List<object> { header };
            _fileEntries.AddRange(pathWithoutLabels);
            _fileEntries.AddRange(labelEntries);
            _sessionId = newSessionId;
            _sessionFile = newSessionFile;
            BuildIndex();

            var hasAssistant = _fileEntries.OfType<SessionEntry>()
                .Any(e => e.Type == "message" && ((SessionMessageEntryWrapper)e).Base.Message?.Role == "assistant");

            if (hasAssistant)
            {
                RewriteFile();
                _flushed = true;
            }
            else
            {
                _flushed = false;
            }

            return newSessionFile;
        }

        var inMemoryLabelEntries = new List<LabelEntry>();
        var inMemoryLastEntryId = pathWithoutLabels.LastOrDefault()?.Id;
        var inMemoryParentId = inMemoryLastEntryId;

        foreach (var (targetId, label) in labelsToWrite)
        {
            var labelEntry = new LabelEntry
            {
                Id = GenerateId(),
                ParentId = inMemoryParentId,
                Timestamp = DateTime.UtcNow.ToString("o"),
                TargetId = targetId,
                Label = label
            };
            inMemoryLabelEntries.Add(labelEntry);
            inMemoryParentId = labelEntry.Id;
        }

        _fileEntries = new List<object> { header };
        _fileEntries.AddRange(pathWithoutLabels);
        _fileEntries.AddRange(inMemoryLabelEntries);
        _sessionId = newSessionId;
        BuildIndex();

        return null;
    }

    // =========================================================================
    // 静态工厂方法
    // =========================================================================

    /// <summary>
    /// 创建新会话
    /// </summary>
    /// <param name="cwd">工作目录</param>
    /// <param name="sessionDir">会话目录（可选，默认 ~/.morty/sessions/&lt;编码的 cwd&gt;/）</param>
    /// <returns>会话管理器实例</returns>
    public static SessionManager Create(string cwd, string? sessionDir = null)
    {
        var dir = sessionDir ?? GetDefaultSessionDir(cwd);
        return new SessionManager(cwd, dir, null, true);
    }

    /// <summary>
    /// 打开指定会话文件
    /// </summary>
    /// <param name="path">会话文件路径</param>
    /// <param name="sessionDir">会话目录（可选）</param>
    /// <returns>会话管理器实例</returns>
    public static SessionManager Open(string path, string? sessionDir = null)
    {
        var entries = LoadEntriesFromFile(path);
        var header = entries.OfType<SessionHeader>().FirstOrDefault();
        var cwd = header?.Cwd ?? Directory.GetCurrentDirectory();
        var dir = sessionDir ?? Path.GetDirectoryName(path) ?? "";
        return new SessionManager(cwd, dir, path, true);
    }

    /// <summary>
    /// 继续最近的会话，如果没有则创建新会话
    /// </summary>
    /// <param name="cwd">工作目录</param>
    /// <param name="sessionDir">会话目录（可选）</param>
    /// <returns>会话管理器实例</returns>
    public static SessionManager ContinueRecent(string cwd, string? sessionDir = null)
    {
        var dir = sessionDir ?? GetDefaultSessionDir(cwd);
        var mostRecent = FindMostRecentSession(dir);

        if (mostRecent != null)
        {
            return new SessionManager(cwd, dir, mostRecent, true);
        }

        return new SessionManager(cwd, dir, null, true);
    }

    /// <summary>
    /// 创建内存会话（不持久化）
    /// </summary>
    /// <param name="cwd">工作目录（可选）</param>
    /// <returns>会话管理器实例</returns>
    public static SessionManager InMemory(string? cwd = null)
    {
        return new SessionManager(cwd ?? Directory.GetCurrentDirectory(), "", null, false);
    }

    /// <summary>
    /// 从另一个项目目录将会话分支到当前项目
    /// </summary>
    /// <param name="sourcePath">源会话文件路径</param>
    /// <param name="targetCwd">目标工作目录</param>
    /// <param name="sessionDir">会话目录（可选）</param>
    /// <returns>会话管理器实例</returns>
    public static SessionManager ForkFrom(string sourcePath, string targetCwd, string? sessionDir = null)
    {
        var sourceEntries = LoadEntriesFromFile(sourcePath);

        if (sourceEntries.Count == 0)
        {
            throw new InvalidOperationException($"Cannot fork: source session file is empty or invalid: {sourcePath}");
        }

        var sourceHeader = sourceEntries.OfType<SessionHeader>().FirstOrDefault();
        if (sourceHeader == null)
        {
            throw new InvalidOperationException($"Cannot fork: source session has no header: {sourcePath}");
        }

        var dir = sessionDir ?? GetDefaultSessionDir(targetCwd);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var newSessionId = Guid.NewGuid().ToString("N");
        var timestamp = DateTime.UtcNow.ToString("o");
        var fileTimestamp = timestamp.Replace(":", "-").Replace(".", "-");
        var newSessionFile = Path.Combine(dir, $"{fileTimestamp}_{newSessionId}.jsonl");

        var newHeader = new SessionHeader
        {
            Type = "session",
            Version = SessionConstants.CurrentVersion,
            Id = newSessionId,
            Timestamp = timestamp,
            Cwd = targetCwd,
            ParentSession = sourcePath
        };

        var lines = new List<string> { JsonSerializer.Serialize(newHeader, new JsonSerializerOptions { WriteIndented = false }) };
        foreach (var entry in sourceEntries)
        {
            if (entry is not SessionHeader)
            {
                lines.Add(JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = false }));
            }
        }

        File.WriteAllLines(newSessionFile, lines);

        return new SessionManager(targetCwd, dir, newSessionFile, true);
    }

    /// <summary>
    /// 列出指定目录的所有会话
    /// </summary>
    /// <param name="cwd">工作目录</param>
    /// <param name="sessionDir">会话目录（可选）</param>
    /// <returns>会话信息列表</returns>
    public static async Task<List<SessionInfo>> List(string cwd, string? sessionDir = null)
    {
        var dir = sessionDir ?? GetDefaultSessionDir(cwd);
        var sessions = await ListSessionsFromDir(dir);
        sessions.Sort((a, b) => b.Modified.CompareTo(a.Modified));
        return sessions;
    }

    /// <summary>
    /// 列出所有项目目录的会话
    /// </summary>
    /// <returns>会话信息列表</returns>
    public static async Task<List<SessionInfo>> ListAll()
    {
        var sessionsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".morty", "agent", "sessions");

        if (!Directory.Exists(sessionsDir))
        {
            return new List<SessionInfo>();
        }

        var dirs = Directory.GetDirectories(sessionsDir);
        var allSessions = new List<SessionInfo>();

        foreach (var dir in dirs)
        {
            var sessions = await ListSessionsFromDir(dir);
            allSessions.AddRange(sessions);
        }

        allSessions.Sort((a, b) => b.Modified.CompareTo(a.Modified));
        return allSessions;
    }

    // =========================================================================
    // 辅助方法
    // =========================================================================

    /// <summary>
    /// 计算给定 cwd 的默认会话目录
    /// </summary>
    private static string GetDefaultSessionDir(string cwd)
    {
        var safePath = $"--{cwd.TrimStart('/').Replace("/", "-").Replace(":", "-")}--";
        var sessionDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".morty", "agent", "sessions", safePath);

        if (!Directory.Exists(sessionDir))
        {
            Directory.CreateDirectory(sessionDir);
        }

        return sessionDir;
    }

    /// <summary>
    /// 从文件加载会话条目
    /// </summary>
    private static List<object> LoadEntriesFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return new List<object>();

        var entries = new List<object>();
        var lines = File.ReadAllLines(filePath);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var entry = JsonSerializer.Deserialize<object>(line);
                if (entry != null) entries.Add(entry);
            }
            catch
            {
                // Skip malformed lines
            }
        }

        if (entries.Count == 0) return entries;

        var header = entries.FirstOrDefault(e => e is System.Text.Json.JsonElement je && je.TryGetProperty("type", out var t) && t.GetString() == "session");
        if (header == null || (header is System.Text.Json.JsonElement jhe && !jhe.TryGetProperty("id", out _)))
        {
            return new List<object>();
        }

        return entries;
    }

    /// <summary>
    /// 检查会话文件是否有效
    /// </summary>
    private static bool IsValidSessionFile(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            var buffer = new byte[512];
            var bytesRead = fs.Read(buffer, 0, 512);
            if (bytesRead == 0) return false;

            var firstLine = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead).Split('\n')[0];
            if (string.IsNullOrEmpty(firstLine)) return false;

            using var doc = System.Text.Json.JsonDocument.Parse(firstLine);
            return doc.RootElement.TryGetProperty("type", out var type) && type.GetString() == "session" &&
                   doc.RootElement.TryGetProperty("id", out var id) && id.GetString() != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 查找最近修改的会话文件
    /// </summary>
    private static string? FindMostRecentSession(string sessionDir)
    {
        try
        {
            if (!Directory.Exists(sessionDir)) return null;

            var files = Directory.GetFiles(sessionDir, "*.jsonl")
                .Where(IsValidSessionFile)
                .Select(f => new { Path = f, Mtime = File.GetLastWriteTime(f) })
                .OrderByDescending(f => f.Mtime)
                .ToList();

            return files.FirstOrDefault()?.Path;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从目录列出会话
    /// </summary>
    private static async Task<List<SessionInfo>> ListSessionsFromDir(string dir)
    {
        var sessions = new List<SessionInfo>();

        if (!Directory.Exists(dir))
        {
            return sessions;
        }

        try
        {
            var files = Directory.GetFiles(dir, "*.jsonl");
            var tasks = files.Select(BuildSessionInfo);
            var results = await Task.WhenAll(tasks);

            foreach (var info in results.Where(i => i != null))
            {
                sessions.Add(info!);
            }
        }
        catch
        {
            // Return empty list on error
        }

        return sessions;
    }

    /// <summary>
    /// 构建会话信息
    /// </summary>
    private static async Task<SessionInfo?> BuildSessionInfo(string filePath)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var entries = new List<object>();
            var lines = content.Split('\n');

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    entries.Add(JsonSerializer.Deserialize<object>(line)!);
                }
                catch
                {
                    // Skip malformed lines
                }
            }

            if (entries.Count == 0) return null;

            var header = entries.OfType<SessionHeader>().FirstOrDefault();
            if (header == null)
            {
                var firstEntry = entries.FirstOrDefault();
                if (firstEntry is System.Text.Json.JsonElement je && je.TryGetProperty("type", out var t) && t.GetString() == "session")
                {
                    header = new SessionHeader
                    {
                        Id = je.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        Timestamp = je.TryGetProperty("timestamp", out var ts) ? ts.GetString() ?? "" : "",
                        Cwd = je.TryGetProperty("cwd", out var cwd) ? cwd.GetString() ?? "" : "",
                        ParentSession = je.TryGetProperty("parentSession", out var ps) ? ps.GetString() : null
                    };
                }
            }

            if (header == null) return null;

            var fileInfo = new FileInfo(filePath);
            var messageCount = 0;
            var firstMessage = "";
            var allMessages = new List<string>();
            string? name = null;

            foreach (var entry in entries)
            {
                if (entry is SessionInfoEntry sie && !string.IsNullOrEmpty(sie.Name))
                {
                    name = sie.Name.Trim();
                }

                if (entry is SessionMessageEntryWrapper sme && sme.Base.Message != null)
                {
                    messageCount++;

                    var role = sme.Base.Message.Role;
                    var msgContent = sme.Base.Message.Content;

                    if (!string.IsNullOrEmpty(msgContent) && (role == "user" || role == "assistant"))
                    {
                        allMessages.Add(msgContent);
                        if (string.IsNullOrEmpty(firstMessage) && role == "user")
                        {
                            firstMessage = msgContent;
                        }
                    }
                }
            }

            var parentSessionPath = header.ParentSession;
            var created = !string.IsNullOrEmpty(header.Timestamp) ? DateTime.Parse(header.Timestamp) : DateTime.MinValue;
            var modified = fileInfo.LastWriteTime;

            return new SessionInfo
            {
                Path = filePath,
                Id = header.Id,
                Cwd = header.Cwd,
                Name = name,
                ParentSessionPath = parentSessionPath,
                Created = created,
                Modified = modified,
                MessageCount = messageCount,
                FirstMessage = string.IsNullOrEmpty(firstMessage) ? "(no messages)" : firstMessage,
                AllMessagesText = string.Join(" ", allMessages)
            };
        }
        catch
        {
            return null;
        }
    }

    // =========================================================================
    // 版本迁移
    // =========================================================================

    /// <summary>
    /// 迁移到当前版本
    /// </summary>
    private bool MigrateToCurrentVersion()
    {
        var header = _fileEntries.OfType<SessionHeader>().FirstOrDefault();
        var version = header?.Version ?? 1;

        if (version >= SessionConstants.CurrentVersion) return false;

        if (version < 2) MigrateV1ToV2();
        if (version < 3) MigrateV2ToV3();

        return true;
    }

    /// <summary>
    /// 迁移 v1 → v2：添加 id/parentId 树形结构
    /// </summary>
    private void MigrateV1ToV2()
    {
        var ids = new HashSet<string>();
        string? prevId = null;

        foreach (var entry in _fileEntries)
        {
            if (entry is SessionHeader h)
            {
                h.Version = 2;
                continue;
            }

            if (entry is SessionEntry se)
            {
                se.Id = GenerateId();
                se.ParentId = prevId;
                prevId = se.Id;
            }
        }
    }

    /// <summary>
    /// 迁移 v2 → v3：将 hookMessage 角色重命名为 custom
    /// </summary>
    private void MigrateV2ToV3()
    {
        foreach (var entry in _fileEntries)
        {
            if (entry is SessionHeader h)
            {
                h.Version = 3;
                continue;
            }

            if (entry is SessionMessageEntryWrapper sme && sme.Base.Message != null)
            {
                if (sme.Base.Message.Role == "hookMessage")
                {
                    sme.Base.Message.Role = "custom";
                }
            }
        }
    }

    // =========================================================================
    // 上下文构建
    // =========================================================================

    /// <summary>
    /// 通过树遍历构建会话上下文
    /// </summary>
    private static SessionContext BuildSessionContext(
        List<SessionEntry> entries,
        string? leafId,
        Dictionary<string, SessionEntry> byId)
    {
        SessionEntry? leaf = null;

        if (leafId == null)
        {
            return new SessionContext { Messages = new List<ChatMessageContent>(), ThinkingLevel = "off", Model = null };
        }

        if (byId.TryGetValue(leafId, out var found))
        {
            leaf = found;
        }

        if (leaf == null)
        {
            leaf = entries.LastOrDefault();
        }

        if (leaf == null)
        {
            return new SessionContext { Messages = new List<ChatMessageContent>(), ThinkingLevel = "off", Model = null };
        }

        var path = new List<SessionEntry>();
        var current = leaf;
        while (current != null)
        {
            path.Insert(0, current);
            current = !string.IsNullOrEmpty(current.ParentId) ? byId.GetValueOrDefault(current.ParentId) : null;
        }

        var thinkingLevel = "off";
        ModelInfo? model = null;
        CompactionEntry? compaction = null;

        foreach (var entry in path)
        {
            switch (entry)
            {
                case ThinkingLevelChangeEntryWrapper t:
                    thinkingLevel = t.Base.ThinkingLevel;
                    break;
                case ModelChangeEntryWrapper m:
                    model = new ModelInfo { Provider = m.Base.Provider, ModelId = m.Base.ModelId };
                    break;
                case SessionMessageEntryWrapper s when s.Base.Message?.Role == "assistant":
                    model = new ModelInfo
                    {
                        Provider = s.Base.Message.Provider ?? "",
                        ModelId = s.Base.Message.Model ?? ""
                    };
                    break;
                case CompactionEntryWrapper c:
                    compaction = c.Base;
                    break;
            }
        }

        var messages = new List<ChatMessageContent>();

        void AppendMessage(SessionEntry entry)
        {
            switch (entry)
            {
                case SessionMessageEntryWrapper m:
                    if (m.Base.Message != null)
                    {
                        messages.Add(m.Base.Message);
                    }
                    break;
                case CustomMessageEntryWrapper c:
                    messages.Add(new ChatMessageContent
                    {
                        Role = "user",
                        Content = c.Base.Content
                    });
                    break;
                case BranchSummaryEntryWrapper b when !string.IsNullOrEmpty(b.Base.Summary):
                    messages.Add(new ChatMessageContent
                    {
                        Role = "user",
                        Content = $"[Branch Summary from {b.Base.FromId}]: {b.Base.Summary}"
                    });
                    break;
            }
        }

        if (compaction != null)
        {
            messages.Add(new ChatMessageContent
            {
                Role = "user",
                Content = $"[Previous conversation summarized. {compaction.TokensBefore} tokens before compression]: {compaction.Summary}"
            });

            var compactionIdx = path.FindIndex(e => e.Type == "compaction" && e.Id == compaction.Id);
            var foundFirstKept = false;

            for (int i = 0; i < compactionIdx; i++)
            {
                var entry = path[i];
                if (entry.Id == compaction.FirstKeptEntryId)
                {
                    foundFirstKept = true;
                }
                if (foundFirstKept)
                {
                    AppendMessage(entry);
                }
            }

            for (int i = compactionIdx + 1; i < path.Count; i++)
            {
                AppendMessage(path[i]);
            }
        }
        else
        {
            foreach (var entry in path)
            {
                AppendMessage(entry);
            }
        }

        return new SessionContext
        {
            Messages = messages,
            ThinkingLevel = thinkingLevel,
            Model = model
        };
    }
}
