using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Morty.Core.Entities;
using Morty.Core.Interfaces;
using Morty.Core.Repositories;
using Morty.Core.Services;
using Morty.Web.DTOs;
using Morty.Web.Hubs;
using Serilog;

namespace Morty.Web.Services;

public class MortyLoopService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClaudeClient _claudeClient;
    private readonly IResponseAnalyzer _responseAnalyzer;
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly IRateLimiter _rateLimiter;
    private readonly Serilog.ILogger _logger;
    private readonly TimeSpan _delayBetweenIterations;
    private readonly int _maxIterationsPerStory;

    private bool _isRunning;

    public MortyLoopService(
        IServiceScopeFactory scopeFactory,
        IClaudeClient claudeClient,
        IResponseAnalyzer responseAnalyzer,
        ICircuitBreaker circuitBreaker,
        IRateLimiter rateLimiter,
        Serilog.ILogger? logger = null,
        int maxIterationsPerStory = 10,
        int delaySeconds = 5)
    {
        _scopeFactory = scopeFactory;
        _claudeClient = claudeClient;
        _responseAnalyzer = responseAnalyzer;
        _circuitBreaker = circuitBreaker;
        _rateLimiter = rateLimiter;
        _logger = logger ?? Log.Logger;
        _maxIterationsPerStory = maxIterationsPerStory;
        _delayBetweenIterations = TimeSpan.FromSeconds(delaySeconds);
    }

    public bool IsRunning => _isRunning;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _isRunning = true;
        _logger.Information("MortyLoopService starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_circuitBreaker.CanExecute())
                {
                    _logger.Warning("Circuit breaker is open, waiting...");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                await ProcessNextStoryAsync(stoppingToken);

                await Task.Delay(_delayBetweenIterations, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.Information("MortyLoopService stopping...");
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in MortyLoopService");
                _circuitBreaker.RecordFailure();
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _isRunning = false;
    }

    private async Task ProcessNextStoryAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var storyRepo = scope.ServiceProvider.GetRequiredService<IStoryRepository>();
        var projectRepo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var iterationRepo = scope.ServiceProvider.GetRequiredService<IIterationRepository>();
        var planRepo = scope.ServiceProvider.GetRequiredService<IPlanRepository>();
        var verificationRepo = scope.ServiceProvider.GetRequiredService<IVerificationRepository>();
        var storyEventRepo = scope.ServiceProvider.GetRequiredService<IStoryEventRepository>();

        // Get next story to process
        var story = await storyRepo.GetNextPendingAsync(stoppingToken);

        if (story == null)
        {
            _logger.Debug("No stories to process");
            return;
        }

        _logger.Information("Processing story {StoryId}: {Title}", story.StoryId, story.Title);

        // Update status
        story.Status = "InProgress";
        await storyRepo.UpdateAsync(story, stoppingToken);

        // Get project PRD
        var project = await projectRepo.GetByIdAsync(story.ProjectId, stoppingToken);
        if (project == null)
        {
            _logger.Error("Project not found for story {StoryId}", story.StoryId);
            story.Status = "Failed";
            await storyRepo.UpdateAsync(story, stoppingToken);
            return;
        }

        // Ensure working directory exists
        if (!string.IsNullOrEmpty(project.WorkingDirectory) && !Directory.Exists(project.WorkingDirectory))
        {
            _logger.Information("Creating working directory: {WorkingDirectory}", project.WorkingDirectory);
            Directory.CreateDirectory(project.WorkingDirectory);
        }

        // Check iteration count
        var iterationCount = await storyRepo.GetIterationCountAsync(story.Id, stoppingToken);

        if (iterationCount >= _maxIterationsPerStory)
        {
            _logger.Warning("Story {StoryId} reached max iterations", story.StoryId);
            story.Status = "Failed";
            await storyRepo.UpdateAsync(story, stoppingToken);
            return;
        }

        // Wait for rate limiter
        await _rateLimiter.WaitForAvailabilityAsync(stoppingToken);
        _rateLimiter.RecordRequest();

        // Create iteration
        var iteration = new Iteration
        {
            StoryId = story.Id,
            IterationNum = iterationCount + 1,
            StartedAt = DateTime.UtcNow
        };
        await iterationRepo.AddAsync(iteration, stoppingToken);

        try
        {
            // Build prompt
            var prompt = BuildPrompt(project.PrdJson, story, iterationCount);

            // Call Claude with project's working directory
            var workingDir = project.WorkingDirectory;
            _logger.Debug("Using working directory: {WorkingDirectory}", workingDir);
            var response = await _claudeClient.SendMessageWithContextAsync(
                prompt,
                workingDir,
                stoppingToken);

            iteration.CompletedAt = DateTime.UtcNow;
            iteration.DurationMs = (long)(iteration.CompletedAt.Value - iteration.StartedAt).TotalMilliseconds;
            iteration.Output = response.Content;
            await iterationRepo.UpdateAsync(iteration, stoppingToken);

            // Broadcast iteration completion via SignalR
            var iterationDto = new IterationDto
            {
                Id = iteration.Id,
                StoryId = iteration.StoryId,
                IterationNum = iteration.IterationNum,
                StartedAt = iteration.StartedAt,
                CompletedAt = iteration.CompletedAt,
                DurationMs = iteration.DurationMs,
                Output = iteration.Output
            };
            await MortyHub.Broadcaster.NotifyIterationComplete(iterationDto);

            // Analyze response
            var analysis = _responseAnalyzer.Analyze(response.Content);

            // Record verification
            var verification = new Verification
            {
                IterationId = iteration.Id,
                Type = "build",
                Passed = !analysis.HasErrors,
                Output = response.Error ?? response.Content,
                CreatedAt = DateTime.UtcNow
            };
            await verificationRepo.AddAsync(verification, stoppingToken);

            // Extract and save plan if this is first iteration
            if (iterationCount == 0)
            {
                var planResult = _responseAnalyzer.ExtractPlan(response.Content);
                if (planResult != null)
                {
                    var plan = new Plan
                    {
                        StoryId = story.Id,
                        PlanContent = planResult.Plan,
                        CreatedAt = DateTime.UtcNow
                    };
                    await planRepo.AddAsync(plan, stoppingToken);
                }
            }

            // Update story status based on analysis
            if (analysis.IsComplete)
            {
                story.Status = "Completed";
                story.CompletedAt = DateTime.UtcNow;
                _circuitBreaker.RecordSuccess();
                _logger.Information("Story {StoryId} completed successfully", story.StoryId);
            }
            else
            {
                story.Status = "InProgress";
                _circuitBreaker.RecordFailure();
                _logger.Information("Story {StoryId} iteration {Iteration} completed, not yet done",
                    story.StoryId, iteration.IterationNum);
            }

            // Record event
            var storyEvent = new StoryEvent
            {
                StoryId = story.Id,
                EventType = "iteration_complete",
                Timestamp = DateTime.UtcNow,
                DataJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    iteration = iteration.IterationNum,
                    complete = analysis.IsComplete,
                    errors = analysis.HasErrors
                })
            };
            await storyEventRepo.AddAsync(storyEvent, stoppingToken);

            await storyRepo.UpdateAsync(story, stoppingToken);

            // Broadcast story update via SignalR
            var storyDto = new StoryDto
            {
                Id = story.Id,
                ProjectId = story.ProjectId,
                StoryId = story.StoryId,
                Title = story.Title,
                Priority = story.Priority,
                Status = story.Status,
                CreatedAt = story.CreatedAt,
                CompletedAt = story.CompletedAt
            };
            await MortyHub.Broadcaster.NotifyStoryUpdated(storyDto);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing story {StoryId}", story.StoryId);
            iteration.Output = ex.Message;
            await iterationRepo.UpdateAsync(iteration, stoppingToken);
            story.Status = "Failed";
            _circuitBreaker.RecordFailure();
            await storyRepo.UpdateAsync(story, stoppingToken);
        }
    }

    private static string BuildPrompt(string prdJson, Story story, int iterationCount)
    {
        var iterationContext = iterationCount == 0
            ? "This is the first iteration. Please analyze the requirements and create a plan."
            : $"This is iteration {iterationCount + 1}. Please continue implementing.";

        return $"""
            You are working on implementing a user story.

            User Story: {story.Title}
            Story ID: {story.StoryId}

            {iterationContext}

            Requirements (PRD):
            {prdJson}

            Please implement the changes and respond with:
            1. What you did
            2. Any files you modified
            3. Whether implementation is complete or what remains
            """;
    }
}
