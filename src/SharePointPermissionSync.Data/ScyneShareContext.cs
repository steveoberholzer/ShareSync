using Microsoft.EntityFrameworkCore;
using SharePointPermissionSync.Data.Entities;

namespace SharePointPermissionSync.Data;

/// <summary>
/// Entity Framework Core DbContext for ScyneShare database
/// </summary>
public class ScyneShareContext : DbContext
{
    public ScyneShareContext(DbContextOptions<ScyneShareContext> options)
        : base(options)
    {
    }

    // Existing tables
    public DbSet<Engagement> Engagements { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<Interaction> Interactions { get; set; }
    public DbSet<InteractionMembership> InteractionMemberships { get; set; }

    // New tables for queue system
    public DbSet<ProcessingJob> ProcessingJobs { get; set; }
    public DbSet<ProcessingJobItem> ProcessingJobItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure schema
        modelBuilder.HasDefaultSchema("ScyneShare");

        // Configure Engagement
        modelBuilder.Entity<Engagement>(entity =>
        {
            entity.ToTable("Engagement");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.SiteURL).HasMaxLength(255);
            entity.Property(e => e.CreatedBy).HasMaxLength(200);
            entity.Property(e => e.CreatedByFQN).HasMaxLength(200);
            entity.Property(e => e.ModifiedBy).HasMaxLength(200);
            entity.Property(e => e.ModifiedByFQN).HasMaxLength(200);
            entity.Property(e => e.ModifiedOnTimeStamp).HasMaxLength(30);
            entity.Property(e => e.SharePointFolderName).HasMaxLength(200);

            entity.HasMany(e => e.Projects)
                .WithOne(p => p.Engagement)
                .HasForeignKey(p => p.EngagementId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(e => e.Interactions)
                .WithOne(i => i.Engagement)
                .HasForeignKey(i => i.EngagementId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure Project
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("Project");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.CreatedBy).HasMaxLength(200);
            entity.Property(e => e.CreatedByFQN).HasMaxLength(200);
            entity.Property(e => e.ModifiedBy).HasMaxLength(200);
            entity.Property(e => e.ModifiedByFQN).HasMaxLength(200);
            entity.Property(e => e.ModifiedOnTimeStamp).HasMaxLength(30);
            entity.Property(e => e.SharePointFolderName).HasMaxLength(200);

            entity.HasMany(p => p.Interactions)
                .WithOne(i => i.Project)
                .HasForeignKey(i => i.ProjectId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure Interaction
        modelBuilder.Entity<Interaction>(entity =>
        {
            entity.ToTable("Interaction");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).HasMaxLength(200);
            entity.Property(e => e.CreatedByFQN).HasMaxLength(200);
            entity.Property(e => e.ModifiedBy).HasMaxLength(200);
            entity.Property(e => e.ModifiedByFQN).HasMaxLength(200);
            entity.Property(e => e.ModifiedOnTimeStamp).HasMaxLength(30);
            entity.Property(e => e.SharePointFolderName).HasMaxLength(500);
            entity.Property(e => e.StatusLastChangedBy).HasMaxLength(200);
            entity.Property(e => e.StatusLastChangedByFQN).HasMaxLength(200);

            entity.HasMany(i => i.InteractionMemberships)
                .WithOne(m => m.Interaction)
                .HasForeignKey(m => m.InteractionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure InteractionMembership
        modelBuilder.Entity<InteractionMembership>(entity =>
        {
            entity.ToTable("InteractionMembership");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedBy).HasMaxLength(200);
            entity.Property(e => e.CreatedByFQN).HasMaxLength(200);
            entity.Property(e => e.ModifiedBy).HasMaxLength(200);
            entity.Property(e => e.ModifiedByFQN).HasMaxLength(200);
        });

        // Configure ProcessingJob
        modelBuilder.Entity<ProcessingJob>(entity =>
        {
            entity.ToTable("ProcessingJobs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.JobId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.JobType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.UploadedBy).HasMaxLength(100);
            entity.Property(e => e.Environment).HasMaxLength(10);
            entity.Property(e => e.SiteUrl).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Queued");

            entity.HasMany(j => j.Items)
                .WithOne(i => i.Job)
                .HasForeignKey(i => i.JobId)
                .HasPrincipalKey(j => j.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure ProcessingJobItem
        modelBuilder.Entity<ProcessingJobItem>(entity =>
        {
            entity.ToTable("ProcessingJobItems");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.JobId);
            entity.HasIndex(e => e.MessageId).IsUnique();
            entity.HasIndex(e => e.Status);

            entity.Property(e => e.ItemType).HasMaxLength(50);
            entity.Property(e => e.ItemIdentifier).HasMaxLength(255);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Pending");
        });
    }
}
