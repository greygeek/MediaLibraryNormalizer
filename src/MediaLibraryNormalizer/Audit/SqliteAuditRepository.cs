using System.Text.Json;
using System.Text.Json.Serialization;
using MediaLibraryNormalizer.Data;
using MediaLibraryNormalizer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediaLibraryNormalizer.Audit;

/// <summary>
/// EF Core-backed store that implements both <see cref="IAuditRepository"/> and
/// <see cref="ICatalogCache"/>. Uses <see cref="AppDbContextFactory"/> for short-lived
/// unit-of-work contexts; schema initialisation is handled by the factory.
/// </summary>
public sealed class SqliteAuditRepository(AppDbContextFactory factory) : IAuditRepository, ICatalogCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    //  IAuditRepository 

    public async Task SaveRunAsync(SeriesAuditRunResult result, CancellationToken ct = default)
    {
        await using var db = factory.Create();
        db.AuditRuns.Add(new AuditRunEntity
        {
            RunDate = DateTime.UtcNow.ToString("O"),
            LibraryPath = result.LibraryPath,
            ResultJson = JsonSerializer.Serialize(result, JsonOptions)
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<(SeriesAuditRunResult Result, DateTime RunDate)?> LoadLatestRunAsync(
        string libraryPath, CancellationToken ct = default)
    {
        await using var db = factory.Create();
        var row = await db.AuditRuns
            .Where(r => r.LibraryPath == libraryPath)
            .OrderByDescending(r => r.RunDate)
            .FirstOrDefaultAsync(ct);

        if (row is null) return null;

        var runDate = DateTime.Parse(row.RunDate, null,
            System.Globalization.DateTimeStyles.RoundtripKind);
        var result = JsonSerializer.Deserialize<SeriesAuditRunResult>(row.ResultJson, JsonOptions);
        return result is null ? null : (result, runDate);
    }

    //  ICatalogCache 

    public async Task<CatalogSeries?> GetAsync(
        CatalogProviderKind provider, string normalizedTitle,
        int expiryHours, CancellationToken ct = default)
    {
        await using var db = factory.Create();
        var providerStr = provider.ToString();
        var key = normalizedTitle.ToLowerInvariant();

        var row = await db.CatalogCache
            .FirstOrDefaultAsync(c => c.Provider == providerStr && c.LookupKey == key, ct);

        if (row is null) return null;

        var cachedAt = DateTime.Parse(row.CachedAt, null,
            System.Globalization.DateTimeStyles.RoundtripKind);
        if (DateTime.UtcNow - cachedAt > TimeSpan.FromHours(expiryHours))
            return null;

        return JsonSerializer.Deserialize<CatalogSeries>(row.SeriesJson, JsonOptions);
    }

    public async Task SetAsync(
        CatalogProviderKind provider, string normalizedTitle,
        CatalogSeries series, CancellationToken ct = default)
    {
        await using var db = factory.Create();
        var providerStr = provider.ToString();
        var key = normalizedTitle.ToLowerInvariant();
        var json = JsonSerializer.Serialize(series, JsonOptions);

        var existing = await db.CatalogCache
            .FirstOrDefaultAsync(c => c.Provider == providerStr && c.LookupKey == key, ct);

        if (existing is null)
        {
            db.CatalogCache.Add(new CatalogCacheEntity
            {
                Provider = providerStr,
                LookupKey = key,
                CachedAt = DateTime.UtcNow.ToString("O"),
                SeriesJson = json
            });
        }
        else
        {
            existing.CachedAt = DateTime.UtcNow.ToString("O");
            existing.SeriesJson = json;
        }

        await db.SaveChangesAsync(ct);
    }
}
