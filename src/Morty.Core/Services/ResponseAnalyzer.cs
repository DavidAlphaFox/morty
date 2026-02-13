using System.Text.Json;
using System.Text.RegularExpressions;
using Morty.Core.Interfaces;

namespace Morty.Core.Services;

/// <summary>
/// 响应分析器 - 分析 Claude 输出
/// </summary>
public partial class ResponseAnalyzer : IResponseAnalyzer
{
    /// <summary>
    /// 分析输出
    /// </summary>
    public AnalysisResult Analyze(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return new AnalysisResult(
                IsComplete: false,
                TestsPassing: false,
                HasErrors: true,
                ChangedFiles: [],
                ErrorMessage: "空输出"
            );
        }

        var hasErrors = ErrorPattern().IsMatch(output);
        var testsPassing = TestsPassingPattern().IsMatch(output);
        var testsFailing = TestsFailingPattern().IsMatch(output);
        var isComplete = IsCompletePattern().IsMatch(output);
        var changedFiles = ExtractChangedFiles(output);

        return new AnalysisResult(
            IsComplete: isComplete && !hasErrors,
            TestsPassing: testsPassing && !testsFailing,
            HasErrors: hasErrors,
            ChangedFiles: changedFiles,
            ErrorMessage: hasErrors ? ExtractErrorMessage(output) : null
        );
    }

    /// <summary>
    /// 提取计划内容
    /// </summary>
    public PlanResult? ExtractPlan(string output)
    {
        var planMatch = PlanPattern().Match(output);
        if (!planMatch.Success)
            return null;

        var planContent = planMatch.Groups[1].Value.Trim();
        var tasks = planContent
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        return new PlanResult(planContent, tasks);
    }

    /// <summary>
    /// 检查实现是否完成
    /// </summary>
    public bool IsImplementationComplete(string output)
    {
        return IsCompletePattern().IsMatch(output) && !ErrorPattern().IsMatch(output);
    }

    /// <summary>
    /// 检查测试是否通过
    /// </summary>
    public bool AreTestsPassing(string output)
    {
        return TestsPassingPattern().IsMatch(output) && !TestsFailingPattern().IsMatch(output);
    }

    /// <summary>
    /// 提取修改的文件
    /// </summary>
    public IEnumerable<string> ExtractChangedFiles(string output)
    {
        var files = new List<string>();

        var modifiedMatch = ModifiedFilesPattern().Match(output);
        if (modifiedMatch.Success)
        {
            var filesContent = modifiedMatch.Groups[1].Value;
            files.AddRange(filesContent
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .Where(f => !string.IsNullOrWhiteSpace(f)));
        }

        return files.Distinct();
    }

    /// <summary>
    /// 提取错误消息
    /// </summary>
    private static string? ExtractErrorMessage(string output)
    {
        var errorMatch = ErrorPattern().Match(output);
        return errorMatch.Success ? errorMatch.Groups[1].Value.Trim() : "未知错误";
    }

    // 错误模式匹配
    [GeneratedRegex(@"(?i)(error|failed|exception|fatal)", RegexOptions.Multiline)]
    private static partial Regex ErrorPattern();

    // 测试通过模式匹配
    [GeneratedRegex(@"(?i)(tests?\s+(pass|passed|success|ok)|all\s+tests?\s+passed)", RegexOptions.Multiline)]
    private static partial Regex TestsPassingPattern();

    // 测试失败模式匹配
    [GeneratedRegex(@"(?i)(tests?\s+(fail|failures?|error)|failed|FAIL)", RegexOptions.Multiline)]
    private static partial Regex TestsFailingPattern();

    // 完成状态模式匹配
    [GeneratedRegex(@"(?i)(complete|done|finished|implementation\s+complete)", RegexOptions.Multiline)]
    private static partial Regex IsCompletePattern();

    // 计划内容模式匹配
    [GeneratedRegex(@"```(?:json)?\s*\{[\s\S]*?""Plan""[\s\S]*?\}", RegexOptions.Multiline)]
    private static partial Regex PlanPattern();

    // 修改文件模式匹配
    [GeneratedRegex(@"(?i)(modified|changed|created|files?)[\s\S]*?```\s*([\s\S]*?)```", RegexOptions.Multiline)]
    private static partial Regex ModifiedFilesPattern();
}
