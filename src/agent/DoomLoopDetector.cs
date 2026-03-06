// =============================================================================
// Doom Loop 检测器
// =============================================================================
// 检测 Agent 循环中重复的工具调用模式
// 连续 N 次相同调用 (工具名 + 参数) 触发中断
// =============================================================================

namespace Morty.Agent;

/// <summary>
/// Doom Loop 检测器
/// </summary>
public class DoomLoopDetector
{
    private readonly int _threshold;
    private readonly List<string> _recentSignatures = new();

    public int Threshold => _threshold;

    public DoomLoopDetector(int threshold = 3)
    {
        _threshold = threshold;
    }

    /// <summary>
    /// 记录一次工具调用，返回是否检测到 doom loop
    /// </summary>
    public bool RecordAndCheck(string toolName, IDictionary<string, object?>? args)
    {
        var signature = BuildSignature(toolName, args);
        _recentSignatures.Add(signature);

        if (_recentSignatures.Count < _threshold)
            return false;

        var recent = _recentSignatures
            .Skip(_recentSignatures.Count - _threshold)
            .ToList();

        return recent.All(s => s == recent[0]);
    }

    /// <summary>
    /// 重置检测器
    /// </summary>
    public void Reset() => _recentSignatures.Clear();

    private static string BuildSignature(string toolName, IDictionary<string, object?>? args)
    {
        if (args == null || args.Count == 0)
            return toolName;

        var sortedArgs = args
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}={kv.Value}")
            .ToArray();

        return $"{toolName}({string.Join(",", sortedArgs)})";
    }
}
