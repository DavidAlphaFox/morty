// =============================================================================
// 输出截断工具
// =============================================================================
// 基于行数和字节数限制截断工具输出
// 参考 pi-mono 的 truncate.ts 实现:
//   - truncateHead: 保留前 N 行 (用于文件读取)
//   - truncateTail: 保留后 N 行 (用于 bash 输出)
// =============================================================================

using System.Text;

namespace Morty.Tools;

/// <summary>
/// 截断结果
/// </summary>
public class TruncationResult
{
    public string Content { get; set; } = "";
    public bool Truncated { get; set; }
    public int TotalLines { get; set; }
    public int OutputLines { get; set; }
}

/// <summary>
/// 输出截断工具
/// </summary>
public static class OutputTruncator
{
    public const int DefaultMaxLines = 2000;
    public const int DefaultMaxBytes = 50 * 1024; // 50KB

    /// <summary>
    /// 从头部截断 (保留前 N 行/字节) — 用于文件读取
    /// </summary>
    public static TruncationResult TruncateHead(string content, int maxLines = DefaultMaxLines, int maxBytes = DefaultMaxBytes)
    {
        var lines = content.Split('\n');
        var totalLines = lines.Length;
        var totalBytes = Encoding.UTF8.GetByteCount(content);

        if (totalLines <= maxLines && totalBytes <= maxBytes)
        {
            return new TruncationResult
            {
                Content = content,
                Truncated = false,
                TotalLines = totalLines,
                OutputLines = totalLines
            };
        }

        var outputLines = new List<string>();
        var bytesCount = 0;

        for (var i = 0; i < lines.Length && i < maxLines; i++)
        {
            var lineBytes = Encoding.UTF8.GetByteCount(lines[i]) + (i > 0 ? 1 : 0);
            if (bytesCount + lineBytes > maxBytes) break;
            outputLines.Add(lines[i]);
            bytesCount += lineBytes;
        }

        return new TruncationResult
        {
            Content = string.Join("\n", outputLines),
            Truncated = true,
            TotalLines = totalLines,
            OutputLines = outputLines.Count
        };
    }

    /// <summary>
    /// 从尾部截断 (保留后 N 行/字节) — 用于 bash 输出
    /// </summary>
    public static TruncationResult TruncateTail(string content, int maxLines = DefaultMaxLines, int maxBytes = DefaultMaxBytes)
    {
        var lines = content.Split('\n');
        var totalLines = lines.Length;
        var totalBytes = Encoding.UTF8.GetByteCount(content);

        if (totalLines <= maxLines && totalBytes <= maxBytes)
        {
            return new TruncationResult
            {
                Content = content,
                Truncated = false,
                TotalLines = totalLines,
                OutputLines = totalLines
            };
        }

        var outputLines = new List<string>();
        var bytesCount = 0;

        for (var i = lines.Length - 1; i >= 0 && outputLines.Count < maxLines; i--)
        {
            var lineBytes = Encoding.UTF8.GetByteCount(lines[i]) + (outputLines.Count > 0 ? 1 : 0);
            if (bytesCount + lineBytes > maxBytes) break;
            outputLines.Insert(0, lines[i]);
            bytesCount += lineBytes;
        }

        return new TruncationResult
        {
            Content = string.Join("\n", outputLines),
            Truncated = true,
            TotalLines = totalLines,
            OutputLines = outputLines.Count
        };
    }
}
