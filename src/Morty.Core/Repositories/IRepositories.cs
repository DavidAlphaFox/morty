using Morty.Core.Entities;

namespace Morty.Core.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<Project>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Project> AddAsync(Project project, CancellationToken cancellationToken = default);
    Task UpdateAsync(Project project, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public interface IStoryRepository
{
    Task<Story?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<Story>> GetByProjectIdAsync(int projectId, CancellationToken cancellationToken = default);
    Task<Story?> GetNextPendingAsync(CancellationToken cancellationToken = default);
    Task<Story> AddAsync(Story story, CancellationToken cancellationToken = default);
    Task UpdateAsync(Story story, CancellationToken cancellationToken = default);
    Task<int> GetIterationCountAsync(int storyId, CancellationToken cancellationToken = default);
    Task<List<Iteration>> GetIterationsByStoryIdAsync(int storyId, CancellationToken cancellationToken = default);
    Task<Plan?> GetPlanByStoryIdAsync(int storyId, CancellationToken cancellationToken = default);
}

public interface IIterationRepository
{
    Task<Iteration> AddAsync(Iteration iteration, CancellationToken cancellationToken = default);
    Task<List<Iteration>> GetByStoryIdAsync(int storyId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Iteration iteration, CancellationToken cancellationToken = default);
}

public interface IPlanRepository
{
    Task<Plan?> GetLatestByStoryIdAsync(int storyId, CancellationToken cancellationToken = default);
    Task<Plan> AddAsync(Plan plan, CancellationToken cancellationToken = default);
}

public interface IVerificationRepository
{
    Task<Verification> AddAsync(Verification verification, CancellationToken cancellationToken = default);
    Task<List<Verification>> GetByIterationIdAsync(int iterationId, CancellationToken cancellationToken = default);
}

public interface IStoryEventRepository
{
    Task<StoryEvent> AddAsync(StoryEvent storyEvent, CancellationToken cancellationToken = default);
    Task<List<StoryEvent>> GetByStoryIdAsync(int storyId, CancellationToken cancellationToken = default);
}
