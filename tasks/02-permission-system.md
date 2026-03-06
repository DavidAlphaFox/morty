# 任务 1.2: 权限系统

## 阶段
Phase 1 — 核心体验提升

## 目标
在工具执行前检查权限，支持 ask/allow/deny 三种动作，bash 命令安全检测，权限记忆持久化。

## 背景
当前 morty 没有任何权限控制。所有工具调用（包括 `rm -rf /`）都直接执行。opencode 有完整的权限系统：规则匹配、交互式询问、持久化存储。

## 设计方案

### 1. 权限模型

```csharp
// src/agent/Permission.cs

/// <summary>
/// 权限动作
/// </summary>
public enum PermissionAction
{
    Allow,  // 自动允许
    Deny,   // 自动拒绝
    Ask     // 交互式询问用户
}

/// <summary>
/// 权限规则
/// </summary>
public class PermissionRule
{
    /// <summary>权限类型: read, edit, bash, external_directory</summary>
    public string Permission { get; set; } = "";

    /// <summary>匹配模式: glob 路径或命令前缀</summary>
    public string Pattern { get; set; } = "*";

    /// <summary>动作</summary>
    public PermissionAction Action { get; set; } = PermissionAction.Ask;
}
```

### 2. 权限检查器

```csharp
// src/agent/PermissionChecker.cs

public class PermissionChecker
{
    private readonly List<PermissionRule> _rules;
    private readonly Dictionary<string, PermissionAction> _remembered;
    private readonly Func<PermissionRequest, Task<bool>>? _askUser;

    /// <summary>
    /// 检查权限，返回是否允许
    /// </summary>
    public async Task<bool> CheckAsync(string permission, string resource)
    {
        // 1. 查找匹配规则 (按优先级: 精确 > glob > 默认)
        var rule = FindMatchingRule(permission, resource);

        switch (rule.Action)
        {
            case PermissionAction.Allow:
                return true;
            case PermissionAction.Deny:
                return false;
            case PermissionAction.Ask:
                // 检查记忆
                var key = $"{permission}:{resource}";
                if (_remembered.TryGetValue(key, out var remembered))
                    return remembered == PermissionAction.Allow;
                // 交互式询问
                if (_askUser != null)
                {
                    var allowed = await _askUser(new PermissionRequest
                    {
                        Permission = permission,
                        Resource = resource,
                        Description = BuildDescription(permission, resource)
                    });
                    _remembered[key] = allowed
                        ? PermissionAction.Allow
                        : PermissionAction.Deny;
                    return allowed;
                }
                return false;
        }
        return false;
    }
}
```

### 3. 权限类型定义

| 权限类型 | 触发工具 | 资源格式 |
|---------|---------|---------|
| `read` | read_file | 文件路径 |
| `edit` | edit_file, write_file | 文件路径 |
| `bash` | bash | 命令字符串 |
| `external_directory` | read/write/edit | 工作目录外的路径 |

### 4. Bash 命令安全检测

```csharp
// src/agent/BashSafety.cs

public static class BashSafety
{
    /// <summary>危险命令模式</summary>
    private static readonly HashSet<string> DangerousCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "rm", "rmdir", "chmod", "chown", "mv", "dd",
        "mkfs", "kill", "killall", "shutdown", "reboot",
        "curl|bash", "wget|bash"
    };

    /// <summary>危险参数模式</summary>
    private static readonly string[] DangerousPatterns =
    {
        "rm -rf /",
        "rm -rf ~",
        "> /dev/",
        ":(){ :|:& };:",  // fork bomb
    };

    /// <summary>
    /// 分析命令风险等级
    /// </summary>
    public static BashRiskLevel Analyze(string command)
    {
        var firstWord = command.Split(' ', '|', ';', '&')[0].Trim();

        // 检查危险模式
        foreach (var pattern in DangerousPatterns)
            if (command.Contains(pattern))
                return BashRiskLevel.Critical;

        // 检查危险命令
        if (DangerousCommands.Contains(firstWord))
            return BashRiskLevel.High;

        // 包含管道、重定向到文件
        if (command.Contains('>') || command.Contains("sudo"))
            return BashRiskLevel.Medium;

        // 只读命令
        if (IsReadOnlyCommand(firstWord))
            return BashRiskLevel.Low;

        return BashRiskLevel.Medium;
    }

    private static bool IsReadOnlyCommand(string cmd) =>
        cmd is "ls" or "cat" or "head" or "tail" or "grep"
            or "find" or "wc" or "diff" or "file" or "which"
            or "echo" or "pwd" or "whoami" or "date"
            or "git status" or "git log" or "git diff";
}

public enum BashRiskLevel { Low, Medium, High, Critical }
```

### 5. 工具执行集成

在 `CodingAgent.ExecuteToolCallsAsync` 中加入权限检查：

```csharp
// 工具执行前检查权限
var permission = GetPermissionType(fc.Name);
var resource = GetResource(fc.Name, fc.Arguments);

if (permission != null)
{
    var allowed = await _permissionChecker.CheckAsync(permission, resource);
    if (!allowed)
    {
        result = $"权限被拒绝: {permission} on {resource}";
        isError = true;
        // 跳过执行
        continue;
    }
}
```

### 6. 权限配置

扩展 `MortyConfig`:

```csharp
// ConfigOptions.cs 新增
public class PermissionConfig
{
    [JsonPropertyName("rules")]
    public List<PermissionRuleConfig>? Rules { get; set; }

    [JsonPropertyName("autoAllow")]
    public List<string>? AutoAllow { get; set; }  // 如 ["read", "grep", "find", "ls"]
}

public class PermissionRuleConfig
{
    [JsonPropertyName("permission")]
    public string Permission { get; set; } = "";

    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "*";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "ask";  // allow, deny, ask
}
```

### 7. 权限持久化

```csharp
// 持久化到 ~/.config/morty/permissions.json
// 格式: { "project:/path/to/project": { "bash:git *": "allow", "edit:*.cs": "allow" } }
```

### 8. CLI 交互式询问

```csharp
// Program.cs — 注入 askUser 回调
var permissionChecker = new PermissionChecker(config.Permission?.Rules, askUser: async (req) =>
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write($"  Permission: {req.Permission} — {req.Description}");
    Console.Write(" [y/N/always] ");
    Console.ResetColor();
    var input = Console.ReadLine()?.Trim().ToLower();
    return input is "y" or "yes" or "always";
});
```

## 实现步骤

1. [ ] 创建 `src/agent/Permission.cs` — 权限模型定义
2. [ ] 创建 `src/agent/PermissionChecker.cs` — 权限检查逻辑 + glob 匹配
3. [ ] 创建 `src/agent/BashSafety.cs` — bash 命令风险分析
4. [ ] `ConfigOptions.cs` — 新增 `PermissionConfig`
5. [ ] `CodingAgent.cs` — 注入 `PermissionChecker`，工具执行前检查
6. [ ] `Program.cs` — 实现交互式询问回调
7. [ ] 实现权限持久化 (JSON 文件)
8. [ ] 默认规则: read/grep/find/ls 自动允许，edit/write/bash 询问

## 验收标准
- [ ] bash 危险命令 (rm -rf) 执行前需用户确认
- [ ] 文件编辑前需用户确认
- [ ] 只读操作自动允许
- [ ] 用户选择 "always" 后同类操作不再询问
- [ ] 权限规则可通过配置文件定义

## 参考
- opencode: `src/permission/next.ts`, `src/permission/arity.ts`

## 相关文件
- `src/agent/Permission.cs` — 新建
- `src/agent/PermissionChecker.cs` — 新建
- `src/agent/BashSafety.cs` — 新建
- `src/agent/CodingAgent.cs` — 集成
- `src/config/ConfigOptions.cs` — 扩展
- `src/cli/Program.cs` — 交互询问
