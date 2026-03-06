// =============================================================================
// 文件操作工具
// =============================================================================
// 提供文件读取、写入、编辑功能
// 包含路径安全检查，确保操作在允许的工作目录内
// 参考 pi-mono 的 read/write/edit 工具:
//   - 读取: 支持 offset/limit，输出带行号，超长自动截断
//   - 编辑: 精确文本匹配替换，唯一性检查
// =============================================================================

using System.Text;

namespace Morty.Tools;

/// <summary>
/// 文件操作工具
/// </summary>
public class FileTools
{
    private readonly string _workingDirectory;

    public FileTools(string workingDirectory)
    {
        _workingDirectory = Path.GetFullPath(workingDirectory);
    }

    /// <summary>
    /// 解析并验证文件路径
    /// </summary>
    private string ResolvePath(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_workingDirectory, path));

        if (!fullPath.StartsWith(_workingDirectory))
            throw new UnauthorizedAccessException($"路径不在工作目录内: {path}");

        return fullPath;
    }

    /// <summary>
    /// 读取文件内容 (支持 offset/limit，带行号输出，自动截断)
    /// </summary>
    public async Task<string> Read(string path, int? offset = null, int? limit = null)
    {
        var fullPath = ResolvePath(path);
        var content = await File.ReadAllTextAsync(fullPath);
        var allLines = content.Split('\n');
        var totalLines = allLines.Length;

        // 计算起始行 (1-indexed to 0-indexed)
        var startLine = offset.HasValue ? Math.Max(0, offset.Value - 1) : 0;
        if (startLine >= allLines.Length)
            throw new InvalidOperationException($"Offset {offset} is beyond end of file ({allLines.Length} lines total)");

        // 应用 limit
        string selectedContent;
        int? userLimitedLines = null;
        if (limit.HasValue)
        {
            var endLine = Math.Min(startLine + limit.Value, allLines.Length);
            selectedContent = string.Join("\n", allLines.Skip(startLine).Take(endLine - startLine));
            userLimitedLines = endLine - startLine;
        }
        else
        {
            selectedContent = string.Join("\n", allLines.Skip(startLine));
        }

        // 应用截断
        var truncation = OutputTruncator.TruncateHead(selectedContent);
        var startLineDisplay = startLine + 1;

        // 添加行号
        var outputLines = truncation.Content.Split('\n');
        var sb = new StringBuilder();
        for (var i = 0; i < outputLines.Length; i++)
        {
            var lineNum = startLineDisplay + i;
            sb.AppendLine($"{lineNum,6}\t{outputLines[i]}");
        }

        var result = sb.ToString().TrimEnd();

        // 添加截断提示
        if (truncation.Truncated)
        {
            var endLineDisplay = startLineDisplay + truncation.OutputLines - 1;
            var nextOffset = endLineDisplay + 1;
            result += $"\n\n[Showing lines {startLineDisplay}-{endLineDisplay} of {totalLines}. Use offset={nextOffset} to continue.]";
        }
        else if (userLimitedLines.HasValue && startLine + userLimitedLines.Value < allLines.Length)
        {
            var remaining = allLines.Length - (startLine + userLimitedLines.Value);
            var nextOffset = startLine + userLimitedLines.Value + 1;
            result += $"\n\n[{remaining} more lines in file. Use offset={nextOffset} to continue.]";
        }

        return result;
    }

    /// <summary>
    /// 写入文件内容
    /// </summary>
    public async Task<string> Write(string path, string content)
    {
        var fullPath = ResolvePath(path);

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(fullPath, content);
        return $"Successfully wrote {content.Length} bytes to {path}";
    }

    /// <summary>
    /// 编辑文件内容 (多策略匹配替换)
    /// </summary>
    public async Task<string> Edit(string path, string oldString, string newString)
    {
        var fullPath = ResolvePath(path);
        var content = await File.ReadAllTextAsync(fullPath);

        var result = EditStrategy.TryReplace(content, oldString, newString);

        if (!result.Success)
        {
            // 找到最相似的片段提供提示
            var (similar, similarity) = TextSimilarity.FindMostSimilar(content, oldString);
            var hint = similarity > 0.5
                ? $"\nClosest match (similarity {similarity:P0}):\n{similar}"
                : "";
            throw new InvalidOperationException(
                $"Could not find matching text in {path}. The old text must match (including whitespace and newlines).{hint}");
        }

        await File.WriteAllTextAsync(fullPath, result.Content);

        var strategyNote = result.Strategy != "Exact"
            ? $" (matched via {result.Strategy})"
            : "";
        return $"Successfully replaced text in {path}.{strategyNote}";
    }
}
