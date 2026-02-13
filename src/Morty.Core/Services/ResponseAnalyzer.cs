using System.Text.Json;
using System.Text.RegularExpressions;
using Morty.Core.Interfaces;

namespace Morty.Core.Services;

public partial class ResponseAnalyzer : IResponseAnalyzer
{
    public AnalysisResult Analyze(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return new AnalysisResult(
                IsComplete: false,
                TestsPassing: false,
                HasErrors: true,
                ChangedFiles: [],
                ErrorMessage: "Empty output"
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

    public bool IsImplementationComplete(string output)
    {
        return IsCompletePattern().IsMatch(output) && !ErrorPattern().IsMatch(output);
    }

    public bool AreTestsPassing(string output)
    {
        return TestsPassingPattern().IsMatch(output) && !TestsFailingPattern().IsMatch(output);
    }

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

    private static string? ExtractErrorMessage(string output)
    {
        var errorMatch = ErrorPattern().Match(output);
        return errorMatch.Success ? errorMatch.Groups[1].Value.Trim() : "Unknown error";
    }

    [GeneratedRegex(@"(?i)(error|failed|exception|fatal)", RegexOptions.Multiline)]
    private static partial Regex ErrorPattern();

    [GeneratedRegex(@"(?i)(tests?\s+(pass|passed|success|ok)|all\s+tests?\s+passed)", RegexOptions.Multiline)]
    private static partial Regex TestsPassingPattern();

    [GeneratedRegex(@"(?i)(tests?\s+(fail|failures?|error)|failed|FAIL)", RegexOptions.Multiline)]
    private static partial Regex TestsFailingPattern();

    [GeneratedRegex(@"(?i)(complete|done|finished|implementation\s+complete)", RegexOptions.Multiline)]
    private static partial Regex IsCompletePattern();

    [GeneratedRegex(@"```(?:json)?\s*\{[\s\S]*?""Plan""[\s\S]*?\}", RegexOptions.Multiline)]
    private static partial Regex PlanPattern();

    [GeneratedRegex(@"(?i)(modified|changed|created|files?)[\s\S]*?```\s*([\s\S]*?)```", RegexOptions.Multiline)]
    private static partial Regex ModifiedFilesPattern();
}
