// =============================================================================
// Skill 系统
// =============================================================================
// 从 .morty/skills/ 加载可复用指令模板 (Markdown + frontmatter)
// Agent 可通过 skill 工具按需加载
// =============================================================================

using System.Text.RegularExpressions;

namespace Morty.Agent;

/// <summary>
/// Skill 信息
/// </summary>
public class SkillInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Content { get; set; } = "";
    public string SourcePath { get; set; } = "";
}

/// <summary>
/// Skill 加载器
/// </summary>
public class SkillLoader
{
    private readonly List<SkillInfo> _skills;

    public SkillLoader(string cwd)
    {
        _skills = LoadAllSkills(cwd);
    }

    /// <summary>
    /// 获取所有可用 skill 名称
    /// </summary>
    public List<string> ListSkills() => _skills.Select(s => s.Name).ToList();

    /// <summary>
    /// 按名称加载 skill 内容
    /// </summary>
    public string LoadSkill(string name)
    {
        var skill = _skills.FirstOrDefault(s =>
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (skill == null)
        {
            var available = string.Join(", ", _skills.Select(s => s.Name));
            return $"Error: Skill '{name}' not found. Available skills: {available}";
        }

        return $"[Skill: {skill.Name}]\n{skill.Description}\n\n{skill.Content}";
    }

    /// <summary>
    /// 是否有可用 skills
    /// </summary>
    public bool HasSkills => _skills.Count > 0;

    private static List<SkillInfo> LoadAllSkills(string cwd)
    {
        var skills = new List<SkillInfo>();
        var dirs = new[]
        {
            Path.Combine(cwd, ".morty", "skills"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".morty", "skills")
        };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.md"))
            {
                var skill = ParseSkillFile(file);
                if (skill != null)
                    skills.Add(skill);
            }
        }

        return skills;
    }

    private static SkillInfo? ParseSkillFile(string path)
    {
        var content = File.ReadAllText(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var description = "";

        // 解析 frontmatter (--- ... ---)
        var frontmatterMatch = Regex.Match(content, @"^---\s*\n(.*?)\n---\s*\n", RegexOptions.Singleline);
        if (frontmatterMatch.Success)
        {
            var frontmatter = frontmatterMatch.Groups[1].Value;
            content = content[frontmatterMatch.Length..];

            // 简单键值解析
            var nameMatch = Regex.Match(frontmatter, @"^name:\s*(.+)$", RegexOptions.Multiline);
            if (nameMatch.Success)
                name = nameMatch.Groups[1].Value.Trim();

            var descMatch = Regex.Match(frontmatter, @"^description:\s*(.+)$", RegexOptions.Multiline);
            if (descMatch.Success)
                description = descMatch.Groups[1].Value.Trim();
        }

        return new SkillInfo
        {
            Name = name,
            Description = description,
            Content = content.Trim(),
            SourcePath = path
        };
    }
}
