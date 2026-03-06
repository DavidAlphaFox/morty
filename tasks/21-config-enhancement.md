# 任务 4.3: 配置增强

## 阶段
Phase 4 — 体验优化

## 目标
多层级配置合并、`.morty/` 项目目录完整支持、环境变量展开。

## 设计方案

### 1. 多层级配置合并

```
优先级 (低 → 高):
1. 内置默认值
2. ~/.config/morty/morty.json (全局)
3. $MORTY_CONFIG (环境变量指定)
4. .morty/config.json (项目)
5. $MORTY_CONFIG_CONTENT (环境变量内容)
```

### 2. .morty/ 项目目录结构

```
.morty/
├── config.json          # 项目配置
├── agents/              # 自定义 Agent 定义
│   └── reviewer.json
├── skills/              # Skill 模板
│   └── refactor.md
├── plugins/             # 插件 DLL
│   └── my-plugin.dll
└── commands/            # 自定义命令
    └── deploy.md
```

### 3. ConfigLoader 改进

```csharp
// ConfigLoader.cs — 多层级合并
public MortyConfig Load()
{
    var config = new MortyConfig();
    MergeFrom(config, LoadGlobal());
    MergeFrom(config, LoadEnvConfig());
    MergeFrom(config, LoadProject());
    MergeFrom(config, LoadEnvContent());
    return config;
}

private void MergeFrom(MortyConfig target, MortyConfig? source)
{
    if (source == null) return;
    // 深度合并: 非 null 字段覆盖
}
```

### 4. 环境变量展开

```csharp
public static string ExpandPath(string path)
{
    path = path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    path = Environment.ExpandEnvironmentVariables(path);
    return path;
}
```

## 实现步骤

1. [ ] `ConfigLoader.cs` — 实现多层级配置加载和合并
2. [ ] 支持 JSONC (带注释的 JSON)
3. [ ] `.morty/` 目录自动创建 (morty init 命令)
4. [ ] 环境变量展开 ($HOME, ~)
5. [ ] `morty config show` 命令显示最终合并配置
6. [ ] `morty init` 命令创建 `.morty/` 目录结构

## 验收标准
- [ ] 全局 + 项目配置正确合并
- [ ] 环境变量展开工作
- [ ] `morty config show` 显示最终配置
- [ ] `morty init` 创建项目目录结构

## 相关文件
- `src/config/ConfigLoader.cs` — 改造
- `src/cli/Program.cs` — 新增命令
