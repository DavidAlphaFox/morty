# 任务 3.2: 插件系统

## 阶段
Phase 3 — 高级特性

## 目标
支持通过 `.morty/plugins/` 加载第三方工具和 Hook。

## 设计方案

### 1. 插件接口

```csharp
// src/agent/IMortyPlugin.cs

public interface IMortyPlugin
{
    string Name { get; }
    string Description { get; }

    /// <summary>返回此插件提供的工具</summary>
    IEnumerable<AIFunction> GetTools(string workingDirectory);

    /// <summary>Hook: 工具执行前</summary>
    Task OnToolExecuteBefore(string toolName, IDictionary<string, object?> args) => Task.CompletedTask;

    /// <summary>Hook: 工具执行后</summary>
    Task OnToolExecuteAfter(string toolName, object? result) => Task.CompletedTask;

    /// <summary>Hook: 系统提示变换</summary>
    string TransformSystemPrompt(string prompt) => prompt;
}
```

### 2. 插件加载器

```csharp
// src/agent/PluginLoader.cs

public class PluginLoader
{
    /// <summary>从 .morty/plugins/ 加载 DLL 插件</summary>
    public List<IMortyPlugin> LoadPlugins(string cwd)
    {
        var pluginDir = Path.Combine(cwd, ".morty", "plugins");
        if (!Directory.Exists(pluginDir)) return new();

        var plugins = new List<IMortyPlugin>();
        foreach (var dll in Directory.GetFiles(pluginDir, "*.dll"))
        {
            var assembly = Assembly.LoadFrom(dll);
            var types = assembly.GetTypes()
                .Where(t => typeof(IMortyPlugin).IsAssignableFrom(t) && !t.IsAbstract);
            foreach (var type in types)
                plugins.Add((IMortyPlugin)Activator.CreateInstance(type)!);
        }
        return plugins;
    }
}
```

### 3. 集成到 Agent

```csharp
// CodingAgent — 注册插件工具
foreach (var plugin in plugins)
    foreach (var tool in plugin.GetTools(cwd))
        agent.RegisterTool(tool);

// 工具执行时调用 Hook
foreach (var plugin in plugins)
    await plugin.OnToolExecuteBefore(fc.Name, fc.Arguments);
```

## 实现步骤

1. [ ] 创建 `src/agent/IMortyPlugin.cs` — 插件接口
2. [ ] 创建 `src/agent/PluginLoader.cs` — DLL 加载
3. [ ] `CodingAgent.cs` — 集成插件工具和 Hook
4. [ ] `SystemPromptBuilder.cs` — 支持插件的 prompt 变换
5. [ ] 文档: 插件开发指南

## 验收标准
- [ ] 可从 `.morty/plugins/` 加载 DLL 插件
- [ ] 插件提供的工具可被 Agent 使用
- [ ] Hook 系统正常工作

## 参考
- opencode: `packages/plugin/`, `src/plugin/index.ts`

## 相关文件
- `src/agent/IMortyPlugin.cs` — 新建
- `src/agent/PluginLoader.cs` — 新建
- `src/agent/CodingAgent.cs` — 集成
