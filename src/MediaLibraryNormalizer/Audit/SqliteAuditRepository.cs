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
public sealed class SqliteAuditRepository(AppDbContextFactory factory) : IAuditRepository, ICatalogCache, INzbDownloadHistory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    //  IAuditRepository 

    public async Task SaveRunAsync(SeriesAuditRunResult result, CancellationToken ct = default)
    {
        await using var db = factory.Create();

        // Replace any existing rows for this library path — only one saved run per path is needed.
        var stale = await db.AuditRuns
            .Where(r => r.LibraryPath == result.LibraryPath)
            .ToListAsync(ct);
        if (stale.Count > 0)
            db.AuditRuns.RemoveRange(stale);

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

    public async Task<IReadOnlyList<SeriesAuditRunResult>> GetAllRunsAsync(CancellationToken ct = default)
    {
        await using var db = factory.Create();
        var rows = await db.AuditRuns.ToListAsync(ct);
        var results = new List<SeriesAuditRunResult>();
        foreach (var row in rows)
        {
            var result = JsonSerializer.Deserialize<SeriesAuditRunResult>(row.ResultJson, JsonOptions);
            if (result is not null)
                results.Add(result);
        }
        return results;
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

    public async Task<IReadOnlyList<NzbDownloadAttempt>> GetAttemptsAsync(
        string libraryPath,
        string normalizedSeriesTitle,
        int? seriesYear,
        string episodeKey,
        CancellationToken ct = default)
    {
        await using var db = factory.Create();
        var seriesIdentityKey = NzbReleaseIdentity.GetSeriesIdentityKey(normalizedSeriesTitle, seriesYear);

        return await db.NzbDownloadAttempts
            .Where(a => a.LibraryPath == libraryPath
                && a.SeriesIdentityKey == seriesIdentityKey
                && a.EpisodeKey == episodeKey)
            .OrderByDescending(a => a.AttemptedAt)
            .Select(a => new NzbDownloadAttempt(
                a.ReleaseKey,
                a.ReleaseTitle,
                a.NzbId,
                a.SabNzoId,
                DateTimeOffset.Parse(a.AttemptedAt, null, System.Globalization.DateTimeStyles.RoundtripKind)))
            .ToListAsync(ct);
    }

    public async Task RecordAttemptAsync(
        string libraryPath,
        string normalizedSeriesTitle,
        int? seriesYear,
        string episodeKey,
        NzbSearchResult result,
        string? sabNzoId = null,
        CancellationToken ct = default)
    {
        await using var db = factory.Create();
        var seriesIdentityKey = NzbReleaseIdentity.GetSeriesIdentityKey(normalizedSeriesTitle, seriesYear);
        var releaseKey = NzbReleaseIdentity.GetReleaseKey(result);

        var exists = await db.NzbDownloadAttempts.AnyAsync(a =>
            a.LibraryPath == libraryPath
            && a.SeriesIdentityKey == seriesIdentityKey
            && a.EpisodeKey == episodeKey
            && a.ReleaseKey == releaseKey,
            ct);

        if (exists)
            return;

        db.NzbDownloadAttempts.Add(new NzbDownloadAttemptEntity
        {
            LibraryPath = libraryPath,
            SeriesIdentityKey = seriesIdentityKey,
            EpisodeKey = episodeKey,
            ReleaseKey = releaseKey,
            ReleaseTitle = result.Title,
            NzbId = string.IsNullOrWhiteSpace(result.NzbId) ? null : result.NzbId,
            SabNzoId = string.IsNullOrWhiteSpace(sabNzoId) ? null : sabNzoId,
            DownloadUrl = string.IsNullOrWhiteSpace(result.DownloadUrl) ? null : result.DownloadUrl,
            AttemptedAt = DateTime.UtcNow.ToString("O")
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task ClearAttemptsAsync(
        string libraryPath,
        string normalizedSeriesTitle,
        int? seriesYear,
        string episodeKey,
        CancellationToken ct = default)
    {
        await using var db = factory.Create();
        var seriesIdentityKey = NzbReleaseIdentity.GetSeriesIdentityKey(normalizedSeriesTitle, seriesYear);

        var rows = await db.NzbDownloadAttempts
            .Where(a => a.LibraryPath == libraryPath
                && a.SeriesIdentityKey == seriesIdentityKey
                && a.EpisodeKey == episodeKey)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return;

        db.NzbDownloadAttempts.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
    }
}
