// =============================================================================
// 编辑替换策略
// =============================================================================
// 多种模糊匹配策略，按精确度从高到低依次尝试
// 参考 opencode src/tool/edit.ts
// =============================================================================

namespace Morty.Tools;

/// <summary>
/// 替换策略接口
/// </summary>
public interface IEditReplacer
{
    string Name { get; }

    /// <summary>
    /// 尝试替换，返回 null 表示未找到匹配
    /// </summary>
    string? TryReplace(string content, string oldString, string newString);
}

/// <summary>
/// 编辑策略 — 依次尝试各替换策略
/// </summary>
public static class EditStrategy
{
    private static readonly IEditReplacer[] Replacers =
    {
        new ExactReplacer(),
        new LineTrimmedReplacer(),
        new WhitespaceNormalizedReplacer(),
        new IndentationFlexibleReplacer(),
        new BlockAnchorReplacer()
    };

    /// <summary>
    /// 依次尝试各策略，返回第一个成功的结果
    /// </summary>
    public static EditResult TryReplace(string content, string oldString, string newString)
    {
        foreach (var replacer in Replacers)
        {
            var result = replacer.TryReplace(content, oldString, newString);
            if (result != null && result != content)
            {
                return new EditResult
                {
                    Success = true,
                    Content = result,
                    Strategy = replacer.Name
                };
            }
        }

        return new EditResult { Success = false };
    }
}

public class EditResult
{
    public bool Success { get; set; }
    public string Content { get; set; } = "";
    public string Strategy { get; set; } = "";
}

// =============================================================================
// 各替换策略实现
// =============================================================================

/// <summary>
/// 1. 精确匹配 (唯一性检查)
/// </summary>
public class ExactReplacer : IEditReplacer
{
    public string Name => "Exact";

    public string? TryReplace(string content, string old, string @new)
    {
        if (!content.Contains(old)) return null;
        if (CountOccurrences(content, old) > 1) return null;
        return content.Replace(old, @new);
    }

    internal static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}

/// <summary>
/// 2. 行尾空白忽略 — TrimEnd 每行后匹配
/// </summary>
public class LineTrimmedReplacer : IEditReplacer
{
    public string Name => "LineTrimmed";

    public string? TryReplace(string content, string old, string @new)
    {
        var contentNorm = NormalizeLines(content);
        var oldNorm = NormalizeLines(old);

        var idx = contentNorm.IndexOf(oldNorm, StringComparison.Ordinal);
        if (idx < 0) return null;
        if (contentNorm.IndexOf(oldNorm, idx + 1, StringComparison.Ordinal) >= 0)
            return null; // 多处匹配

        var range = MapToOriginal(content, contentNorm, idx, oldNorm.Length);
        if (range == null) return null;

        return content[..range.Value.Start] + @new + content[range.Value.End..];
    }

    private static string NormalizeLines(string s) =>
        string.Join("\n", s.Split('\n').Select(l => l.TrimEnd()));

    /// <summary>
    /// 将 normalized 字符串中的位置映射回原始字符串
    /// </summary>
    private static (int Start, int End)? MapToOriginal(
        string original, string normalized, int normStart, int normLength)
    {
        var origLines = original.Split('\n');
        var normLines = normalized.Split('\n');
        var normTarget = normalized.Substring(normStart, normLength);
        var targetLines = normTarget.Split('\n');

        // 找到起始行
        var normOffset = 0;
        int startLine = -1, startCol = -1;

        for (var i = 0; i < normLines.Length; i++)
        {
            if (normOffset <= normStart && normStart < normOffset + normLines[i].Length + 1)
            {
                startLine = i;
                startCol = normStart - normOffset;
                break;
            }
            normOffset += normLines[i].Length + 1;
        }

        if (startLine < 0) return null;

        // 计算原始位置
        var origStart = 0;
        for (var i = 0; i < startLine; i++)
            origStart += origLines[i].Length + 1;
        origStart += startCol;

        var endLine = startLine + targetLines.Length - 1;
        var origEnd = 0;
        for (var i = 0; i < endLine; i++)
            origEnd += origLines[i].Length + 1;
        origEnd += targetLines[^1].Length;
        if (endLine < origLines.Length)
            origEnd = Math.Min(origEnd, origStart + origLines[startLine..Math.Min(endLine + 1, origLines.Length)]
                .Select((l, i) => l.Length + (i > 0 ? 1 : 0)).Sum());

        // 简化: 按行重新计算
        origEnd = origStart;
        for (var i = 0; i < targetLines.Length; i++)
        {
            var lineIdx = startLine + i;
            if (lineIdx >= origLines.Length) return null;
            origEnd += origLines[lineIdx].Length;
            if (i < targetLines.Length - 1)
                origEnd += 1; // newline
        }

        return (origStart, origEnd);
    }
}

/// <summary>
/// 3. 空白归一化 — 连续空白视为单个空格
/// </summary>
public class WhitespaceNormalizedReplacer : IEditReplacer
{
    public string Name => "WhitespaceNormalized";

    public string? TryReplace(string content, string old, string @new)
    {
        var contentLines = content.Split('\n');
        var oldLines = old.Split('\n');
        if (oldLines.Length == 0) return null;

        // 逐行窗口扫描
        for (var i = 0; i <= contentLines.Length - oldLines.Length; i++)
        {
            var match = true;
            for (var j = 0; j < oldLines.Length; j++)
            {
                if (NormalizeWhitespace(contentLines[i + j]) !=
                    NormalizeWhitespace(oldLines[j]))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                // 检查唯一性 — 看后面还有没有匹配
                var hasAnother = false;
                for (var k = i + 1; k <= contentLines.Length - oldLines.Length; k++)
                {
                    var match2 = true;
                    for (var j = 0; j < oldLines.Length; j++)
                    {
                        if (NormalizeWhitespace(contentLines[k + j]) !=
                            NormalizeWhitespace(oldLines[j]))
                        {
                            match2 = false;
                            break;
                        }
                    }
                    if (match2) { hasAnother = true; break; }
                }
                if (hasAnother) return null;

                var before = string.Join("\n", contentLines.Take(i));
                var after = string.Join("\n", contentLines.Skip(i + oldLines.Length));
                if (before.Length > 0) before += "\n";
                if (after.Length > 0) @new += "\n";
                return before + @new + after;
            }
        }

        return null;
    }

    private static string NormalizeWhitespace(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"\s+", " ");
}

/// <summary>
/// 4. 缩进灵活匹配 — 忽略统一的缩进偏移
/// </summary>
public class IndentationFlexibleReplacer : IEditReplacer
{
    public string Name => "IndentationFlexible";

    public string? TryReplace(string content, string old, string @new)
    {
        var contentLines = content.Split('\n');
        var oldLines = old.Split('\n');
        if (oldLines.Length == 0) return null;

        for (var i = 0; i <= contentLines.Length - oldLines.Length; i++)
        {
            var indent = DetectIndentOffset(contentLines[i], oldLines[0]);
            if (indent == null) continue;

            var match = true;
            for (var j = 1; j < oldLines.Length; j++)
            {
                var expected = ApplyIndent(oldLines[j], indent.Value);
                if (contentLines[i + j] != expected)
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                var newLines = @new.Split('\n')
                    .Select(l => ApplyIndent(l, indent.Value))
                    .ToArray();

                var result = contentLines.Take(i)
                    .Concat(newLines)
                    .Concat(contentLines.Skip(i + oldLines.Length))
                    .ToArray();

                return string.Join("\n", result);
            }
        }

        return null;
    }

    private static int? DetectIndentOffset(string contentLine, string oldLine)
    {
        var contentIndent = contentLine.Length - contentLine.TrimStart().Length;
        var oldIndent = oldLine.Length - oldLine.TrimStart().Length;

        // 非空白内容必须匹配
        if (contentLine.TrimStart() != oldLine.TrimStart())
            return null;

        return contentIndent - oldIndent;
    }

    private static string ApplyIndent(string line, int offset)
    {
        if (string.IsNullOrWhiteSpace(line)) return line;
        var currentIndent = line.Length - line.TrimStart().Length;
        var newIndent = Math.Max(0, currentIndent + offset);
        return new string(' ', newIndent) + line.TrimStart();
    }
}

/// <summary>
/// 5. 锚点块匹配 — 首尾行精确匹配，中间行作为块替换
/// </summary>
public class BlockAnchorReplacer : IEditReplacer
{
    public string Name => "BlockAnchor";

    public string? TryReplace(string content, string old, string @new)
    {
        var contentLines = content.Split('\n');
        var oldLines = old.Split('\n');
        if (oldLines.Length < 3) return null; // 至少需要 3 行 (首+中+尾)

        var firstLine = oldLines[0].Trim();
        var lastLine = oldLines[^1].Trim();
        if (string.IsNullOrEmpty(firstLine) || string.IsNullOrEmpty(lastLine))
            return null;

        // 查找首行锚点
        for (var i = 0; i < contentLines.Length; i++)
        {
            if (contentLines[i].Trim() != firstLine) continue;

            // 查找尾行锚点
            var expectedEnd = i + oldLines.Length - 1;
            if (expectedEnd >= contentLines.Length) continue;
            if (contentLines[expectedEnd].Trim() != lastLine) continue;

            // 匹配: 替换首行到尾行之间的所有内容
            var result = contentLines.Take(i)
                .Concat(@new.Split('\n'))
                .Concat(contentLines.Skip(expectedEnd + 1))
                .ToArray();

            return string.Join("\n", result);
        }

        return null;
    }
}

// =============================================================================
// 文本相似度
// =============================================================================

public static class TextSimilarity
{
    /// <summary>
    /// 在 content 中找到与 target 最相似的片段
    /// </summary>
    public static (string Text, double Similarity) FindMostSimilar(string content, string target)
    {
        var contentLines = content.Split('\n');
        var targetLineCount = target.Split('\n').Length;
        var bestMatch = "";
        var bestSimilarity = 0.0;

        for (var i = 0; i <= contentLines.Length - targetLineCount; i++)
        {
            var candidate = string.Join("\n",
                contentLines.Skip(i).Take(targetLineCount + 2));
            var sim = CalculateSimilarity(candidate, target);
            if (sim > bestSimilarity)
            {
                bestSimilarity = sim;
                bestMatch = candidate;
            }
        }

        return (bestMatch, bestSimilarity);
    }

    public static double CalculateSimilarity(string a, string b)
    {
        var distance = LevenshteinDistance(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        return maxLen == 0 ? 1.0 : 1.0 - (double)distance / maxLen;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        // 使用两行优化空间
        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
            prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }
}
