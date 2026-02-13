using Microsoft.EntityFrameworkCore;
using Morty.Core.Entities;
using Morty.Core.Repositories;
using Morty.Infrastructure.Data;

namespace Morty.Infrastructure.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly MortyDbContext _context;

    public ProjectRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<Project?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Projects.FindAsync([id], cancellationToken);
    }

    public async Task<List<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Projects.ToListAsync(cancellationToken);
    }

    public async Task<Project> AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        _context.Projects.Add(project);
        await _context.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        _context.Projects.Update(project);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects.FindAsync([id], cancellationToken);
        if (project != null)
        {
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public class StoryRepository : IStoryRepository
{
    private readonly MortyDbContext _context;

    public StoryRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<Story?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Stories.FindAsync([id], cancellationToken);
    }

    public async Task<List<Story>> GetByProjectIdAsync(int projectId, CancellationToken cancellationToken = default)
    {
        return await _context.Stories
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.Priority == "High" ? 0 : s.Priority == "Medium" ? 1 : 2)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Story?> GetNextPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Stories
            .Where(s => s.Status == "Pending" || s.Status == "InProgress")
            .OrderBy(s => s.Priority == "High" ? 0 : s.Priority == "Medium" ? 1 : 2)
            .ThenBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Story> AddAsync(Story story, CancellationToken cancellationToken = default)
    {
        _context.Stories.Add(story);
        await _context.SaveChangesAsync(cancellationToken);
        return story;
    }

    public async Task UpdateAsync(Story story, CancellationToken cancellationToken = default)
    {
        _context.Stories.Update(story);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetIterationCountAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.Iterations.CountAsync(i => i.StoryId == storyId, cancellationToken);
    }

    public async Task<List<Iteration>> GetIterationsByStoryIdAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.Iterations
            .Where(i => i.StoryId == storyId)
            .OrderBy(i => i.IterationNum)
            .ToListAsync(cancellationToken);
    }

    public async Task<Plan?> GetPlanByStoryIdAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.Plans
            .Where(p => p.StoryId == storyId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public class IterationRepository : IIterationRepository
{
    private readonly MortyDbContext _context;

    public IterationRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<Iteration> AddAsync(Iteration iteration, CancellationToken cancellationToken = default)
    {
        _context.Iterations.Add(iteration);
        await _context.SaveChangesAsync(cancellationToken);
        return iteration;
    }

    public async Task<List<Iteration>> GetByStoryIdAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.Iterations
            .Where(i => i.StoryId == storyId)
            .OrderBy(i => i.IterationNum)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(Iteration iteration, CancellationToken cancellationToken = default)
    {
        _context.Iterations.Update(iteration);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class PlanRepository : IPlanRepository
{
    private readonly MortyDbContext _context;

    public PlanRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<Plan?> GetLatestByStoryIdAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.Plans
            .Where(p => p.StoryId == storyId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Plan> AddAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync(cancellationToken);
        return plan;
    }
}

public class VerificationRepository : IVerificationRepository
{
    private readonly MortyDbContext _context;

    public VerificationRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<Verification> AddAsync(Verification verification, CancellationToken cancellationToken = default)
    {
        _context.Verifications.Add(verification);
        await _context.SaveChangesAsync(cancellationToken);
        return verification;
    }

    public async Task<List<Verification>> GetByIterationIdAsync(int iterationId, CancellationToken cancellationToken = default)
    {
        return await _context.Verifications
            .Where(v => v.IterationId == iterationId)
            .ToListAsync(cancellationToken);
    }
}

public class StoryEventRepository : IStoryEventRepository
{
    private readonly MortyDbContext _context;

    public StoryEventRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<StoryEvent> AddAsync(StoryEvent storyEvent, CancellationToken cancellationToken = default)
    {
        _context.StoryEvents.Add(storyEvent);
        await _context.SaveChangesAsync(cancellationToken);
        return storyEvent;
    }

    public async Task<List<StoryEvent>> GetByStoryIdAsync(int storyId, CancellationToken cancellationToken = default)
    {
        return await _context.StoryEvents
            .Where(e => e.StoryId == storyId)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }
}

public class ProviderRepository : IProviderRepository
{
    private readonly MortyDbContext _context;

    public ProviderRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<Provider?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Providers.FindAsync([id], cancellationToken);
    }

    public async Task<List<Provider>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Providers.ToListAsync(cancellationToken);
    }

    public async Task<Provider?> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Providers.FirstOrDefaultAsync(p => p.IsDefault, cancellationToken);
    }

    public async Task<Provider> AddAsync(Provider provider, CancellationToken cancellationToken = default)
    {
        _context.Providers.Add(provider);
        await _context.SaveChangesAsync(cancellationToken);
        return provider;
    }

    public async Task UpdateAsync(Provider provider, CancellationToken cancellationToken = default)
    {
        _context.Providers.Update(provider);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var provider = await _context.Providers.FindAsync([id], cancellationToken);
        if (provider != null)
        {
            _context.Providers.Remove(provider);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public class ExecutionOutputRepository : IExecutionOutputRepository
{
    private readonly MortyDbContext _context;

    public ExecutionOutputRepository(MortyDbContext context)
    {
        _context = context;
    }

    public async Task<ExecutionOutput> AddAsync(ExecutionOutput output, CancellationToken cancellationToken = default)
    {
        _context.ExecutionOutputs.Add(output);
        await _context.SaveChangesAsync(cancellationToken);
        return output;
    }

    public async Task<List<ExecutionOutput>> GetByIterationIdAsync(int iterationId, CancellationToken cancellationToken = default)
    {
        return await _context.ExecutionOutputs
            .Where(e => e.IterationId == iterationId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
