using MediaLibraryNormalizer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediaLibraryNormalizer.Data;

public class AppDbContext(string dbPath) : DbContext
{
    public DbSet<AuditRunEntity> AuditRuns => Set<AuditRunEntity>();

    public DbSet<CatalogCacheEntity> CatalogCache => Set<CatalogCacheEntity>();

    public DbSet<SettingEntity> Settings => Set<SettingEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={dbPath}");

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AuditRunEntity>(e =>
        {
            e.ToTable("AuditRuns");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.LibraryPath, x.RunDate });
        });

        model.Entity<CatalogCacheEntity>(e =>
        {
            e.ToTable("CatalogCache");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Provider, x.LookupKey }).IsUnique();
        });

        model.Entity<SettingEntity>(e =>
        {
            e.ToTable("Settings");
            e.HasKey(x => x.Key);
        });
    }

    /// <summary>
    /// Ensures the schema is up to date.
    /// <list type="bullet">
    ///   <item><see cref="Database.EnsureCreatedAsync"/> creates the full schema on a fresh database.</item>
    ///   <item>The explicit <c>CREATE TABLE IF NOT EXISTS Settings</c> handles existing databases
    ///         that were created before the Settings table was introduced.</item>
    /// </list>
    /// </summary>
    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        await Database.EnsureCreatedAsync(ct);

        await Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS Settings (
                Key   TEXT PRIMARY KEY NOT NULL,
                Value TEXT NOT NULL
            );
            """, ct);
    }
}
