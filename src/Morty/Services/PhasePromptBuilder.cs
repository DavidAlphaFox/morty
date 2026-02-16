using Morty.Entities;

namespace Morty.Services;

/// <summary>
/// 阶段提示词构建器 - 根据故事当前阶段生成对应的 AI 提示词
/// 每个阶段有独立的模板方法，支持动态插入 PRD、计划、验收标准等上下文
/// 注册为 Singleton（纯函数，无状态）
/// </summary>
public class PhasePromptBuilder
{
    /// <summary>
    /// 根据当前阶段构建提示词
    /// </summary>
    /// <param name="prdJson">项目 PRD 文档（JSON 格式）</param>
    /// <param name="story">当前故事实体</param>
    /// <param name="detailedPlan">之前生成的详细实施计划（RequirementsPlanning 阶段为 null）</param>
    /// <param name="acceptanceCriteria">之前生成的验收标准（RequirementsPlanning/AcceptancePlanning 阶段为 null）</param>
    /// <returns>(提示词文本, 是否使用 Plan 模式)</returns>
    public (string prompt, bool usePlanMode) Build(
        string prdJson,
        Story story,
        string? detailedPlan,
        string? acceptanceCriteria)
    {
        var usePlanMode = story.Phase switch
        {
            StoryPhase.RequirementsPlanning => true,
            StoryPhase.AcceptancePlanning => true,
            StoryPhase.Acceptance => false,
            _ => false
        };

        var prdSection = string.IsNullOrWhiteSpace(prdJson) ? "" : $"\n项目 PRD:\n{prdJson}\n";

        var prompt = story.Phase switch
        {
            StoryPhase.RequirementsPlanning => BuildRequirementsPlanningPrompt(story, prdSection),
            StoryPhase.AcceptancePlanning => BuildAcceptancePlanningPrompt(story, detailedPlan),
            StoryPhase.Executing => BuildExecutingPrompt(story, detailedPlan),
            StoryPhase.Testing => BuildTestingPrompt(story, detailedPlan, acceptanceCriteria),
            StoryPhase.Acceptance => BuildAcceptancePrompt(story, acceptanceCriteria),
            _ => $"""
                用户故事: {story.Title}
                故事 ID: {story.StoryId}
                {prdSection}
                """
        };

        return (prompt, usePlanMode);
    }

    /// <summary>
    /// 需求规划提示词 - 分析需求并生成实施计划
    /// 包含遗漏任务发现指令，AI 会在回复末尾输出 discoveredTasks JSON
    /// </summary>
    private static string BuildRequirementsPlanningPrompt(Story story, string prdSection)
    {
        return $$"""
            请分析以下用户故事需求，并生成详细的实施计划。

            用户故事: {{story.Title}}
            故事 ID: {{story.StoryId}}

            原始需求:
            {{story.Requirements}}
            {{prdSection}}

            请生成详细的实施计划，包括：
            1. 需要实现的功能
            2. 需要修改或创建的文件
            3. 实现步骤
            4. 潜在的技术难点

            同时，请分析是否存在当前 backlog（待办列表）中遗漏的任务。如果发现遗漏的任务，请在回复最后以以下 JSON 格式输出：

            ```json
            {
                "discoveredTasks": [
                    {
                        "title": "任务标题",
                        "requirements": "详细需求描述",
                        "priority": "High|Medium|Low",
                        "reason": "为什么需要这个任务"
                    }
                ]
            }
            ```

            如果没有发现遗漏任务，请输出 "discoveredTasks": []
            """;
    }

    /// <summary>
    /// 验收标准细化提示词 - 基于实施计划和用户验收标准生成详细验收标准
    /// </summary>
    private static string BuildAcceptancePlanningPrompt(Story story, string? detailedPlan)
    {
        return $"""
            请根据以下用户故事、需求和原始验收标准，细化验收标准。

            用户故事: {story.Title}
            故事 ID: {story.StoryId}

            详细实施计划:
            {detailedPlan ?? "(暂无)"}

            用户提供的验收标准:
            {story.UserAcceptanceCriteria}

            请基于以上信息生成细化的验收标准，包括：
            1. 功能验收标准（具体、可测试）
            2. 非功能验收标准（如性能、安全等）
            3. 边界条件和异常情况
            """;
    }

    /// <summary>
    /// 代码实施提示词 - 根据详细计划执行编码工作
    /// </summary>
    private static string BuildExecutingPrompt(Story story, string? detailedPlan)
    {
        return $"""
            请根据以下详细计划实施代码。

            用户故事: {story.Title}
            故事 ID: {story.StoryId}

            详细实施计划:
            {detailedPlan ?? "(暂无)"}

            请实现更改并回复:
            1. 您做了什么
            2. 修改了哪些文件
            3. 实现是否完成或还有什么待完成
            """;
    }

    /// <summary>
    /// 单元测试生成提示词 - 根据实施计划和验收标准生成测试
    /// </summary>
    private static string BuildTestingPrompt(Story story, string? detailedPlan, string? acceptanceCriteria)
    {
        return $"""
            请为以下已实现的代码生成单元测试。

            用户故事: {story.Title}
            故事 ID: {story.StoryId}

            详细实施计划:
            {detailedPlan ?? "(暂无)"}

            验收标准:
            {acceptanceCriteria ?? "(暂无)"}

            请生成单元测试代码，确保：
            1. 测试覆盖验收标准中的所有功能点
            2. 测试代码能够编译和运行
            3. 包含边界条件的测试用例
            """;
    }

    /// <summary>
    /// 验收验证提示词 - 验证实现是否满足验收标准
    /// </summary>
    private static string BuildAcceptancePrompt(Story story, string? acceptanceCriteria)
    {
        return $"""
            请根据以下验收标准验证实现是否满足要求。

            用户故事: {story.Title}
            故事 ID: {story.StoryId}

            验收标准:
            {acceptanceCriteria ?? "(暂无)"}

            请验证实现并回复：
            1. 每个验收标准是否满足
            2. 如不满足，说明原因
            3. 总体评估：是否通过验收
            """;
    }
}
