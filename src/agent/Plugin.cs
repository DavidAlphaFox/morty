// =============================================================================
// 插件系统
// =============================================================================
// 支持通过 .morty/plugins/ 加载第三方工具和 Hook
// =============================================================================

using System.Reflection;
using Microsoft.Extensions.AI;

namespace Morty.Agent;

/// <summary>
/// 插件接口
/// </summary>
public interface IMortyPlugin
{
    string Name { get; }
    string Description { get; }

    /// <summary>返回此插件提供的工具</summary>
    IEnumerable<AIFunction> GetTools(string workingDirectory);

    /// <summary>Hook: 系统提示变换</summary>
    string TransformSystemPrompt(string prompt) => prompt;
}

/// <summary>
/// 插件加载器
/// </summary>
public class PluginLoader
{
    /// <summary>
    /// 从 .morty/plugins/ 加载 DLL 插件
    /// </summary>
    public static List<IMortyPlugin> LoadPlugins(string cwd)
    {
        var pluginDir = Path.Combine(cwd, ".morty", "plugins");
        if (!Directory.Exists(pluginDir)) return new();

        var plugins = new List<IMortyPlugin>();
        foreach (var dll in Directory.GetFiles(pluginDir, "*.dll"))
        {
            try
            {
                var assembly = Assembly.LoadFrom(dll);
                var types = assembly.GetTypes()
                    .Where(t => typeof(IMortyPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
                foreach (var type in types)
                {
                    if (Activator.CreateInstance(type) is IMortyPlugin plugin)
                        plugins.Add(plugin);
                }
            }
            catch
            {
                // 加载失败的插件跳过
            }
        }

        return plugins;
    }
}
