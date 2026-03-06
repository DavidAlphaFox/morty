# 任务 3.3: Skill 系统

## 阶段
Phase 3 — 高级特性

## 目标
支持从 `.morty/skills/` 加载可复用指令模板，Agent 可通过 `skill` 工具按需加载。

## 设计方案

### 1. Skill 格式

```markdown
<!-- .morty/skills/refactor.md -->
---
name: refactor
description: Code refactoring guidelines
---

When refactoring code, follow these steps:
1. Read the entire file first
2. Identify code smells
3. Apply one refactoring at a time
4. Run tests after each change
...
```

### 2. Skill 加载器

```csharp
// src/agent/SkillLoader.cs

public class SkillInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Content { get; set; } = "";
    public string SourcePath { get; set; } = "";
}

public class SkillLoader
{
    public List<SkillInfo> LoadSkills(string cwd)
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
                skills.Add(ParseSkillFile(file));
        }

        return skills;
    }

    private SkillInfo ParseSkillFile(string path)
    {
        var content = File.ReadAllText(path);
        // 解析 frontmatter (--- ... ---)
        ...
    }
}
```

### 3. Skill 工具

```csharp
tools.Add(AIFunctionFactory.Create(
    ([Description("Skill name to load")] string name) =>
        skillLoader.LoadSkill(name),
    "skill",
    $"Load a skill (reusable instruction template). Available skills: {skillList}"));
```

## 实现步骤

1. [ ] 创建 `src/agent/SkillLoader.cs` — Markdown + frontmatter 解析
2. [ ] `ToolRegistry.cs` — 注册 skill 工具
3. [ ] 扫描 `.morty/skills/` 和 `~/.morty/skills/`
4. [ ] 工具描述动态列出可用 skills

## 验收标准
- [ ] 从 `.morty/skills/` 加载 Markdown skills
- [ ] Agent 可通过 skill 工具按名称加载
- [ ] 工具描述中列出可用 skill 名称

## 参考
- opencode: `src/skill/skill.ts`, `src/tool/skill.ts`

## 相关文件
- `src/agent/SkillLoader.cs` — 新建
- `src/agent/ToolRegistry.cs` — 注册
