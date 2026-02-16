using System.Text.RegularExpressions;
using Morty.Core.Interfaces;

namespace Morty.Core.Services;

/// <summary>
/// 响应分析器
/// 主要依赖进程 exit code（processSuccess）判断成功/失败
/// 正则匹配仅用于提取辅助信息（文件列表等），不作为成败判断依据
/// </summary>
public partial class ResponseAnalyzer : IResponseAnalyzer
{
    /// <summary>
    /// 基于 exit code 和输出内容分析结果
    /// </summary>
    /// <param name="output">Claude CLI 的输出文本</param>
    /// <param name="processSuccess">进程是否成功（exit code 0 且无 is_error）</param>
    public AnalysisResult Analyze(string output, bool processSuccess)
    {
        // 空输出 + 失败 → 明确失败
        if (string.IsNullOrWhiteSpace(output) && !processSuccess)
        {
            return new AnalysisResult(
                IsComplete: false,
                TestsPassing: false,
                HasErrors: true,
                ChangedFiles: [],
                ErrorMessage: "空输出且进程失败"
            );
        }

        // 空输出 + 成功 → 成功但无内容
        if (string.IsNullOrWhiteSpace(output) && processSuccess)
        {
            return new AnalysisResult(
                IsComplete: true,
                TestsPassing: false,
                HasErrors: false,
                ChangedFiles: [],
                ErrorMessage: null
            );
        }

        var changedFiles = ExtractChangedFiles(output);

        // 主要依赖 processSuccess 判断
        return new AnalysisResult(
            IsComplete: processSuccess,
            TestsPassing: processSuccess,
            HasErrors: !processSuccess,
            ChangedFiles: changedFiles,
            ErrorMessage: processSuccess ? null : "进程执行失败"
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
    /// 提取修改的文件列表
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

    // 计划内容模式：匹配 JSON 代码块中的 Plan 字段
    [GeneratedRegex(@"```(?:json)?\s*\{[\s\S]*?""Plan""[\s\S]*?\}", RegexOptions.Multiline)]
    private static partial Regex PlanPattern();

    // 修改文件模式：匹配包含 modified, changed, created, files 的代码块
    [GeneratedRegex(@"(?i)(modified|changed|created|files?)[\s\S]*?```\s*([\s\S]*?)```", RegexOptions.Multiline)]
    private static partial Regex ModifiedFilesPattern();
}
