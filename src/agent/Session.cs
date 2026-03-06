// =============================================================================
// 会话模型
// =============================================================================
// 定义会话、会话条目和消息的数据结构
// 支持树形结构的会话条目（用于分支和上下文压缩）
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;
using Morty.LLM;

namespace Morty.Agent;

/// <summary>
/// 会话常量
/// </summary>
public static class SessionConstants
{
    /// <summary>
    /// 当前会话文件格式版本号
    /// </summary>
    public const int CurrentVersion = 3;
}

/// <summary>
/// 会话文件头部 - JSONL 文件的第一行
/// </summary>
public class SessionHeader
{
    /// <summary>
    /// 类型标识
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "session";

    /// <summary>
    /// 格式版本 (v1 会话没有此字段)
    /// </summary>
    [JsonPropertyName("version")]
    public int? Version { get; set; }

    /// <summary>
    /// 会话 ID
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    /// 时间戳 (ISO 8601)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    /// <summary>
    /// 工作目录
    /// </summary>
    [JsonPropertyName("cwd")]
    public string Cwd { get; set; } = "";

    /// <summary>
    /// 父会话路径 (如果是分支)
    /// </summary>
    [JsonPropertyName("parentSession")]
    public string? ParentSession { get; set; }
}

/// <summary>
/// 会话条目基础结构 - 所有条目类型的公共字段
/// </summary>
public abstract class SessionEntryBase
{
    /// <summary>
    /// 条目类型
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }

    /// <summary>
    /// 条目 ID
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    /// 父条目 ID
    /// </summary>
    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    /// <summary>
    /// 时间戳 (ISO 8601)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}

/// <summary>
/// 消息条目 - 存储 LLM 对话消息
/// </summary>
public class SessionMessageEntry : SessionEntryBase
{
    /// <summary>
    /// 条目类型
    /// </summary>
    public override string Type => "message";

    /// <summary>
    /// 消息内容
    /// </summary>
    [JsonPropertyName("message")]
    public ChatMessageContent? Message { get; set; }
}

/// <summary>
/// 思考级别变更条目
/// </summary>
public class ThinkingLevelChangeEntry : SessionEntryBase
{
    /// <summary>
    /// 条目类型
    /// </summary>
    public override string Type => "thinking_level_change";

    /// <summary>
    /// 思考级别
    /// </summary>
    [JsonPropertyName("thinkingLevel")]
    public string ThinkingLevel { get; set; } = "off";
}

/// <summary>
/// 模型变更条目
/// </summary>
public class ModelChangeEntry : SessionEntryBase
{
    /// <summary>
    /// 条目类型
    /// </summary>
    public override string Type => "model_change";

    /// <summary>
    /// 提供商
    /// </summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "";

    /// <summary>
    /// 模型 ID
    /// </summary>
    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = "";
}

/// <summary>
/// 压缩条目 - 记录上下文压缩操作及摘要
/// </summary>
public class CompactionEntry : SessionEntryBase
{
    /// <summary>
    /// 条目类型
    /// </summary>
    public override string Type => "compaction";

    /// <summary>
    /// 摘要内容
    /// </summary>
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    /// <summary>
    /// 保留的第一个条目 ID
    /// </summary>
    [JsonPropertyName("firstKeptEntryId")]
    public string FirstKeptEntryId { get; set; } = "";

    /// <summary>
    /// 压缩前的 token 数量
    /// </summary>
    [JsonPropertyName("tokensBefore")]
    public int TokensBefore { get; set; }

    /// <summary>
    /// 扩展特定数据
    /// </summary>
    [JsonPropertyName("details")]
    public object? Details { get; set; }

    /// <summary>
    /// 是否来自扩展
    /// </summary>
    [JsonPropertyName("fromHook")]
    public bool FromHook { get; set; }
}

/// <summary>
/// 分支摘要条目 - 切换分支时保存的对话摘要
/// </summary>
public class BranchSummaryEntry : SessionEntryBase
{
    /// <summary>
    /// 条目类型
    /// </summary>
    public override string Type => "branch_summary";

    /// <summary>
    /// 来源条目 ID
    /// </summary>
    [JsonPropertyName("fromId")]
    public string FromId { get; set; } = "";

    /// <summary>
    /// 摘要内容
    /// </summary>
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    /// <summary>
    /// 扩展特定数据 (不发送给 LLM)
    /// </summary>
    [JsonPropertyName("details")]
    public object? Details { get; set; }

    /// <summary>
    /// 是否来自扩展
    /// </summary>
    [JsonPropertyName("fromHook")]
    public bool FromHook { get; set; }
}

/// <summary>
/// 自定义条目 - 用于扩展存储扩展特定数据
/// </summary>
public class CustomEntry : SessionEntryBase
{
    /// <summary>
    /// 条目类型
    /// </summary>
    public override string Type => "custom";

    /// <summary>
    /// 扩展类型标识
    /// </summary>
    [JsonPropertyName("customType")]
    public string CustomType { get; set; } = "";

    /// <summary>
    /// 自定义数据
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

/// <summary>
/// 自定义消息条目 - 用于扩展向 LLM 上下文注入消息
/// </summary>
public class CustomMessageEntry : SessionEntryBase
{
    /// <summary>
    /// 条目类型
    /// </summary>
    public override string Type => "custom_message";

    /// <summary>
    /// 扩展类型标识
    /// </summary>
    [JsonPropertyName("customType")]
    public string CustomType { get; set; } = "";

    /// <summary>
    /// 消息内容
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    /// <summary>
    /// 是否在 TUI 中显示
    /// </summary>
    [JsonPropertyName("display")]
    public bool Display { get; set; } = true;

    /// <summary>
    /// 扩展特定元数据 (不发送给 LLM)
    /// </summary>
    [JsonPropertyName("details")]
    public object? Details { get; set; }
}

/// <summary>
/// 标签条目 - 用户定义的书签/标记
/// </summary>
public class LabelEntry : SessionEntryBase
{
    /// <summary>
    /// 条目类型
    /// </summary>
    public override string Type => "label";

    /// <summary>
    /// 目标条目 ID
    /// </summary>
    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = "";

    /// <summary>
    /// 标签内容
    /// </summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

/// <summary>
/// 会话信息条目 (如用户定义的显示名称)
/// </summary>
public class SessionInfoEntry : SessionEntryBase
{
    /// <summary>
    /// 条目类型
    /// </summary>
    public override string Type => "session_info";

    /// <summary>
    /// 显示名称
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>
/// 会话条目联合类型
/// </summary>
[JsonConverter(typeof(SessionEntryJsonConverter))]
public abstract class SessionEntry
{
    /// <summary>
    /// 条目类型
    /// </summary>
    public abstract string Type { get; }

    /// <summary>
    /// 条目 ID
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// 父条目 ID
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// 时间戳 (ISO 8601)
    /// </summary>
    public string Timestamp { get; set; } = "";

    /// <summary>
    /// 从基础类型转换
    /// </summary>
    public static SessionEntry FromBase(SessionEntryBase baseEntry)
    {
        return baseEntry switch
        {
            SessionMessageEntry m => new SessionMessageEntryWrapper(m),
            ThinkingLevelChangeEntry t => new ThinkingLevelChangeEntryWrapper(t),
            ModelChangeEntry m => new ModelChangeEntryWrapper(m),
            CompactionEntry c => new CompactionEntryWrapper(c),
            BranchSummaryEntry b => new BranchSummaryEntryWrapper(b),
            CustomEntry c => new CustomEntryWrapper(c),
            CustomMessageEntry c => new CustomMessageEntryWrapper(c),
            LabelEntry l => new LabelEntryWrapper(l),
            SessionInfoEntry s => new SessionInfoEntryWrapper(s),
            _ => throw new ArgumentException($"Unknown entry type: {baseEntry.GetType()}")
        };
    }
}

/// <summary>
/// SessionEntry 到 SessionEntryBase 的 JSON 转换器
/// </summary>
public class SessionEntryJsonConverter : JsonConverter<SessionEntry>
{
    public override SessionEntry? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProp))
            return null;

        var type = typeProp.GetString();
        var json = root.GetRawText();

        return type switch
        {
            "message" => new SessionMessageEntryWrapper(JsonSerializer.Deserialize<SessionMessageEntry>(json, options)!),
            "thinking_level_change" => new ThinkingLevelChangeEntryWrapper(JsonSerializer.Deserialize<ThinkingLevelChangeEntry>(json, options)!),
            "model_change" => new ModelChangeEntryWrapper(JsonSerializer.Deserialize<ModelChangeEntry>(json, options)!),
            "compaction" => new CompactionEntryWrapper(JsonSerializer.Deserialize<CompactionEntry>(json, options)!),
            "branch_summary" => new BranchSummaryEntryWrapper(JsonSerializer.Deserialize<BranchSummaryEntry>(json, options)!),
            "custom" => new CustomEntryWrapper(JsonSerializer.Deserialize<CustomEntry>(json, options)!),
            "custom_message" => new CustomMessageEntryWrapper(JsonSerializer.Deserialize<CustomMessageEntry>(json, options)!),
            "label" => new LabelEntryWrapper(JsonSerializer.Deserialize<LabelEntry>(json, options)!),
            "session_info" => new SessionInfoEntryWrapper(JsonSerializer.Deserialize<SessionInfoEntry>(json, options)!),
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, SessionEntry value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case SessionMessageEntryWrapper m:
                JsonSerializer.Serialize(writer, m.Base, options);
                break;
            case ThinkingLevelChangeEntryWrapper t:
                JsonSerializer.Serialize(writer, t.Base, options);
                break;
            case ModelChangeEntryWrapper mc:
                JsonSerializer.Serialize(writer, mc.Base, options);
                break;
            case CompactionEntryWrapper c:
                JsonSerializer.Serialize(writer, c.Base, options);
                break;
            case BranchSummaryEntryWrapper b:
                JsonSerializer.Serialize(writer, b.Base, options);
                break;
            case CustomEntryWrapper ce:
                JsonSerializer.Serialize(writer, ce.Base, options);
                break;
            case CustomMessageEntryWrapper cm:
                JsonSerializer.Serialize(writer, cm.Base, options);
                break;
            case LabelEntryWrapper l:
                JsonSerializer.Serialize(writer, l.Base, options);
                break;
            case SessionInfoEntryWrapper si:
                JsonSerializer.Serialize(writer, si.Base, options);
                break;
        }
    }
}

/// <summary>
/// 会话条目包装器实现
/// </summary>
public class SessionMessageEntryWrapper : SessionEntry
{
    public override string Type => "message";
    public SessionMessageEntry Base { get; }
    public SessionMessageEntryWrapper(SessionMessageEntry baseEntry) => Base = baseEntry;
    public new string Id { get => Base.Id; set => Base.Id = value; }
    public new string? ParentId { get => Base.ParentId; set => Base.ParentId = value; }
    public new string Timestamp { get => Base.Timestamp; set => Base.Timestamp = value; }
}

public class ThinkingLevelChangeEntryWrapper : SessionEntry
{
    public override string Type => "thinking_level_change";
    public ThinkingLevelChangeEntry Base { get; }
    public ThinkingLevelChangeEntryWrapper(ThinkingLevelChangeEntry baseEntry) => Base = baseEntry;
    public new string Id { get => Base.Id; set => Base.Id = value; }
    public new string? ParentId { get => Base.ParentId; set => Base.ParentId = value; }
    public new string Timestamp { get => Base.Timestamp; set => Base.Timestamp = value; }
}

public class ModelChangeEntryWrapper : SessionEntry
{
    public override string Type => "model_change";
    public ModelChangeEntry Base { get; }
    public ModelChangeEntryWrapper(ModelChangeEntry baseEntry) => Base = baseEntry;
    public new string Id { get => Base.Id; set => Base.Id = value; }
    public new string? ParentId { get => Base.ParentId; set => Base.ParentId = value; }
    public new string Timestamp { get => Base.Timestamp; set => Base.Timestamp = value; }
}

public class CompactionEntryWrapper : SessionEntry
{
    public override string Type => "compaction";
    public CompactionEntry Base { get; }
    public CompactionEntryWrapper(CompactionEntry baseEntry) => Base = baseEntry;
    public new string Id { get => Base.Id; set => Base.Id = value; }
    public new string? ParentId { get => Base.ParentId; set => Base.ParentId = value; }
    public new string Timestamp { get => Base.Timestamp; set => Base.Timestamp = value; }
}

public class BranchSummaryEntryWrapper : SessionEntry
{
    public override string Type => "branch_summary";
    public BranchSummaryEntry Base { get; }
    public BranchSummaryEntryWrapper(BranchSummaryEntry baseEntry) => Base = baseEntry;
    public new string Id { get => Base.Id; set => Base.Id = value; }
    public new string? ParentId { get => Base.ParentId; set => Base.ParentId = value; }
    public new string Timestamp { get => Base.Timestamp; set => Base.Timestamp = value; }
}

public class CustomEntryWrapper : SessionEntry
{
    public override string Type => "custom";
    public CustomEntry Base { get; }
    public CustomEntryWrapper(CustomEntry baseEntry) => Base = baseEntry;
    public new string Id { get => Base.Id; set => Base.Id = value; }
    public new string? ParentId { get => Base.ParentId; set => Base.ParentId = value; }
    public new string Timestamp { get => Base.Timestamp; set => Base.Timestamp = value; }
}

public class CustomMessageEntryWrapper : SessionEntry
{
    public override string Type => "custom_message";
    public CustomMessageEntry Base { get; }
    public CustomMessageEntryWrapper(CustomMessageEntry baseEntry) => Base = baseEntry;
    public new string Id { get => Base.Id; set => Base.Id = value; }
    public new string? ParentId { get => Base.ParentId; set => Base.ParentId = value; }
    public new string Timestamp { get => Base.Timestamp; set => Base.Timestamp = value; }
}

public class LabelEntryWrapper : SessionEntry
{
    public override string Type => "label";
    public LabelEntry Base { get; }
    public LabelEntryWrapper(LabelEntry baseEntry) => Base = baseEntry;
    public new string Id { get => Base.Id; set => Base.Id = value; }
    public new string? ParentId { get => Base.ParentId; set => Base.ParentId = value; }
    public new string Timestamp { get => Base.Timestamp; set => Base.Timestamp = value; }
}

public class SessionInfoEntryWrapper : SessionEntry
{
    public override string Type => "session_info";
    public SessionInfoEntry Base { get; }
    public SessionInfoEntryWrapper(SessionInfoEntry baseEntry) => Base = baseEntry;
    public new string Id { get => Base.Id; set => Base.Id = value; }
    public new string? ParentId { get => Base.ParentId; set => Base.ParentId = value; }
    public new string Timestamp { get => Base.Timestamp; set => Base.Timestamp = value; }
}

/// <summary>
/// 会话树节点
/// </summary>
public class SessionTreeNode
{
    /// <summary>
    /// 条目
    /// </summary>
    public SessionEntry Entry { get; set; } = null!;

    /// <summary>
    /// 子节点
    /// </summary>
    public List<SessionTreeNode> Children { get; set; } = new();

    /// <summary>
    /// 解析后的标签
    /// </summary>
    public string? Label { get; set; }
}

/// <summary>
/// 会话上下文 - 发送给 LLM 的已解析消息列表
/// </summary>
public class SessionContext
{
    /// <summary>
    /// 消息列表
    /// </summary>
    public List<ChatMessageContent> Messages { get; set; } = new();

    /// <summary>
    /// 思考级别
    /// </summary>
    public string ThinkingLevel { get; set; } = "off";

    /// <summary>
    /// 当前模型
    /// </summary>
    public ModelInfo? Model { get; set; }
}

/// <summary>
/// 模型信息
/// </summary>
public class ModelInfo
{
    /// <summary>
    /// 提供商
    /// </summary>
    public string Provider { get; set; } = "";

    /// <summary>
    /// 模型 ID
    /// </summary>
    public string ModelId { get; set; } = "";
}

/// <summary>
/// 会话信息 - 用于会话列表显示
/// </summary>
public class SessionInfo
{
    /// <summary>
    /// 文件路径
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// 会话 ID
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// 工作目录
    /// </summary>
    public string Cwd { get; set; } = "";

    /// <summary>
    /// 显示名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 父会话路径
    /// </summary>
    public string? ParentSessionPath { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime Modified { get; set; }

    /// <summary>
    /// 消息数量
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// 第一条消息
    /// </summary>
    public string FirstMessage { get; set; } = "";

    /// <summary>
    /// 所有消息文本
    /// </summary>
    public string AllMessagesText { get; set; } = "";
}

/// <summary>
/// 会话 (向后兼容)
/// </summary>
public class Session
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// 工作目录
    /// </summary>
    public string WorkingDirectory { get; set; } = "";

    /// <summary>
    /// 创建时间 (Unix 时间戳)
    /// </summary>
    public long CreatedAt { get; set; }

    /// <summary>
    /// 最后消息 ID
    /// </summary>
    public string? LastMessageId { get; set; }
}
