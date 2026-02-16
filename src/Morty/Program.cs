using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Morty.Data;
using Morty.Entities;
using Morty.Hubs;
using Morty.Interfaces;
using Morty.Providers;
using Morty.Repositories;
using Morty.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Debug()
    .CreateLogger();

// CLI 模式检查
if (args.Length > 0 && (args[0] == "--cli" || args[0] != "--urls" && !args[0].StartsWith("--")))
{
    // 如果第一个参数不是 URL 参数，则作为 CLI 命令处理
    if (args[0].ToLower() is "server" or "project" or "story" or "help" or "--help" or "-h")
    {
        await CliHandler.HandleAsync(args);
        return;
    }
}

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// 添加服务到容器
builder.Services.AddDbContext<MortyDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// 注册仓储为 scoped 服务
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IStoryRepository, StoryRepository>();
builder.Services.AddScoped<IIterationRepository, IterationRepository>();
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IVerificationRepository, VerificationRepository>();
builder.Services.AddScoped<IStoryEventRepository, StoryEventRepository>();
builder.Services.AddScoped<IExecutionOutputRepository, ExecutionOutputRepository>();
builder.Services.AddScoped<IPhaseHistoryRepository, PhaseHistoryRepository>();
builder.Services.AddScoped<IStoryDependencyRepository, StoryDependencyRepository>();
builder.Services.AddScoped<IEnvConfigGroupRepository, EnvConfigGroupRepository>();
builder.Services.AddScoped<IEnvConfigRuleRepository, EnvConfigRuleRepository>();

// 注册核心服务
builder.Services.AddSingleton<IResponseAnalyzer, ResponseAnalyzer>();
builder.Services.AddSingleton<ICircuitBreaker, CircuitBreaker>();
builder.Services.AddSingleton<IRateLimiter, RateLimiter>();

// 注册进程执行器和 Claude CLI 提供者
builder.Services.AddSingleton<ProcessRunner>();
builder.Services.AddSingleton<IClaudeProvider, ClaudeCliProvider>();

// 注册从 MortyLoopService 提取的服务
builder.Services.AddSingleton<PhasePromptBuilder>();
builder.Services.AddSingleton<PhaseResultHandler>();
builder.Services.AddSingleton<StoryRecoveryService>();

// 注册 SignalR
builder.Services.AddSignalR();

// 注册后台服务（同时注册为单例，以便控制器注入通知）
builder.Services.AddSingleton<MortyLoopService>();
builder.Services.AddHostedService<MortyLoopService>(sp => sp.GetRequiredService<MortyLoopService>());

builder.Services.AddControllers();

// 配置 CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// 配置 HTTP 请求管道

// 从 wwwroot 提供静态文件
app.UseStaticFiles();

// 配置静态文件的 MIME 类型
var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".html"] = "text/html";
contentTypeProvider.Mappings[".js"] = "application/javascript";
contentTypeProvider.Mappings[".css"] = "text/css";
contentTypeProvider.Mappings[".json"] = "application/json";
contentTypeProvider.Mappings[".wasm"] = "application/wasm";
contentTypeProvider.Mappings[".woff"] = "font/woff";
contentTypeProvider.Mappings[".woff2"] = "font/woff2";

app.UseSerilogRequestLogging();
app.UseCors();

// API 端点优先
app.MapControllers();
app.MapHub<MortyHub>("/morty-hub");

// SPA 回退 - 为非 API 路由提供 index.html
app.MapFallbackToFile("index.html");

// 初始化 SignalR 广播器
var hubContext = app.Services.GetRequiredService<IHubContext<MortyHub>>();
MortyHub.Broadcaster.SetHubContext(hubContext);

// 确保数据库已迁移
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MortyDbContext>();
    db.Database.Migrate();
}

app.Run();

/// <summary>
/// CLI 命令处理器
/// </summary>
public static class CliHandler
{
    public static async Task HandleAsync(string[] args)
    {
        var command = args[0].ToLower();

        await Task.Run(() =>
        {
            switch (command)
            {
                case "server":
                    HandleServer(args);
                    break;
                case "project":
                    HandleProject(args);
                    break;
                case "story":
                    HandleStory(args);
                    break;
                case "--help":
                case "-h":
                case "help":
                    PrintHelp();
                    break;
                default:
                    PrintHelp();
                    break;
            }
        });
    }

    private static void HandleServer(string[] args)
    {
        var url = "http://localhost:5000";

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].StartsWith("--urls="))
                url = args[i][7..];
            else if (args[i] == "--urls" && i + 1 < args.Length)
                url = args[++i];
        }

        Console.WriteLine($"启动 Morty 服务器: {url}");
        Console.WriteLine("提示: 使用 'dotnet run --project Morty.Web' 启动服务器");
    }

    private static void HandleProject(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("项目命令:");
            Console.WriteLine("  project list                    - 列出所有项目");
            Console.WriteLine("  project create --name <名称>   - 创建项目");
            Console.WriteLine("  project get --id <ID>          - 获取项目详情");
            Console.WriteLine("  project delete --id <ID>        - 删除项目");
            return;
        }

        var subCommand = args[1].ToLower();

        switch (subCommand)
        {
            case "list":
                ListProjects();
                break;
            case "create":
                CreateProject(args);
                break;
            case "get":
                GetProject(args);
                break;
            case "delete":
                DeleteProject(args);
                break;
        }
    }

    private static void ListProjects()
    {
        Console.WriteLine("获取项目列表...");
        Console.WriteLine("提示: 使用 REST API");
        Console.WriteLine("  curl http://localhost:5000/api/projects");
    }

    private static void CreateProject(string[] args)
    {
        var name = "新项目";
        var workingDir = "./workspace";

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].StartsWith("--name="))
                name = args[i][7..];
            else if (args[i] == "--name" && i + 1 < args.Length)
                name = args[++i];
            else if (args[i].StartsWith("--working-dir="))
                workingDir = args[i][13..];
        }

        Console.WriteLine($"创建项目: {name}");
        Console.WriteLine($"工作目录: {workingDir}");
        Console.WriteLine("提示: 使用 REST API");
        Console.WriteLine($"  curl -X POST http://localhost:5000/api/projects -H 'Content-Type: application/json' -d '{{\"name\":\"{name}\",\"workingDirectory\":\"{workingDir}\"}}'");
    }

    private static void GetProject(string[] args)
    {
        var id = 1;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].StartsWith("--id="))
                int.TryParse(args[i][5..], out id);
            else if (args[i] == "--id" && i + 1 < args.Length)
                int.TryParse(args[++i], out id);
        }

        Console.WriteLine($"获取项目 ID: {id}");
        Console.WriteLine("提示: 使用 REST API");
        Console.WriteLine($"  curl http://localhost:5000/api/projects/{id}");
    }

    private static void DeleteProject(string[] args)
    {
        var id = 1;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].StartsWith("--id="))
                int.TryParse(args[i][5..], out id);
            else if (args[i] == "--id" && i + 1 < args.Length)
                int.TryParse(args[++i], out id);
        }

        Console.WriteLine($"删除项目 ID: {id}");
        Console.WriteLine("提示: 使用 REST API");
        Console.WriteLine($"  curl -X DELETE http://localhost:5000/api/projects/{id}");
    }

    private static void HandleStory(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("故事命令:");
            Console.WriteLine("  story list --project-id <ID>    - 列出项目中的故事");
            Console.WriteLine("  story create --project-id <ID> --title <标题> - 创建故事");
            return;
        }

        var subCommand = args[1].ToLower();

        switch (subCommand)
        {
            case "list":
                ListStories(args);
                break;
            case "create":
                CreateStory(args);
                break;
        }
    }

    private static void ListStories(string[] args)
    {
        var projectId = 1;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].StartsWith("--project-id="))
                int.TryParse(args[i][13..], out projectId);
        }

        Console.WriteLine($"获取项目 {projectId} 的故事列表...");
        Console.WriteLine("提示: 使用 REST API");
        Console.WriteLine($"  curl http://localhost:5000/api/projects/{projectId}/stories");
    }

    private static void CreateStory(string[] args)
    {
        var projectId = 1;
        var title = "新故事";
        var priority = "Medium";

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].StartsWith("--project-id="))
                int.TryParse(args[i][13..], out projectId);
            else if (args[i].StartsWith("--title="))
                title = args[i][8..];
            else if (args[i].StartsWith("--priority="))
                priority = args[i][11..];
        }

        Console.WriteLine($"在项目 {projectId} 中创建故事: {title}");
        Console.WriteLine($"优先级: {priority}");
        Console.WriteLine("提示: 使用 REST API");
        Console.WriteLine($"  curl -X POST http://localhost:5000/api/projects/{projectId}/stories -H 'Content-Type: application/json' -d '{{\"title\":\"{title}\",\"priority\":\"{priority}\"}}'");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Morty - AI 驱动的开发管理系统");
        Console.WriteLine();
        Console.WriteLine("用法:");
        Console.WriteLine("  morty [命令] [选项]");
        Console.WriteLine();
        Console.WriteLine("命令:");
        Console.WriteLine("  server                      - 启动 Web 服务器");
        Console.WriteLine("  project                     - 项目管理");
        Console.WriteLine("  story                       - 用户故事管理");
        Console.WriteLine();
        Console.WriteLine("示例:");
        Console.WriteLine("  dotnet run --project Morty.Web -- server --urls=http://localhost:5000");
        Console.WriteLine("  dotnet run --project Morty.Web -- project list");
        Console.WriteLine("  dotnet run --project Morty.Web -- project create --name=\"我的项目\" --working-dir=\"./workspace\"");
        Console.WriteLine("  dotnet run --project Morty.Web -- story create --project-id=1 --title=\"实现登录功能\"");
        Console.WriteLine();
        Console.WriteLine("更多信息请访问: https://github.com/morty");
    }
}
