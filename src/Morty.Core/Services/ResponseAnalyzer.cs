using System.Text.Json;
using System.Text.RegularExpressions;
using Morty.Core.Interfaces;

namespace Morty.Core.Services;

/// <summary>
/// 响应分析器
/// 使用正则表达式分析 Claude CLI 的输出
/// 用于判断任务完成状态、测试结果、错误信息等
/// </summary>
public partial class ResponseAnalyzer : IResponseAnalyzer
{
    /// <summary>
    /// 分析输出文本，返回完整的分析结果
    /// 检查错误、测试状态、完成状态和修改的文件
    /// </summary>
    /// <param name="output">Claude CLI 的输出文本</param>
    /// <returns>包含各项分析结果的 AnalysisResult 对象</returns>
    public AnalysisResult Analyze(string output)
    {
        // 空输出检查
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

        // 使用正则表达式匹配各项指标
        var hasErrors = ErrorPattern().IsMatch(output);
        var testsPassing = TestsPassingPattern().IsMatch(output);
        var testsFailing = TestsFailingPattern().IsMatch(output);
        var isComplete = IsCompletePattern().IsMatch(output);
        var changedFiles = ExtractChangedFiles(output);

        return new AnalysisResult(
            // 完成 = 有完成标记且无错误
            IsComplete: isComplete && !hasErrors,
            // 测试通过 = 有通过标记且无失败标记
            TestsPassing: testsPassing && !testsFailing,
            HasErrors: hasErrors,
            ChangedFiles: changedFiles,
            ErrorMessage: hasErrors ? ExtractErrorMessage(output) : null
        );
    }

    /// <summary>
    /// 提取计划内容
    /// 从输出中解析 JSON 格式的计划内容
    /// </summary>
    /// <param name="output">Claude CLI 的输出文本</param>
    /// <returns>PlanResult 对象，如果未找到计划则返回 null</returns>
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
    /// 通过检测完成状态标记且无错误来判断
    /// </summary>
    /// <param name="output">Claude CLI 的输出文本</param>
    /// <returns>如果实现完成返回 true</returns>
    public bool IsImplementationComplete(string output)
    {
        return IsCompletePattern().IsMatch(output) && !ErrorPattern().IsMatch(output);
    }

    /// <summary>
    /// 检查测试是否通过
    /// 通过检测测试通过标记且无失败标记来判断
    /// </summary>
    /// <param name="output">Claude CLI 的输出文本</param>
    /// <returns>如果测试通过返回 true</returns>
    public bool AreTestsPassing(string output)
    {
        return TestsPassingPattern().IsMatch(output) && !TestsFailingPattern().IsMatch(output);
    }

    /// <summary>
    /// 提取修改的文件列表
    /// 从输出中解析包含文件列表的代码块
    /// </summary>
    /// <param name="output">Claude CLI 的输出文本</param>
    /// <returns>修改的文件路径集合</returns>
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
    /// 从输出中提取第一个匹配的错误信息
    /// </summary>
    /// <param name="output">Claude CLI 的输出文本</param>
    /// <returns>错误消息字符串</returns>
    private static string? ExtractErrorMessage(string output)
    {
        var errorMatch = ErrorPattern().Match(output);
        return errorMatch.Success ? errorMatch.Groups[1].Value.Trim() : "未知错误";
    }

    // 错误模式：匹配 error, failed, exception, fatal 等关键词
    [GeneratedRegex(@"(?i)(error|failed|exception|fatal)", RegexOptions.Multiline)]
    private static partial Regex ErrorPattern();

    // 测试通过模式：匹配 tests pass, tests passed, all tests passed 等
    [GeneratedRegex(@"(?i)(tests?\s+(pass|passed|success|ok)|all\s+tests?\s+passed)", RegexOptions.Multiline)]
    private static partial Regex TestsPassingPattern();

    // 测试失败模式：匹配 tests fail, tests failed, FAIL 等
    [GeneratedRegex(@"(?i)(tests?\s+(fail|failures?|error)|failed|FAIL)", RegexOptions.Multiline)]
    private static partial Regex TestsFailingPattern();

    // 完成状态模式：匹配 complete, done, finished, implementation complete
    [GeneratedRegex(@"(?i)(complete|done|finished|implementation\s+complete)", RegexOptions.Multiline)]
    private static partial Regex IsCompletePattern();

    // 计划内容模式：匹配 JSON 代码块中的 Plan 字段
    [GeneratedRegex(@"```(?:json)?\s*\{[\s\S]*?""Plan""[\s\S]*?\}", RegexOptions.Multiline)]
    private static partial Regex PlanPattern();

    // 修改文件模式：匹配包含 modified, changed, created, files 的代码块
    [GeneratedRegex(@"(?i)(modified|changed|created|files?)[\s\S]*?```\s*([\s\S]*?)```", RegexOptions.Multiline)]
    private static partial Regex ModifiedFilesPattern();
}
