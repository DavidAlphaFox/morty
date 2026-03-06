# 任务: 实现日志系统

## 阶段
Phase 1: 基础设施

## 描述
为 morty 项目实现完整的日志系统，包括 Serilog 配置、控制台输出和日志文件管理。

## 验收标准
- [ ] 日志可输出到控制台
- [ ] 日志可写入文件
- [ ] 支持不同日志级别
- [ ] 日志文件按日期滚动

## 实现步骤

### 3.1 创建日志配置类
创建 `src/cli/LoggingSetup.cs`:
```csharp
public static class LoggingSetup
{
    public static void Configure(string logDirectory)
    {
        var logPath = Path.Combine(logDirectory, "morty-.log");
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: 
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(logPath, 
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: 
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
```

### 3.2 创建日志目录
```csharp
var logDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "morty", "logs");
Directory.CreateDirectory(logDir);
LoggingSetup.Configure(logDir);
```

### 3.3 在 Program.cs 中初始化
```csharp
using Serilog;

public static void Main(string[] args)
{
    // 初始化日志
    LoggingSetup.Configure();
    
    Log.Information("morty starting...");
    
    try
    {
        // 运行应用
        App.Run(args);
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Application terminated unexpectedly");
    }
    finally
    {
        Log.CloseAndFlush();
    }
}
```

### 3.4 添加日志级别控制
支持通过命令行参数或环境变量控制日志级别:
- `--log-level DEBUG|INFO|WARN|ERROR`

## 日志文件位置
- Linux: `~/.local/share/morty/logs/morty-YYYYMMDD.log`
- 保留最近 7 天的日志

## 相关文件
- tasks/02-add-dependencies.md
- design/dotnet-coding-agent.md
