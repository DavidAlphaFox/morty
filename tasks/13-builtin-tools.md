# 任务: 实现内置工具

## 阶段
Phase 5: 工具系统

## 描述
实现内置工具集：read、write、edit、bash、grep、find、ls，包含路径安全检查。

## 验收标准
- [ ] read/write/edit 文件操作正常
- [ ] bash/grep/find/ls 系统命令正常
- [ ] 路径安全检查 (工作目录内)
- [ ] 命令白名单支持

## 实现步骤

### 13.1 创建文件操作工具
创建 `src/tools/FileTools.cs`:
```csharp
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
    
    [AgentTool]
    [Description("读取文件内容")]
    public async Task<string> Read(
        [Description("文件路径")] string path)
    {
        var fullPath = ResolvePath(path);
        return await File.ReadAllTextAsync(fullPath);
    }
    
    [AgentTool]
    [Description("写入文件内容")]
    public async Task<string> Write(
        [Description("文件路径")] string path,
        [Description("文件内容")] string content)
    {
        var fullPath = ResolvePath(path);
        var dir = Path.GetDirectoryName(fullPath);
        
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        
        await File.WriteAllTextAsync(fullPath, content);
        return $"已写入文件: {path}";
    }
    
    [AgentTool]
    [Description("编辑文件内容")]
    public async Task<string> Edit(
        [Description("文件路径")] string path,
        [Description("需要替换的原文")] string oldString,
        [Description("替换后的内容")] string newString)
    {
        var content = await Read(path);
        
        if (!content.Contains(oldString))
            throw new InvalidOperationException("未找到需要替换的内容");
        
        content = content.Replace(oldString, newString);
        await Write(path, content);
        return $"已编辑文件: {path}";
    }
}
```

### 13.2 创建系统工具
创建 `src/tools/SystemTools.cs`:
```csharp
public class SystemTools
{
    private readonly string _workingDirectory;
    private readonly HashSet<string> _allowedCommands;
    private readonly int _timeout;
    
    public SystemTools(string workingDirectory, List<string>? allowedCommands = null, int timeout = 300)
    {
        _workingDirectory = Path.GetFullPath(workingDirectory);
        _allowedCommands = allowedCommands != null 
            ? new HashSet<string>(allowedCommands, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _timeout = timeout;
    }
    
    [AgentTool]
    [Description("执行 bash 命令")]
    public async Task<string> Bash([Description("要执行的 bash 命令")] string command)
    {
        // 检查命令白名单
        if (_allowedCommands.Count > 0)
        {
            var firstWord = command.Split(' ')[0];
            if (!_allowedCommands.Contains(firstWord))
                throw new UnauthorizedAccessException($"命令不在白名单中: {firstWord}");
        }
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeout));
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
            EnableRaisingEvents = true
        };
        
        process.Start();
        
        try
        {
            var output = await process.StandardOutput.ReadToEndAsync(cts.Token);
            var error = await process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);
            
            return string.IsNullOrEmpty(error) 
                ? output 
                : $"输出:\n{output}\n错误:\n{error}";
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            throw new TimeoutException($"命令执行超时: {command}");
        }
    }
    
    [AgentTool]
    [Description("搜索文件内容")]
    public async Task<string> Grep(
        [Description("正则表达式模式")] string pattern,
        [Description("搜索路径，默认当前目录")] string? path = null)
    {
        var searchPath = path ?? _workingDirectory;
        return await Bash($"grep -rn '{pattern.Replace("'", "\\'")}' {searchPath}");
    }
    
    [AgentTool]
    [Description("查找文件")]
    public async Task<string> Find(
        [Description("文件名模式，支持 * 和 ?")] string pattern,
        [Description("搜索路径，默认当前目录")] string? path = null)
    {
        var searchPath = path ?? _workingDirectory;
        return await Bash($"find {searchPath} -name '{pattern.Replace("'", "\\'")}'");
    }
    
    [AgentTool]
    [Description("列出目录内容")]
    public async Task<string> Ls(
        [Description("目录路径，默认当前目录")] string? path = null)
    {
        var targetPath = path ?? _workingDirectory;
        return await Bash($"ls -la {targetPath}");
    }
}
```

### 13.3 添加工具到 Agent
```csharp
// 在 CodingAgent 中注册工具
public void RegisterTools(FileTools fileTools, SystemTools systemTools)
{
    _tools.AddRange(fileTools.GetType()
        .GetMethods()
        .Where(m => m.GetCustomAttribute<AgentToolAttribute>() != null)
        .Select(m => AgentTool.FromMethod(m, fileTools)));
    
    _tools.AddRange(systemTools.GetType()
        .GetMethods()
        .Where(m => m.GetCustomAttribute<AgentToolAttribute>() != null)
        .Select(m => AgentTool.FromMethod(m, systemTools)));
}
```

## 相关文件
- tasks/10-coding-agent.md
- design/dotnet-coding-agent.md (4.1 内置工具)
