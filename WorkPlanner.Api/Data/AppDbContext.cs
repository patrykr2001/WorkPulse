using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WorkPlanner.Api.Models;

namespace WorkPlanner.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<WorkEntry> WorkEntries => Set<WorkEntry>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Sprint> Sprints => Set<Sprint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.HasOne(e => e.Project)
                  .WithMany(p => p.Tasks)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Sprint)
                  .WithMany(s => s.Tasks)
                  .HasForeignKey(e => e.SprintId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Assignee)
                  .WithMany()
                  .HasForeignKey(e => e.AssigneeId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(e => e.WorkEntries)
                  .WithOne(we => we.TaskItem)
                  .HasForeignKey(we => we.TaskItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Sprint>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.Project)
                  .WithMany()
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.Owner)
                  .WithMany()
                  .HasForeignKey(e => e.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.HasKey(e => new { e.ProjectId, e.UserId });
            entity.HasOne(e => e.Project)
                  .WithMany(p => p.Members)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<WorkEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasOne(e => e.TaskItem)
                  .WithMany(t => t.WorkEntries)
                  .HasForeignKey(e => e.TaskItemId);
        });
    }
}
