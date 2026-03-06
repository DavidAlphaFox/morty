// =============================================================================
// 权限系统
// =============================================================================
// 工具执行前的权限检查，支持 allow/deny/ask 三种动作
// 规则匹配、交互式询问、会话内记忆
// =============================================================================

namespace Morty.Agent;

/// <summary>
/// 权限动作
/// </summary>
public enum PermissionAction
{
    Allow,
    Deny,
    Ask
}

/// <summary>
/// 权限规则
/// </summary>
public class PermissionRule
{
    /// <summary>权限类型: read, edit, bash</summary>
    public string Permission { get; set; } = "";

    /// <summary>匹配模式: glob 路径或命令前缀, "*" 表示全部</summary>
    public string Pattern { get; set; } = "*";

    /// <summary>动作</summary>
    public PermissionAction Action { get; set; } = PermissionAction.Ask;
}

/// <summary>
/// 权限请求 — 传递给 askUser 回调
/// </summary>
public class PermissionRequest
{
    public string ToolName { get; set; } = "";
    public string Permission { get; set; } = "";
    public string Resource { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>
/// 权限检查器
/// </summary>
public class PermissionChecker
{
    private readonly List<PermissionRule> _rules;
    private readonly Dictionary<string, PermissionAction> _remembered = new();
    private readonly Func<PermissionRequest, Task<PermissionAction>>? _askUser;

    /// <summary>
    /// 工具名到权限类型的映射
    /// </summary>
    private static readonly Dictionary<string, string> ToolPermissionMap = new()
    {
        ["read_file"] = "read",
        ["write_file"] = "edit",
        ["edit_file"] = "edit",
        ["bash"] = "bash",
        ["grep"] = "read",
        ["find"] = "read",
        ["ls"] = "read"
    };

    /// <summary>
    /// 默认自动允许的权限类型
    /// </summary>
    private static readonly HashSet<string> DefaultAutoAllow = new() { "read" };

    public PermissionChecker(
        List<PermissionRule>? rules = null,
        Func<PermissionRequest, Task<PermissionAction>>? askUser = null)
    {
        _rules = rules ?? BuildDefaultRules();
        _askUser = askUser;
    }

    /// <summary>
    /// 检查工具调用权限，返回是否允许执行
    /// </summary>
    public async Task<bool> CheckAsync(string toolName, IDictionary<string, object?>? args)
    {
        var permission = GetPermissionType(toolName);
        if (permission == null)
            return true; // 未知工具默认允许

        var resource = GetResource(toolName, args);

        // 对 bash 命令做安全分析
        if (permission == "bash")
        {
            var risk = BashSafety.Analyze(resource);
            if (risk == BashRiskLevel.Low)
                return true; // 只读 bash 命令自动允许
        }

        // 查找匹配规则
        var rule = FindMatchingRule(permission, resource);

        switch (rule.Action)
        {
            case PermissionAction.Allow:
                return true;
            case PermissionAction.Deny:
                return false;
            case PermissionAction.Ask:
                return await AskUserAsync(toolName, permission, resource);
            default:
                return false;
        }
    }

    /// <summary>
    /// 获取被拒绝时的提示消息
    /// </summary>
    public static string GetDeniedMessage(string toolName, IDictionary<string, object?>? args)
    {
        var resource = GetResource(toolName, args);
        return $"Permission denied: {toolName} on {resource}. User rejected this operation.";
    }

    private async Task<bool> AskUserAsync(string toolName, string permission, string resource)
    {
        // 检查会话记忆
        var key = $"{permission}:{resource}";
        if (_remembered.TryGetValue(key, out var remembered))
            return remembered == PermissionAction.Allow;

        // 检查通配符记忆 (如 "edit:*" 表示用户选了 always)
        var wildcardKey = $"{permission}:*";
        if (_remembered.TryGetValue(wildcardKey, out var wildcardRemembered))
            return wildcardRemembered == PermissionAction.Allow;

        if (_askUser == null)
            return false;

        var request = new PermissionRequest
        {
            ToolName = toolName,
            Permission = permission,
            Resource = resource,
            Description = BuildDescription(toolName, permission, resource)
        };

        var action = await _askUser(request);

        // Allow = 本次允许, Deny = 本次拒绝, Ask = "always" (记住通配符)
        switch (action)
        {
            case PermissionAction.Allow:
                _remembered[key] = PermissionAction.Allow;
                return true;
            case PermissionAction.Deny:
                _remembered[key] = PermissionAction.Deny;
                return false;
            default: // Ask 被复用为 "always allow this permission type"
                _remembered[wildcardKey] = PermissionAction.Allow;
                return true;
        }
    }

    private PermissionRule FindMatchingRule(string permission, string resource)
    {
        // 精确匹配优先
        foreach (var rule in _rules)
        {
            if (rule.Permission != permission) continue;
            if (rule.Pattern == "*") continue; // 跳过通配符，后面兜底
            if (MatchPattern(resource, rule.Pattern))
                return rule;
        }

        // 通配符兜底
        foreach (var rule in _rules)
        {
            if (rule.Permission == permission && rule.Pattern == "*")
                return rule;
        }

        // 无匹配规则 — 默认 ask
        return new PermissionRule { Permission = permission, Action = PermissionAction.Ask };
    }

    private static bool MatchPattern(string resource, string pattern)
    {
        // 简单 glob: 支持 * 和 ** 通配符
        if (pattern == "*") return true;

        // 前缀匹配 (如 "git *" 匹配 "git status")
        if (pattern.EndsWith("*"))
        {
            var prefix = pattern[..^1];
            return resource.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(resource, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetPermissionType(string toolName)
    {
        return ToolPermissionMap.GetValueOrDefault(toolName);
    }

    private static string GetResource(string toolName, IDictionary<string, object?>? args)
    {
        if (args == null) return "";

        return toolName switch
        {
            "bash" => GetArg(args, "command"),
            "read_file" or "write_file" or "edit_file" => GetArg(args, "path"),
            "grep" => GetArg(args, "pattern"),
            "find" => GetArg(args, "pattern"),
            "ls" => GetArg(args, "path", "."),
            _ => ""
        };
    }

    private static string GetArg(IDictionary<string, object?> args, string key, string defaultValue = "")
    {
        return args.TryGetValue(key, out var value) ? value?.ToString() ?? defaultValue : defaultValue;
    }

    private static string BuildDescription(string toolName, string permission, string resource)
    {
        return toolName switch
        {
            "bash" => $"Execute: {resource}",
            "write_file" => $"Write to: {resource}",
            "edit_file" => $"Edit: {resource}",
            _ => $"{permission}: {resource}"
        };
    }

    private static List<PermissionRule> BuildDefaultRules()
    {
        return new List<PermissionRule>
        {
            // 只读操作自动允许
            new() { Permission = "read", Pattern = "*", Action = PermissionAction.Allow },
            // 编辑和 bash 需要确认
            new() { Permission = "edit", Pattern = "*", Action = PermissionAction.Ask },
            new() { Permission = "bash", Pattern = "*", Action = PermissionAction.Ask },
        };
    }
}
