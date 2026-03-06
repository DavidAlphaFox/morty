using System.Text;

namespace Morty.Tools;

/// <summary>
/// Patch 解析与应用工具
/// 支持 Add/Delete/Update/Move 文件操作
/// </summary>
public class PatchTool
{
    private readonly string _workingDirectory;

    public PatchTool(string workingDirectory)
    {
        _workingDirectory = Path.GetFullPath(workingDirectory);
    }

    /// <summary>
    /// 应用 patch 文本
    /// </summary>
    public async Task<string> ApplyAsync(string patchText)
    {
        var hunks = ParsePatch(patchText);
        if (hunks.Count == 0)
            return "Error: empty patch — no file operations found.";

        // 先验证所有操作
        foreach (var hunk in hunks)
        {
            var path = ResolvePath(hunk.Path);
            switch (hunk)
            {
                case AddHunk:
                    if (File.Exists(path))
                        return $"Error: file already exists: {hunk.Path}";
                    break;
                case DeleteHunk:
                case UpdateHunk:
                    if (!File.Exists(path))
                        return $"Error: file not found: {hunk.Path}";
                    break;
            }
        }

        var added = new List<string>();
        var modified = new List<string>();
        var deleted = new List<string>();

        foreach (var hunk in hunks)
        {
            switch (hunk)
            {
                case AddHunk add:
                {
                    var path = ResolvePath(add.Path);
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    await File.WriteAllTextAsync(path, add.Contents);
                    added.Add(add.Path);
                    break;
                }
                case DeleteHunk del:
                {
                    var path = ResolvePath(del.Path);
                    File.Delete(path);
                    deleted.Add(del.Path);
                    break;
                }
                case UpdateHunk upd:
                {
                    var path = ResolvePath(upd.Path);
                    var newContent = DeriveNewContents(path, upd.Chunks);

                    if (upd.MovePath != null)
                    {
                        var newPath = ResolvePath(upd.MovePath);
                        var dir = Path.GetDirectoryName(newPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        await File.WriteAllTextAsync(newPath, newContent);
                        File.Delete(path);
                        modified.Add($"{upd.Path} → {upd.MovePath}");
                    }
                    else
                    {
                        await File.WriteAllTextAsync(path, newContent);
                        modified.Add(upd.Path);
                    }
                    break;
                }
            }
        }

        var sb = new StringBuilder();
        if (added.Count > 0) sb.AppendLine($"Added: {string.Join(", ", added)}");
        if (modified.Count > 0) sb.AppendLine($"Modified: {string.Join(", ", modified)}");
        if (deleted.Count > 0) sb.AppendLine($"Deleted: {string.Join(", ", deleted)}");
        return sb.ToString().TrimEnd();
    }

    // ================================================================
    // 路径解析
    // ================================================================

    private string ResolvePath(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_workingDirectory, path));
        if (!fullPath.StartsWith(_workingDirectory))
            throw new UnauthorizedAccessException($"Path outside working directory: {path}");
        return fullPath;
    }

    // ================================================================
    // Patch 解析
    // ================================================================

    public static List<PatchHunk> ParsePatch(string patchText)
    {
        var cleaned = StripHeredoc(patchText.Trim());
        var lines = cleaned.Split('\n');
        var hunks = new List<PatchHunk>();

        var beginIdx = Array.FindIndex(lines, l => l.Trim() == "*** Begin Patch");
        var endIdx = Array.FindIndex(lines, l => l.Trim() == "*** End Patch");

        if (beginIdx == -1 || endIdx == -1 || beginIdx >= endIdx)
            throw new FormatException("Invalid patch format: missing *** Begin Patch / *** End Patch markers");

        var i = beginIdx + 1;

        while (i < endIdx)
        {
            var line = lines[i];

            if (line.StartsWith("*** Add File:"))
            {
                var filePath = line["*** Add File:".Length..].Trim();
                i++;
                var (contents, nextIdx) = ParseAddFileContent(lines, i, endIdx);
                hunks.Add(new AddHunk(filePath, contents));
                i = nextIdx;
            }
            else if (line.StartsWith("*** Delete File:"))
            {
                var filePath = line["*** Delete File:".Length..].Trim();
                hunks.Add(new DeleteHunk(filePath));
                i++;
            }
            else if (line.StartsWith("*** Update File:"))
            {
                var filePath = line["*** Update File:".Length..].Trim();
                i++;

                string? movePath = null;
                if (i < endIdx && lines[i].StartsWith("*** Move to:"))
                {
                    movePath = lines[i]["*** Move to:".Length..].Trim();
                    i++;
                }

                var (chunks, nextIdx) = ParseUpdateFileChunks(lines, i, endIdx);
                hunks.Add(new UpdateHunk(filePath, movePath, chunks));
                i = nextIdx;
            }
            else
            {
                i++;
            }
        }

        return hunks;
    }

    private static (string Contents, int NextIdx) ParseAddFileContent(string[] lines, int startIdx, int endIdx)
    {
        var sb = new StringBuilder();
        var i = startIdx;

        while (i < endIdx && !lines[i].StartsWith("***"))
        {
            if (lines[i].StartsWith("+"))
                sb.AppendLine(lines[i][1..]);
            i++;
        }

        var content = sb.ToString();
        // Remove trailing newline to match source exactly
        if (content.EndsWith("\n"))
            content = content[..^1];

        return (content, i);
    }

    private static (List<UpdateChunk> Chunks, int NextIdx) ParseUpdateFileChunks(string[] lines, int startIdx, int endIdx)
    {
        var chunks = new List<UpdateChunk>();
        var i = startIdx;

        while (i < endIdx && !lines[i].StartsWith("***"))
        {
            if (lines[i].StartsWith("@@"))
            {
                var contextLine = lines[i][2..].Trim();
                i++;

                var oldLines = new List<string>();
                var newLines = new List<string>();
                var isEndOfFile = false;

                while (i < endIdx && !lines[i].StartsWith("@@") && !lines[i].StartsWith("***"))
                {
                    var changeLine = lines[i];

                    if (changeLine == "*** End of File")
                    {
                        isEndOfFile = true;
                        i++;
                        break;
                    }

                    if (changeLine.StartsWith(" "))
                    {
                        var content = changeLine[1..];
                        oldLines.Add(content);
                        newLines.Add(content);
                    }
                    else if (changeLine.StartsWith("-"))
                    {
                        oldLines.Add(changeLine[1..]);
                    }
                    else if (changeLine.StartsWith("+"))
                    {
                        newLines.Add(changeLine[1..]);
                    }

                    i++;
                }

                chunks.Add(new UpdateChunk(
                    oldLines,
                    newLines,
                    string.IsNullOrEmpty(contextLine) ? null : contextLine,
                    isEndOfFile));
            }
            else
            {
                i++;
            }
        }

        return (chunks, i);
    }

    private static string StripHeredoc(string input)
    {
        // Match: cat <<'EOF'\n...\nEOF or <<EOF\n...\nEOF
        var match = System.Text.RegularExpressions.Regex.Match(
            input, @"^(?:cat\s+)?<<['""]?(\w+)['""]?\s*\n([\s\S]*?)\n\1\s*$");
        return match.Success ? match.Groups[2].Value : input;
    }

    // ================================================================
    // Patch 应用
    // ================================================================

    private static string DeriveNewContents(string filePath, List<UpdateChunk> chunks)
    {
        var originalContent = File.ReadAllText(filePath);
        var originalLines = originalContent.Split('\n').ToList();

        // Drop trailing empty element for consistent line counting
        if (originalLines.Count > 0 && originalLines[^1] == "")
            originalLines.RemoveAt(originalLines.Count - 1);

        var replacements = ComputeReplacements(originalLines, filePath, chunks);

        // Apply replacements in reverse order
        replacements.Sort((a, b) => a.StartIdx.CompareTo(b.StartIdx));
        for (var i = replacements.Count - 1; i >= 0; i--)
        {
            var r = replacements[i];
            originalLines.RemoveRange(r.StartIdx, r.OldLen);
            originalLines.InsertRange(r.StartIdx, r.NewSegment);
        }

        // Ensure trailing newline
        if (originalLines.Count == 0 || originalLines[^1] != "")
            originalLines.Add("");

        return string.Join("\n", originalLines);
    }

    private static List<Replacement> ComputeReplacements(
        List<string> originalLines, string filePath, List<UpdateChunk> chunks)
    {
        var replacements = new List<Replacement>();
        var lineIndex = 0;

        foreach (var chunk in chunks)
        {
            // Handle context-based seeking
            if (chunk.ChangeContext != null)
            {
                var contextIdx = SeekSequence(originalLines, new[] { chunk.ChangeContext }, lineIndex);
                if (contextIdx == -1)
                    throw new InvalidOperationException($"Failed to find context '{chunk.ChangeContext}' in {filePath}");
                lineIndex = contextIdx + 1;
            }

            // Pure addition (no old lines)
            if (chunk.OldLines.Count == 0)
            {
                var insertionIdx = originalLines.Count > 0 && originalLines[^1] == ""
                    ? originalLines.Count - 1
                    : originalLines.Count;
                replacements.Add(new Replacement(insertionIdx, 0, chunk.NewLines));
                continue;
            }

            // Try to match old lines
            var pattern = chunk.OldLines;
            var newSlice = chunk.NewLines;
            var found = SeekSequence(originalLines, pattern, lineIndex, chunk.IsEndOfFile);

            // Retry without trailing empty line
            if (found == -1 && pattern.Count > 0 && pattern[^1] == "")
            {
                pattern = pattern.Take(pattern.Count - 1).ToList();
                if (newSlice.Count > 0 && newSlice[^1] == "")
                    newSlice = newSlice.Take(newSlice.Count - 1).ToList();
                found = SeekSequence(originalLines, pattern, lineIndex, chunk.IsEndOfFile);
            }

            if (found != -1)
            {
                replacements.Add(new Replacement(found, pattern.Count, newSlice));
                lineIndex = found + pattern.Count;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Failed to find expected lines in {filePath}:\n{string.Join("\n", chunk.OldLines)}");
            }
        }

        return replacements;
    }

    // ================================================================
    // 多策略行匹配
    // ================================================================

    private static int SeekSequence(
        IList<string> lines, IList<string> pattern, int startIndex, bool eof = false)
    {
        if (pattern.Count == 0) return -1;

        // Pass 1: exact
        var result = TryMatch(lines, pattern, startIndex, (a, b) => a == b, eof);
        if (result != -1) return result;

        // Pass 2: rstrip
        result = TryMatch(lines, pattern, startIndex, (a, b) => a.TrimEnd() == b.TrimEnd(), eof);
        if (result != -1) return result;

        // Pass 3: trim
        result = TryMatch(lines, pattern, startIndex, (a, b) => a.Trim() == b.Trim(), eof);
        if (result != -1) return result;

        // Pass 4: unicode normalized
        result = TryMatch(lines, pattern, startIndex,
            (a, b) => NormalizeUnicode(a.Trim()) == NormalizeUnicode(b.Trim()), eof);
        return result;
    }

    private static int TryMatch(
        IList<string> lines, IList<string> pattern, int startIndex,
        Func<string, string, bool> compare, bool eof)
    {
        // EOF anchor: try matching from end first
        if (eof)
        {
            var fromEnd = lines.Count - pattern.Count;
            if (fromEnd >= startIndex && MatchAt(lines, pattern, fromEnd, compare))
                return fromEnd;
        }

        // Forward search
        for (var i = startIndex; i <= lines.Count - pattern.Count; i++)
        {
            if (MatchAt(lines, pattern, i, compare))
                return i;
        }

        return -1;
    }

    private static bool MatchAt(
        IList<string> lines, IList<string> pattern, int index, Func<string, string, bool> compare)
    {
        for (var j = 0; j < pattern.Count; j++)
        {
            if (!compare(lines[index + j], pattern[j]))
                return false;
        }
        return true;
    }

    private static string NormalizeUnicode(string str)
    {
        return str
            .Replace('\u2018', '\'').Replace('\u2019', '\'')
            .Replace('\u201A', '\'').Replace('\u201B', '\'')
            .Replace('\u201C', '"').Replace('\u201D', '"')
            .Replace('\u201E', '"').Replace('\u201F', '"')
            .Replace('\u2010', '-').Replace('\u2011', '-')
            .Replace('\u2012', '-').Replace('\u2013', '-')
            .Replace('\u2014', '-').Replace('\u2015', '-')
            .Replace("\u2026", "...")
            .Replace('\u00A0', ' ');
    }

    // ================================================================
    // 数据类型
    // ================================================================

    public abstract record PatchHunk(string Path);
    public record AddHunk(string Path, string Contents) : PatchHunk(Path);
    public record DeleteHunk(string Path) : PatchHunk(Path);
    public record UpdateHunk(string Path, string? MovePath, List<UpdateChunk> Chunks) : PatchHunk(Path);

    public record UpdateChunk(
        List<string> OldLines,
        List<string> NewLines,
        string? ChangeContext,
        bool IsEndOfFile);

    private record Replacement(int StartIdx, int OldLen, IList<string> NewSegment);
}
