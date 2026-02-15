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
    public DbSet<EnvConfigGroup> EnvConfigGroups => Set<EnvConfigGroup>();
    public DbSet<EnvVariable> EnvVariables => Set<EnvVariable>();
    public DbSet<EnvConfigRule> EnvConfigRules => Set<EnvConfigRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.WorkingDirectory).IsRequired().HasMaxLength(500);
            entity.Property(e => e.PrdJson).IsRequired();

            entity.HasOne(e => e.DefaultEnvConfigGroup)
                .WithMany()
                .HasForeignKey(e => e.DefaultEnvConfigGroupId)
                .OnDelete(DeleteBehavior.SetNull);
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
            entity.Property(e => e.Output).IsRequired();
            entity.Property(e => e.Type).HasConversion<string>();

            // 创建索引：StoryId + Type + Version 组合唯一
            entity.HasIndex(e => new { e.StoryId, e.Type, e.Version }).IsUnique();

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

        modelBuilder.Entity<EnvConfigGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<EnvVariable>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Value).HasMaxLength(1000);
            entity.Property(e => e.DefaultValue).HasMaxLength(1000);

            entity.HasOne(e => e.EnvConfigGroup)
                .WithMany(g => g.Variables)
                .HasForeignKey(e => e.EnvConfigGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EnvConfigRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FromPhase).HasConversion<string>();
            entity.Property(e => e.ToPhase).HasConversion<string>();
            entity.Property(e => e.Tags).IsRequired().HasMaxLength(500);

            entity.HasOne(e => e.Project)
                .WithMany(p => p.EnvConfigRules)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.EnvConfigGroup)
                .WithMany()
                .HasForeignKey(e => e.EnvConfigGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Story>(entity =>
        {
            entity.Property(e => e.Phase).HasConversion<string>();
            entity.Property(e => e.Source).HasConversion<string>();
            entity.Property(e => e.Requirements).HasMaxLength(4000);
            entity.Property(e => e.UserAcceptanceCriteria).HasMaxLength(4000);
            entity.Property(e => e.Tags).IsRequired().HasMaxLength(1000);
        });
    }
}
