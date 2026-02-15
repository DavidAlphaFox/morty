using Microsoft.EntityFrameworkCore;
using Morty.Core.Entities;

namespace Morty.Infrastructure.Data;

public class MortyDbContext : DbContext
{
    public MortyDbContext(DbContextOptions<MortyDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Story> Stories => Set<Story>();
    public DbSet<Iteration> Iterations => Set<Iteration>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Verification> Verifications => Set<Verification>();
    public DbSet<StoryEvent> StoryEvents => Set<StoryEvent>();
    public DbSet<ExecutionOutput> ExecutionOutputs => Set<ExecutionOutput>();
    public DbSet<PhaseHistory> PhaseHistories => Set<PhaseHistory>();
    public DbSet<StoryDependency> StoryDependencies => Set<StoryDependency>();
    public DbSet<ClaudeEnvConfig> ClaudeEnvConfigs => Set<ClaudeEnvConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.WorkingDirectory).IsRequired().HasMaxLength(500);
            entity.Property(e => e.PrdJson).IsRequired();
        });

        modelBuilder.Entity<Story>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StoryId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Priority).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50);

            entity.HasOne(e => e.Project)
                .WithMany(p => p.Stories)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Iteration>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Story)
                .WithMany(s => s.Iterations)
                .HasForeignKey(e => e.StoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlanContent).IsRequired();
            entity.Property(e => e.Type).HasConversion<string>();

            entity.HasOne(e => e.Story)
                .WithMany(s => s.Plans)
                .HasForeignKey(e => e.StoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Verification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(100);

            entity.HasOne(e => e.Iteration)
                .WithMany(i => i.Verifications)
                .HasForeignKey(e => e.IterationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StoryEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);

            entity.HasOne(e => e.Story)
                .WithMany(s => s.Events)
                .HasForeignKey(e => e.StoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExecutionOutput>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Iteration)
                .WithMany(i => i.ExecutionOutputs)
                .HasForeignKey(e => e.IterationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PhaseHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Phase).HasConversion<string>();

            entity.HasOne(e => e.Story)
                .WithMany(s => s.PhaseHistories)
                .HasForeignKey(e => e.StoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StoryDependency>(entity =>
        {
            entity.HasKey(e => e.Id);

            // StoryId - 被阻塞的故事
            entity.HasOne(e => e.Story)
                .WithMany(s => s.Dependencies)
                .HasForeignKey(e => e.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // DependsOnStoryId - 依赖的故事
            entity.HasOne(e => e.DependsOnStory)
                .WithMany(s => s.Dependents)
                .HasForeignKey(e => e.DependsOnStoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClaudeEnvConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Value).HasMaxLength(500);
            entity.Property(e => e.DefaultValue).HasMaxLength(500);

            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Story>(entity =>
        {
            entity.Property(e => e.Phase).HasConversion<string>();
            entity.Property(e => e.Source).HasConversion<string>();
            entity.Property(e => e.Requirements).HasMaxLength(4000);
            entity.Property(e => e.DetailedPlan).HasMaxLength(8000);
            entity.Property(e => e.AcceptanceCriteria).HasMaxLength(4000);
        });
    }
}
