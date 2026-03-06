# 任务 1.5: 更智能的 Edit 工具

## 阶段
Phase 1 — 核心体验提升

## 目标
精确匹配失败时启用多种模糊匹配策略，提高 edit 工具的成功率。

## 背景
当前 `FileTools.Edit` 要求 `oldString` 完全精确匹配（包括空白）。LLM 经常因为缩进差异、尾部空白、换行符差异导致 edit 失败。opencode 有 9 种替换策略，按精确度递减依次尝试。

## 当前代码分析

`src/tools/FileTools.cs` 第 119-144 行：
- 精确匹配 `content.Contains(oldString)`
- 唯一性检查 `CountOccurrences`
- 匹配失败直接抛异常

## 设计方案

### 1. 替换策略接口

```csharp
// src/tools/EditReplacer.cs

public interface IEditReplacer
{
    /// <summary>策略名称</summary>
    string Name { get; }

    /// <summary>尝试在 content 中找到 oldString 并替换为 newString</summary>
    /// <returns>替换后的内容，null 表示未找到匹配</returns>
    string? TryReplace(string content, string oldString, string newString);
}
```

### 2. 替换策略实现

按精确度从高到低排列：

```csharp
/// <summary>1. 精确匹配 (当前行为)</summary>
public class ExactReplacer : IEditReplacer
{
    public string Name => "Exact";
    public string? TryReplace(string content, string old, string @new)
    {
        if (!content.Contains(old)) return null;
        if (CountOccurrences(content, old) > 1) return null;
        return content.Replace(old, @new);
    }
}

/// <summary>2. 行尾空白忽略</summary>
public class LineTrimmedReplacer : IEditReplacer
{
    public string Name => "LineTrimmed";
    public string? TryReplace(string content, string old, string @new)
    {
        // 将 content 和 old 的每行 TrimEnd 后匹配
        var contentNorm = NormalizeLines(content);
        var oldNorm = NormalizeLines(old);
        var idx = contentNorm.IndexOf(oldNorm, StringComparison.Ordinal);
        if (idx < 0) return null;
        // 找到原始内容中对应的范围并替换
        var (start, end) = MapToOriginal(content, contentNorm, idx, oldNorm.Length);
        return content[..start] + @new + content[end..];
    }
}

/// <summary>3. 空白归一化 (多个空白视为一个)</summary>
public class WhitespaceNormalizedReplacer : IEditReplacer { ... }

/// <summary>4. 缩进灵活匹配 (忽略统一的缩进偏移)</summary>
public class IndentationFlexibleReplacer : IEditReplacer
{
    // 检测 old 和 content 中对应块的缩进差异
    // 如果差异是统一的偏移量 (如都多了4个空格)，允许匹配
}

/// <summary>5. 锚点块匹配 (首尾行精确匹配，中间灵活)</summary>
public class BlockAnchorReplacer : IEditReplacer
{
    // 用 old 的首行和尾行作为锚点
    // 找到两个锚点之间的块进行替换
}

/// <summary>6. 转义归一化 (不同引号/换行符风格)</summary>
public class EscapeNormalizedReplacer : IEditReplacer { ... }
```

### 3. 策略链执行

```csharp
// src/tools/EditStrategy.cs

public class EditStrategy
{
    private static readonly IEditReplacer[] Replacers = new IEditReplacer[]
    {
        new ExactReplacer(),
        new LineTrimmedReplacer(),
        new WhitespaceNormalizedReplacer(),
        new IndentationFlexibleReplacer(),
        new BlockAnchorReplacer(),
        new EscapeNormalizedReplacer()
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
```

### 4. 改造 FileTools.Edit

```csharp
public async Task<string> Edit(string path, string oldString, string newString)
{
    var fullPath = ResolvePath(path);
    var content = await File.ReadAllTextAsync(fullPath);

    var result = EditStrategy.TryReplace(content, oldString, newString);

    if (!result.Success)
    {
        // 计算最相似的片段用于错误提示
        var suggestion = FindMostSimilar(content, oldString);
        throw new InvalidOperationException(
            $"Could not find matching text in {path}. " +
            $"Closest match (similarity {suggestion.Similarity:P0}):\n{suggestion.Text}");
    }

    await File.WriteAllTextAsync(fullPath, result.Content);

    var strategyNote = result.Strategy != "Exact"
        ? $" (matched via {result.Strategy})"
        : "";
    return $"Successfully replaced text in {path}.{strategyNote}";
}
```

### 5. Levenshtein 相似度 (错误提示用)

```csharp
// src/tools/TextSimilarity.cs

public static class TextSimilarity
{
    /// <summary>
    /// 在 content 中找到与 target 最相似的子串
    /// </summary>
    public static (string Text, double Similarity) FindMostSimilar(
        string content, string target, int windowSize = 0)
    {
        if (windowSize <= 0)
            windowSize = target.Length + target.Length / 4;

        var lines = content.Split('\n');
        var targetLines = target.Split('\n').Length;
        var bestMatch = "";
        var bestSimilarity = 0.0;

        for (var i = 0; i <= lines.Length - targetLines; i++)
        {
            var candidate = string.Join("\n",
                lines.Skip(i).Take(targetLines + 2));
            var sim = CalculateSimilarity(candidate, target);
            if (sim > bestSimilarity)
            {
                bestSimilarity = sim;
                bestMatch = candidate;
            }
        }

        return (bestMatch, bestSimilarity);
    }

    /// <summary>
    /// 计算两个字符串的相似度 (0-1)
    /// </summary>
    public static double CalculateSimilarity(string a, string b)
    {
        var distance = LevenshteinDistance(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        return maxLen == 0 ? 1.0 : 1.0 - (double)distance / maxLen;
    }

    private static int LevenshteinDistance(string a, string b) { ... }
}
```

## 实现步骤

1. [ ] 创建 `src/tools/EditReplacer.cs` — 替换策略接口
2. [ ] 实现 `ExactReplacer` (提取当前逻辑)
3. [ ] 实现 `LineTrimmedReplacer`
4. [ ] 实现 `WhitespaceNormalizedReplacer`
5. [ ] 实现 `IndentationFlexibleReplacer`
6. [ ] 实现 `BlockAnchorReplacer`
7. [ ] 创建 `src/tools/EditStrategy.cs` — 策略链
8. [ ] 创建 `src/tools/TextSimilarity.cs` — Levenshtein + 相似匹配
9. [ ] 改造 `FileTools.Edit` — 使用策略链
10. [ ] 匹配失败时返回最相似片段提示

## 验收标准
- [ ] 精确匹配仍然优先
- [ ] 行尾多余空白不影响匹配
- [ ] 统一缩进偏移不影响匹配
- [ ] 非精确匹配时输出中标注使用的策略
- [ ] 完全无匹配时显示最相似片段

## 参考
- opencode: `src/tool/edit.ts` (9 种替换策略, Levenshtein)

## 相关文件
- `src/tools/EditReplacer.cs` — 新建
- `src/tools/EditStrategy.cs` — 新建
- `src/tools/TextSimilarity.cs` — 新建
- `src/tools/FileTools.cs` — 改造 Edit 方法
