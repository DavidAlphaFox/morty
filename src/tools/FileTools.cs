// =============================================================================
// 文件操作工具
// =============================================================================
// 提供文件读取、写入、编辑功能
// 包含路径安全检查，确保操作在允许的工作目录内
// =============================================================================

using System.Diagnostics;

namespace Morty.Tools;

/// <summary>
/// 文件操作工具
/// </summary>
public class FileTools
{
    /// <summary>
    /// 工作目录
    /// </summary>
    private readonly string _workingDirectory;

    /// <summary>
    /// 初始化文件工具
    /// </summary>
    /// <param name="workingDirectory">工作目录</param>
    public FileTools(string workingDirectory)
    {
        _workingDirectory = Path.GetFullPath(workingDirectory);
    }

    /// <summary>
    /// 解析并验证文件路径
    /// </summary>
    /// <param name="path">相对或绝对路径</param>
    /// <returns>绝对路径</returns>
    /// <exception cref="UnauthorizedAccessException">路径不在工作目录内</exception>
    private string ResolvePath(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_workingDirectory, path));
        
        // 安全检查: 确保路径在工作目录内
        if (!fullPath.StartsWith(_workingDirectory))
            throw new UnauthorizedAccessException($"路径不在工作目录内: {path}");
        
        return fullPath;
    }

    /// <summary>
    /// 读取文件内容
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <returns>文件内容</returns>
    public Task<string> Read(string path)
    {
        var fullPath = ResolvePath(path);
        return File.ReadAllTextAsync(fullPath);
    }

    /// <summary>
    /// 写入文件内容
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="content">文件内容</param>
    /// <returns>操作结果</returns>
    public async Task<string> Write(string path, string content)
    {
        var fullPath = ResolvePath(path);
        
        // 确保目录存在
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(fullPath, content);
        return $"已写入文件: {path}";
    }

    /// <summary>
    /// 编辑文件内容 (替换)
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="oldString">需要替换的原文</param>
    /// <param name="newString">替换后的内容</param>
    /// <returns>操作结果</returns>
    /// <exception cref="InvalidOperationException">未找到需要替换的内容</exception>
    public async Task<string> Edit(string path, string oldString, string newString)
    {
        var content = await Read(path);

        if (!content.Contains(oldString))
            throw new InvalidOperationException("未找到需要替换的内容");

        content = content.Replace(oldString, newString);
        await Write(path, content);
        return $"已编辑文件: {path}";
    }
}
