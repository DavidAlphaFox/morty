using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Morty.Core.Entities;
using Morty.Core.Interfaces;
using Morty.Core.Repositories;
using Morty.Core.Services;
using Morty.Infrastructure.Data;
using Morty.Infrastructure.Repositories;
using Morty.Web.Hubs;
using Morty.Web.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Debug()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddDbContext<MortyDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register repositories as scoped
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IStoryRepository, StoryRepository>();
builder.Services.AddScoped<IIterationRepository, IterationRepository>();
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IVerificationRepository, VerificationRepository>();
builder.Services.AddScoped<IStoryEventRepository, StoryEventRepository>();

// Register core services
builder.Services.AddSingleton<IClaudeClient, ClaudeClient>();
builder.Services.AddSingleton<IResponseAnalyzer, ResponseAnalyzer>();
builder.Services.AddSingleton<ICircuitBreaker, CircuitBreaker>();
builder.Services.AddSingleton<IRateLimiter, RateLimiter>();

// Register SignalR
builder.Services.AddSignalR();

// Register background service with scope factory
builder.Services.AddHostedService<MortyLoopService>();

builder.Services.AddControllers();

// Configure CORS
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

// Configure the HTTP request pipeline.

// Serve static files from wwwroot
app.UseStaticFiles();

// Configure static file options for proper MIME types
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

// API endpoints first
app.MapControllers();
app.MapHub<MortyHub>("/morty-hub");

// SPA fallback - serve index.html for non-API routes
app.MapFallbackToFile("index.html");

// Initialize SignalR broadcaster
var hubContext = app.Services.GetRequiredService<IHubContext<MortyHub>>();
MortyHub.Broadcaster.SetHubContext(hubContext);

// Ensure database is migrated
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MortyDbContext>();
    db.Database.Migrate();
}

app.Run();
