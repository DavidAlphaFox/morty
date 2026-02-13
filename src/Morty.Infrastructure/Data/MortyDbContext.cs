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
    }
}
