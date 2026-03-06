using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Morty.Agent;

/// <summary>
/// 问题选项
/// </summary>
public class QuestionOption
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

/// <summary>
/// 问题定义
/// </summary>
public class QuestionInfo
{
    [JsonPropertyName("question")]
    public string Question { get; set; } = "";

    [JsonPropertyName("options")]
    public List<QuestionOption> Options { get; set; } = new();

    [JsonPropertyName("multiple")]
    public bool Multiple { get; set; }
}

/// <summary>
/// 问题请求
/// </summary>
public class QuestionRequest
{
    public List<QuestionInfo> Questions { get; set; } = new();
}

/// <summary>
/// 用户提问工具
/// </summary>
public class QuestionTool
{
    private readonly Func<QuestionRequest, Task<List<List<string>>>>? _askCallback;

    public QuestionTool(Func<QuestionRequest, Task<List<List<string>>>>? askCallback)
    {
        _askCallback = askCallback;
    }

    /// <summary>
    /// 向用户提问
    /// </summary>
    public async Task<string> AskAsync(string questionsJson)
    {
        if (_askCallback == null)
            return "Error: question tool is not available in this mode.";

        List<QuestionInfo> questions;
        try
        {
            questions = JsonSerializer.Deserialize<List<QuestionInfo>>(questionsJson)
                ?? throw new JsonException("null result");
        }
        catch (JsonException ex)
        {
            return $"Error: failed to parse questions JSON: {ex.Message}";
        }

        if (questions.Count == 0)
            return "Error: no questions provided.";

        var request = new QuestionRequest { Questions = questions };
        var answers = await _askCallback(request);

        var sb = new StringBuilder();
        for (var i = 0; i < questions.Count && i < answers.Count; i++)
        {
            if (questions.Count > 1)
                sb.Append($"Q{i + 1}: ");
            sb.AppendLine(string.Join(", ", answers[i]));
        }
        return sb.ToString().TrimEnd();
    }
}
