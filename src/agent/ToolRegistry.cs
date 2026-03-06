// =============================================================================
// 工具注册表
// =============================================================================
// 将 FileTools 和 SystemTools 的方法包装为 AIFunction 实例
// 参考 pi-mono 的 createAllTools / createCodingTools 模式
// =============================================================================

using System.ComponentModel;
using Microsoft.Extensions.AI;
using Morty.Config;
using Morty.Tools;

namespace Morty.Agent;

/// <summary>
/// 工具注册表 — 创建绑定到指定工作目录的 AIFunction 工具集
/// </summary>
public static class ToolRegistry
{
    /// <summary>
    /// 创建所有可用工具
    /// </summary>
    public static List<AIFunction> CreateAllTools(string workingDirectory, ToolsConfig? config = null, PermissionChecker? permissionChecker = null)
    {
        var tools = new List<AIFunction>();
        var enabled = config?.Enabled ?? new List<string> { "read", "write", "edit", "bash", "grep", "glob", "ls" };

        var fileTools = new FileTools(workingDirectory);
        var systemTools = new SystemTools(
            workingDirectory,
            config?.Bash?.AllowedCommands,
            config?.Bash?.Timeout ?? 300);
        var webTools = new WebTools();

        if (enabled.Contains("read"))
            tools.Add(AIFunctionFactory.Create(
                ([Description("File path (relative or absolute)")] string path,
                 [Description("Line number to start reading from (1-indexed)")] int? offset,
                 [Description("Maximum number of lines to read")] int? limit) =>
                    fileTools.Read(path, offset, limit),
                "read_file",
                "Read file contents with line numbers. Output is truncated to 2000 lines or 50KB. Use offset/limit for large files. When you need the full file, continue with offset until complete."));

        if (enabled.Contains("write"))
            tools.Add(AIFunctionFactory.Create(
                ([Description("File path (relative or absolute)")] string path,
                 [Description("Content to write to the file")] string content) =>
                    fileTools.Write(path, content),
                "write_file",
                "Write content to a file. Creates the file and parent directories if they don't exist, overwrites if it does."));

        if (enabled.Contains("edit"))
            tools.Add(AIFunctionFactory.Create(
                ([Description("File path (relative or absolute)")] string path,
                 [Description("Exact text to find and replace (must match exactly including whitespace)")] string oldString,
                 [Description("New text to replace the old text with")] string newString) =>
                    fileTools.Edit(path, oldString, newString),
                "edit_file",
                "Edit a file by replacing exact text. The oldString must match exactly (including whitespace and newlines). Use this for precise, surgical edits."));

        if (enabled.Contains("bash"))
            tools.Add(AIFunctionFactory.Create(
                ([Description("Bash command to execute")] string command) =>
                    systemTools.Bash(command),
                "bash",
                "Execute a bash command in the working directory. Returns stdout and stderr."));

        if (enabled.Contains("grep"))
            tools.Add(AIFunctionFactory.Create(
                ([Description("Search pattern (regex)")] string pattern,
                 [Description("Directory or file to search (default: working directory)")] string? path) =>
                    systemTools.Grep(pattern, path),
                "grep",
                "Search file contents for a pattern using grep. Returns matching lines with file paths and line numbers."));

        if (enabled.Contains("glob") || enabled.Contains("find"))
            tools.Add(AIFunctionFactory.Create(
                ([Description("Glob pattern (e.g. '*.cs', 'Program.*', '*.ts')")] string pattern,
                 [Description("Directory to search (default: working directory)")] string? path) =>
                    systemTools.Glob(pattern, path),
                "glob",
                "Find files matching a glob pattern. Results sorted by modification time (newest first). " +
                "Automatically excludes .git, node_modules, bin, obj directories."));

        if (enabled.Contains("ls"))
            tools.Add(AIFunctionFactory.Create(
                ([Description("Directory path (default: working directory)")] string? path) =>
                    systemTools.Ls(path),
                "ls",
                "List directory contents with details (permissions, size, date)."));

        if (enabled.Contains("webfetch"))
            tools.Add(AIFunctionFactory.Create(
                ([Description("URL to fetch")] string url,
                 [Description("Output format: 'text', 'markdown', or 'html' (default: markdown)")] string? format) =>
                    webTools.FetchAsync(url, format ?? "markdown"),
                "webfetch",
                "Fetch content from a URL. Returns the page content in the specified format. " +
                "Use 'markdown' for documentation, 'text' for plain content."));

        // lsp 工具
        if (enabled.Contains("lsp"))
        {
            var lspServer = LspClient.DetectLspServer(workingDirectory);
            if (lspServer != null)
            {
                var lspClient = new LspClient();
                tools.Add(AIFunctionFactory.Create(
                    ([Description("Operation: goToDefinition, findReferences, hover, documentSymbol")] string operation,
                     [Description("File path")] string file,
                     [Description("Line number (1-indexed, required for goToDefinition/findReferences/hover)")] int? line,
                     [Description("Column number (1-indexed, required for goToDefinition/findReferences/hover)")] int? character) =>
                        ExecuteLspAsync(lspClient, lspServer.Value, workingDirectory, operation, file, line, character),
                    "lsp",
                    "Perform code intelligence operations via Language Server Protocol. " +
                    "Operations: goToDefinition, findReferences, hover, documentSymbol."));
            }
        }

        // skill 工具
        if (enabled.Contains("skill"))
        {
            var skillLoader = new SkillLoader(workingDirectory);
            if (skillLoader.HasSkills)
            {
                var skillList = string.Join(", ", skillLoader.ListSkills());
                tools.Add(AIFunctionFactory.Create(
                    ([Description("Skill name to load")] string name) =>
                        skillLoader.LoadSkill(name),
                    "skill",
                    $"Load a skill (reusable instruction template). Available skills: {skillList}"));
            }
        }

        // batch 工具注册在最后，因为它需要引用其他工具
        if (enabled.Contains("batch"))
        {
            var batchExecutor = new BatchExecutor(tools, permissionChecker);
            tools.Add(AIFunctionFactory.Create(
                ([Description("JSON array of tool calls: [{\"tool\":\"read_file\",\"args\":{\"path\":\"...\"}}, ...]")]
                 string calls) =>
                    batchExecutor.ExecuteAsync(calls),
                "batch",
                "Execute multiple tool calls in parallel. " +
                "Input: JSON array of {tool, args} objects. Max 25 calls. " +
                "Returns aggregated results. Use when you need to perform multiple independent operations."));
        }

        return tools;
    }

    private static LspClient? _activeLspClient;

    private static async Task<string> ExecuteLspAsync(
        LspClient client, (string command, string[] args) server,
        string cwd, string operation, string file, int? line, int? character)
    {
        // 延迟初始化 LSP Server
        if (_activeLspClient == null)
        {
            try
            {
                await client.InitializeAsync(server.command, server.args, cwd);
                _activeLspClient = client;
            }
            catch (Exception ex)
            {
                return $"Error: Failed to start LSP server ({server.command}): {ex.Message}";
            }
        }

        return operation.ToLower() switch
        {
            "gotodefinition" => await client.GoToDefinitionAsync(file, line ?? 1, character ?? 1),
            "findreferences" => await client.FindReferencesAsync(file, line ?? 1, character ?? 1),
            "hover" => await client.HoverAsync(file, line ?? 1, character ?? 1),
            "documentsymbol" => await client.DocumentSymbolAsync(file),
            _ => $"Error: Unknown operation '{operation}'. Use: goToDefinition, findReferences, hover, documentSymbol"
        };
    }

    /// <summary>
    /// 创建默认编码工具集 (read, bash, edit, write)
    /// </summary>
    public static List<AIFunction> CreateCodingTools(string workingDirectory, ToolsConfig? config = null, PermissionChecker? permissionChecker = null)
    {
        var codingConfig = new ToolsConfig
        {
            Enabled = new List<string> { "read", "write", "edit", "bash" },
            Bash = config?.Bash
        };
        return CreateAllTools(workingDirectory, codingConfig, permissionChecker);
    }

    /// <summary>
    /// 创建只读工具集 (read, grep, glob, ls)
    /// </summary>
    public static List<AIFunction> CreateReadOnlyTools(string workingDirectory, ToolsConfig? config = null, PermissionChecker? permissionChecker = null)
    {
        var readOnlyConfig = new ToolsConfig
        {
            Enabled = new List<string> { "read", "grep", "glob", "ls" },
            Bash = config?.Bash
        };
        return CreateAllTools(workingDirectory, readOnlyConfig, permissionChecker);
    }
}
