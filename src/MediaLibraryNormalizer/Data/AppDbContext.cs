using MediaLibraryNormalizer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace MediaLibraryNormalizer.Data;

public class AppDbContext(string dbPath) : DbContext
{
    public DbSet<AuditRunEntity> AuditRuns => Set<AuditRunEntity>();

    public DbSet<CatalogCacheEntity> CatalogCache => Set<CatalogCacheEntity>();

    public DbSet<NzbDownloadAttemptEntity> NzbDownloadAttempts => Set<NzbDownloadAttemptEntity>();

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

        model.Entity<NzbDownloadAttemptEntity>(e =>
        {
            e.ToTable("NzbDownloadAttempts");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.LibraryPath, x.SeriesIdentityKey, x.EpisodeKey });
            e.HasIndex(x => new { x.LibraryPath, x.SeriesIdentityKey, x.EpisodeKey, x.ReleaseKey }).IsUnique();
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

        await Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS NzbDownloadAttempts (
                Id                INTEGER PRIMARY KEY AUTOINCREMENT,
                LibraryPath       TEXT NOT NULL,
                SeriesIdentityKey TEXT NOT NULL,
                EpisodeKey        TEXT NOT NULL,
                ReleaseKey        TEXT NOT NULL,
                ReleaseTitle      TEXT NOT NULL,
                NzbId             TEXT NULL,
                SabNzoId          TEXT NULL,
                DownloadUrl       TEXT NULL,
                AttemptedAt       TEXT NOT NULL
            );
            """, ct);

        await EnsureColumnExistsAsync("NzbDownloadAttempts", "SabNzoId", "TEXT NULL", ct);

        await Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS IX_NzbDownloadAttempts_LibraryPath_SeriesIdentityKey_EpisodeKey
            ON NzbDownloadAttempts (LibraryPath, SeriesIdentityKey, EpisodeKey);
            """, ct);

        await Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_NzbDownloadAttempts_UniqueRelease
            ON NzbDownloadAttempts (LibraryPath, SeriesIdentityKey, EpisodeKey, ReleaseKey);
            """, ct);
    }

    private async Task EnsureColumnExistsAsync(string tableName, string columnName, string columnDefinition, CancellationToken ct)
    {
        var connection = Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(ct);

        try
        {
            await using var pragmaCommand = connection.CreateCommand();
            pragmaCommand.CommandText = $"PRAGMA table_info({tableName});";

            var exists = false;
            await using (var reader = await pragmaCommand.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    if (string.Equals(reader[1]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
            }

            if (exists)
                return;

            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
            await alterCommand.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }
}
