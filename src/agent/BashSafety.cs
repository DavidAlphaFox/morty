// =============================================================================
// Bash 命令安全分析
// =============================================================================
// 分析 bash 命令的风险等级，用于权限检查
// =============================================================================

namespace Morty.Agent;

/// <summary>
/// Bash 命令风险等级
/// </summary>
public enum BashRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Bash 命令安全分析
/// </summary>
public static class BashSafety
{
    private static readonly HashSet<string> ReadOnlyCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "ls", "cat", "head", "tail", "grep", "rg", "find", "fd",
        "wc", "diff", "file", "which", "echo", "pwd", "whoami", "date",
        "git", "dotnet", "node", "npm", "npx", "pnpm", "bun",
        "python", "python3", "pip", "cargo", "go", "java", "javac",
        "env", "printenv", "uname", "hostname", "tree", "du", "df",
        "man", "help", "type", "realpath", "dirname", "basename"
    };

    /// <summary>
    /// 只读的 git 子命令
    /// </summary>
    private static readonly HashSet<string> ReadOnlyGitSubcommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "status", "log", "diff", "show", "branch", "tag", "remote",
        "describe", "rev-parse", "ls-files", "ls-tree", "blame",
        "shortlog", "stash list"
    };

    private static readonly HashSet<string> DangerousCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "rm", "rmdir", "chmod", "chown", "mv", "dd",
        "mkfs", "kill", "killall", "shutdown", "reboot"
    };

    private static readonly string[] CriticalPatterns =
    {
        "rm -rf /",
        "rm -rf ~",
        "rm -rf $HOME",
        "> /dev/",
        ":(){ :|:& };:",
        "curl|bash",
        "curl | bash",
        "wget|bash",
        "wget | bash",
        "curl|sh",
        "curl | sh"
    };

    /// <summary>
    /// 分析命令风险等级
    /// </summary>
    public static BashRiskLevel Analyze(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return BashRiskLevel.Low;

        // 检查 critical 模式
        foreach (var pattern in CriticalPatterns)
            if (command.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return BashRiskLevel.Critical;

        // 提取第一个命令 (处理管道和分号)
        var firstCommand = ExtractFirstCommand(command);
        var parts = firstCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return BashRiskLevel.Low;

        var cmd = parts[0];

        // 检查危险命令
        if (DangerousCommands.Contains(cmd))
            return BashRiskLevel.High;

        // sudo 提升风险
        if (cmd == "sudo")
            return BashRiskLevel.High;

        // 检查只读命令
        if (IsReadOnly(command, cmd, parts))
            return BashRiskLevel.Low;

        // 包含重定向到文件
        if (command.Contains('>'))
            return BashRiskLevel.Medium;

        return BashRiskLevel.Medium;
    }

    private static bool IsReadOnly(string fullCommand, string cmd, string[] parts)
    {
        if (!ReadOnlyCommands.Contains(cmd))
            return false;

        // git 需要检查子命令
        if (cmd == "git" && parts.Length > 1)
        {
            return ReadOnlyGitSubcommands.Contains(parts[1]);
        }

        // dotnet 只有 build/test/run/list 等才是安全的
        if (cmd == "dotnet" && parts.Length > 1)
        {
            return parts[1] is "build" or "test" or "run" or "list" or "restore"
                or "clean" or "format" or "--info" or "--version";
        }

        // 如果只读命令包含重定向，不算只读
        if (fullCommand.Contains('>'))
            return false;

        return true;
    }

    private static string ExtractFirstCommand(string command)
    {
        // 取管道、分号、&& 之前的部分
        var idx = command.IndexOfAny(new[] { '|', ';' });
        var andIdx = command.IndexOf("&&", StringComparison.Ordinal);

        if (andIdx >= 0 && (idx < 0 || andIdx < idx))
            idx = andIdx;

        return idx >= 0 ? command[..idx].Trim() : command.Trim();
    }
}
