using System.Diagnostics;

namespace Morty.Tools;

public class FileTools
{
    private readonly string _workingDirectory;

    public FileTools(string workingDirectory)
    {
        _workingDirectory = Path.GetFullPath(workingDirectory);
    }

    private string ResolvePath(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_workingDirectory, path));
        if (!fullPath.StartsWith(_workingDirectory))
            throw new UnauthorizedAccessException($"路径不在工作目录内: {path}");
        return fullPath;
    }

    public Task<string> Read(string path)
    {
        var fullPath = ResolvePath(path);
        return File.ReadAllTextAsync(fullPath);
    }

    public async Task<string> Write(string path, string content)
    {
        var fullPath = ResolvePath(path);
        var dir = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(fullPath, content);
        return $"已写入文件: {path}";
    }

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
