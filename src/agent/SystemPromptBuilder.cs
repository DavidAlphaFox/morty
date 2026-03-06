// =============================================================================
// 系统提示词构建器
// =============================================================================
// 构建包含工具列表、使用指南和项目上下文的系统提示词
// 参考 pi-mono 的 buildSystemPrompt 模式
// =============================================================================

namespace Morty.Agent;

/// <summary>
/// 系统提示词构建选项
/// </summary>
public class SystemPromptOptions
{
    /// <summary>
    /// 自定义系统提示词 (替换默认)
    /// </summary>
    public string? CustomPrompt { get; set; }

    /// <summary>
    /// 追加到系统提示词末尾的内容
    /// </summary>
    public string? AppendPrompt { get; set; }

    /// <summary>
    /// 选中的工具名称列表
    /// </summary>
    public List<string>? SelectedTools { get; set; }

    /// <summary>
    /// 工作目录
    /// </summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// 项目上下文文件 (如 CLAUDE.md, AGENTS.md)
    /// </summary>
    public List<ContextFile>? ContextFiles { get; set; }
}

/// <summary>
/// 上下文文件
/// </summary>
public class ContextFile
{
    public string Path { get; set; } = "";
    public string Content { get; set; } = "";
}

/// <summary>
/// 系统提示词构建器
/// </summary>
public static class SystemPromptBuilder
{
    /// <summary>
    /// 工具描述映射
    /// </summary>
    private static readonly Dictionary<string, string> ToolDescriptions = new()
    {
        ["read"] = "Read file contents",
        ["bash"] = "Execute bash commands",
        ["edit"] = "Make surgical edits to files (find exact text and replace)",
        ["write"] = "Create or overwrite files",
        ["grep"] = "Search file contents for patterns",
        ["find"] = "Find files by name pattern",
        ["ls"] = "List directory contents"
    };

    /// <summary>
    /// 构建系统提示词
    /// </summary>
    public static string Build(SystemPromptOptions? options = null)
    {
        var cwd = options?.Cwd ?? Environment.CurrentDirectory;
        var now = DateTime.Now;
        var dateTime = now.ToString("yyyy-MM-dd HH:mm:ss");

        // 自定义提示词模式
        if (!string.IsNullOrEmpty(options?.CustomPrompt))
        {
            var prompt = options.CustomPrompt;

            if (!string.IsNullOrEmpty(options.AppendPrompt))
                prompt += $"\n\n{options.AppendPrompt}";

            prompt += BuildContextSection(options.ContextFiles);
            prompt += $"\nCurrent date and time: {dateTime}";
            prompt += $"\nCurrent working directory: {cwd}";

            return prompt;
        }

        // 构建工具列表
        var selectedTools = options?.SelectedTools ?? new List<string>
            { "read", "bash", "edit", "write", "grep", "find", "ls" };
        var tools = selectedTools.Where(t => ToolDescriptions.ContainsKey(t)).ToList();
        var toolsList = tools.Count > 0
            ? string.Join("\n", tools.Select(t => $"- {t}: {ToolDescriptions[t]}"))
            : "(none)";

        // 构建使用指南
        var guidelines = BuildGuidelines(tools);

        var result = $"""
            You are Morty, an expert coding assistant. You help users by reading files, executing commands, editing code, and writing new files.

            Available tools:
            {toolsList}

            Guidelines:
            {guidelines}
            """;

        if (!string.IsNullOrEmpty(options?.AppendPrompt))
            result += $"\n\n{options.AppendPrompt}";

        result += BuildContextSection(options?.ContextFiles);
        result += $"\nCurrent date and time: {dateTime}";
        result += $"\nCurrent working directory: {cwd}";

        return result;
    }

    /// <summary>
    /// 构建使用指南
    /// </summary>
    private static string BuildGuidelines(List<string> tools)
    {
        var guidelines = new List<string>();

        var hasBash = tools.Contains("bash");
        var hasEdit = tools.Contains("edit");
        var hasWrite = tools.Contains("write");
        var hasGrep = tools.Contains("grep");
        var hasFind = tools.Contains("find");
        var hasLs = tools.Contains("ls");
        var hasRead = tools.Contains("read");

        if (hasBash && (hasGrep || hasFind || hasLs))
            guidelines.Add("Prefer grep/find/ls tools over bash for file exploration");

        if (hasRead && hasEdit)
            guidelines.Add("Always read files before editing to understand current content");

        if (hasEdit)
            guidelines.Add("Use edit for precise changes (old text must match exactly including whitespace)");

        if (hasWrite)
            guidelines.Add("Use write only for new files or complete rewrites");

        guidelines.Add("Be concise in your responses");
        guidelines.Add("Show file paths clearly when working with files");

        return string.Join("\n", guidelines.Select(g => $"- {g}"));
    }

    /// <summary>
    /// 构建项目上下文段落
    /// </summary>
    private static string BuildContextSection(List<ContextFile>? contextFiles)
    {
        if (contextFiles == null || contextFiles.Count == 0)
            return "";

        var section = "\n\n# Project Context\n\nProject-specific instructions and guidelines:\n\n";
        foreach (var file in contextFiles)
        {
            section += $"## {file.Path}\n\n{file.Content}\n\n";
        }
        return section;
    }

    /// <summary>
    /// 加载项目上下文文件 (.morty/AGENTS.md 等)
    /// </summary>
    public static List<ContextFile> LoadContextFiles(string cwd)
    {
        var files = new List<ContextFile>();

        // 查找项目根目录的上下文文件
        string[] contextFileNames = { "AGENTS.md", "CLAUDE.md", ".morty/AGENTS.md" };
        foreach (var name in contextFileNames)
        {
            var path = Path.Combine(cwd, name);
            if (File.Exists(path))
            {
                files.Add(new ContextFile
                {
                    Path = name,
                    Content = File.ReadAllText(path)
                });
            }
        }

        return files;
    }
}
