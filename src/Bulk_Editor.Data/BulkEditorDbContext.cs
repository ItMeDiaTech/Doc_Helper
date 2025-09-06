using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Doc_Helper.Data.Entities;

namespace Doc_Helper.Data;

/// <summary>
/// Entity Framework Core DbContext for SQLite database operations
/// </summary>
public class DocHelperDbContext : DbContext
{
    public DocHelperDbContext(DbContextOptions<DocHelperDbContext> options) : base(options)
    {
    }

    // Entity sets
    public DbSet<DocumentEntity> Documents { get; set; } = null!;
    public DbSet<HyperlinkEntity> Hyperlinks { get; set; } = null!;
    public DbSet<ProcessingResultEntity> ProcessingResults { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Document entity configuration
        modelBuilder.Entity<DocumentEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FileName);
            entity.HasIndex(e => e.FilePath).IsUnique();
            entity.HasIndex(e => e.FileHash);
            entity.HasIndex(e => e.ProcessingStatus);
            entity.HasIndex(e => e.LastProcessedAt);
            entity.HasIndex(e => e.IsDeleted);

            // Configure relationships
            entity.HasMany(d => d.Hyperlinks)
                  .WithOne(h => h.Document)
                  .HasForeignKey(h => h.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(d => d.ProcessingResults)
                  .WithOne(pr => pr.Document)
                  .HasForeignKey(pr => pr.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Hyperlink entity configuration
        modelBuilder.Entity<HyperlinkEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Address);
            entity.HasIndex(e => e.ContentID);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ProcessingStatus);
            entity.HasIndex(e => e.ContentHash);
            entity.HasIndex(e => e.DocumentId);
            entity.HasIndex(e => e.IsDeleted);
            entity.HasIndex(e => new { e.DocumentId, e.ElementId });

            // Configure content hash for deduplication
            entity.HasIndex(e => e.ContentHash).IsUnique();
        });

        // ProcessingResult entity configuration
        modelBuilder.Entity<ProcessingResultEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.ProcessingType);
            entity.HasIndex(e => e.Success);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => e.DocumentId);
            entity.HasIndex(e => new { e.SessionId, e.ProcessingType });
        });

        // Global query filters for soft delete
        modelBuilder.Entity<DocumentEntity>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<HyperlinkEntity>().HasQueryFilter(e => !e.IsDeleted);

        // Seed data for development
        SeedData(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Default SQLite configuration if not configured externally
            var dbPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DocHelper",
                "DocHelper.db");

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        // Enable sensitive data logging in development
#if DEBUG
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.EnableDetailedErrors();
#endif
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateAuditFields();
        return base.SaveChanges();
    }

    /// <summary>
    /// Automatically update audit fields before saving changes
    /// </summary>
    private void UpdateAuditFields()
    {
        var entries = ChangeTracker.Entries();
        var timestamp = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Property("CreatedAt").CurrentValue == null)
                    entry.Property("CreatedAt").CurrentValue = timestamp;

                if (entry.Property("UpdatedAt").CurrentValue == null)
                    entry.Property("UpdatedAt").CurrentValue = timestamp;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property("UpdatedAt").CurrentValue = timestamp;

                // Prevent modification of CreatedAt
                entry.Property("CreatedAt").IsModified = false;
            }
        }
    }

    /// <summary>
    /// Seed initial data for development and testing
    /// </summary>
    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Add any seed data here if needed
        // For example, default processing statuses, document types, etc.
    }

    /// <summary>
    /// Soft delete an entity by setting IsDeleted flag
    /// </summary>
    public void SoftDelete<T>(T entity) where T : class
    {
        var entry = Entry(entity);

        if (entry.Property("IsDeleted").CurrentValue != null)
        {
            entry.Property("IsDeleted").CurrentValue = true;
            entry.Property("DeletedAt").CurrentValue = DateTime.UtcNow;
            entry.State = EntityState.Modified;
        }
    }

    /// <summary>
    /// Get all entities including soft deleted ones
    /// </summary>
    public IQueryable<T> GetAllIncludingDeleted<T>() where T : class
    {
        return Set<T>().IgnoreQueryFilters();
    }
}