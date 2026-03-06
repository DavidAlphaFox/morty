# 任务 2.4: Glob 工具改进

## 阶段
Phase 2 — 功能扩展

## 目标
将 `find` 工具升级为 `glob` 工具，支持 glob 模式匹配、按修改时间排序、自动排除常见目录。

## 背景
当前 `find` 工具直接调用系统 `find` 命令，不支持 glob 语法糖、不排除 .git/node_modules、不按修改时间排序。opencode 使用 ripgrep 实现高性能 glob。

## 设计方案

### 改造 SystemTools

```csharp
// SystemTools.cs — 替换 Find 方法

public async Task<string> Glob(string pattern, string? path = null)
{
    var searchPath = path ?? ".";

    // 构建排除模式
    var excludes = "--glob '!.git' --glob '!node_modules' " +
                   "--glob '!bin' --glob '!obj' --glob '!.vs'";

    // 优先使用 rg (ripgrep)，回退到 find
    var command = await HasCommand("rg")
        ? $"rg --files --glob '{Escape(pattern)}' {excludes} {searchPath} | head -100"
        : $"find {searchPath} -name '{Escape(pattern)}' " +
          $"! -path '*/.git/*' ! -path '*/node_modules/*' | head -100";

    var result = await RunCommand(command);
    if (string.IsNullOrEmpty(result))
        return "No files found";

    // 按修改时间排序 (最近修改的在前)
    var files = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    var sorted = files
        .Select(f => new { Path = f, MTime = GetMTime(f) })
        .OrderByDescending(f => f.MTime)
        .Select(f => f.Path)
        .ToArray();

    return string.Join('\n', sorted);
}
```

### ToolRegistry 更新

```csharp
// 将 "find" 改为 "glob"
if (enabled.Contains("glob"))
    tools.Add(AIFunctionFactory.Create(
        ([Description("Glob pattern (e.g. '**/*.cs', 'src/**/*.ts')")] string pattern,
         [Description("Directory to search (default: working directory)")] string? path) =>
            systemTools.Glob(pattern, path),
        "glob",
        "Find files matching a glob pattern. Results sorted by modification time (newest first). " +
        "Automatically excludes .git, node_modules, bin, obj directories."));
```

## 实现步骤

1. [ ] `SystemTools.cs` — 将 `Find` 改为 `Glob`，支持 glob 模式
2. [ ] 自动排除 .git, node_modules, bin, obj 等
3. [ ] 按修改时间排序
4. [ ] 结果限制 100 条
5. [ ] `ToolRegistry.cs` — 更新工具名和描述
6. [ ] 默认工具列表从 `find` 改为 `glob`
7. [ ] `SystemPromptBuilder.cs` — 更新工具描述
8. [ ] `ConfigOptions.cs` — 默认 enabled 列表更新

## 验收标准
- [ ] 支持 `**/*.cs` 语法
- [ ] 自动排除 .git 等目录
- [ ] 结果按修改时间排序
- [ ] 有 ripgrep 时使用 rg，否则回退到 find

## 参考
- opencode: `src/tool/glob.ts`

## 相关文件
- `src/tools/SystemTools.cs` — 改造
- `src/agent/ToolRegistry.cs` — 更新注册
- `src/agent/SystemPromptBuilder.cs` — 更新描述
- `src/config/ConfigOptions.cs` — 默认工具列表
