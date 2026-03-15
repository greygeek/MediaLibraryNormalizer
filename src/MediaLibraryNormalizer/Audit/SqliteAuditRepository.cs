using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace MediaLibraryNormalizer.Audit;

/// <summary>
/// Single-file SQLite store that implements both audit-run persistence and catalog lookup caching.
/// The database is created on first use at the supplied <paramref name="dbPath"/>.
/// </summary>
public sealed class SqliteAuditRepository : IAuditRepository, ICatalogCache
{
    private readonly string _dbPath;
    private volatile bool _schemaEnsured;
    private readonly SemaphoreSlim _schemaSemaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public SqliteAuditRepository(string dbPath)
    {
        _dbPath = dbPath;
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    }

    // ── IAuditRepository ────────────────────────────────────────────────────

    public async Task SaveRunAsync(SeriesAuditRunResult result, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(result, JsonOptions);
        using var conn = await OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO AuditRuns (RunDate, LibraryPath, ResultJson)
            VALUES (@runDate, @libraryPath, @json);
            """;
        cmd.Parameters.AddWithValue("@runDate", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@libraryPath", result.LibraryPath);
        cmd.Parameters.AddWithValue("@json", json);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<(SeriesAuditRunResult Result, DateTime RunDate)?> LoadLatestRunAsync(
        string libraryPath, CancellationToken ct = default)
    {
        if (!File.Exists(_dbPath)) return null;

        using var conn = await OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT RunDate, ResultJson
            FROM   AuditRuns
            WHERE  LibraryPath = @libraryPath
            ORDER  BY RunDate DESC
            LIMIT  1;
            """;
        cmd.Parameters.AddWithValue("@libraryPath", libraryPath);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var runDate = DateTime.Parse(reader.GetString(0), null,
            System.Globalization.DateTimeStyles.RoundtripKind);
        var result = JsonSerializer.Deserialize<SeriesAuditRunResult>(reader.GetString(1), JsonOptions);
        return result is null ? null : (result, runDate);
    }

    // ── ICatalogCache ────────────────────────────────────────────────────────

    public async Task<CatalogSeries?> GetAsync(
        CatalogProviderKind provider, string normalizedTitle,
        int expiryHours, CancellationToken ct = default)
    {
        if (!File.Exists(_dbPath)) return null;

        using var conn = await OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT CachedAt, SeriesJson
            FROM   CatalogCache
            WHERE  Provider = @provider AND LookupKey = @key;
            """;
        cmd.Parameters.AddWithValue("@provider", provider.ToString());
        cmd.Parameters.AddWithValue("@key", normalizedTitle.ToLowerInvariant());

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var cachedAt = DateTime.Parse(reader.GetString(0), null,
            System.Globalization.DateTimeStyles.RoundtripKind);
        if (DateTime.UtcNow - cachedAt > TimeSpan.FromHours(expiryHours))
            return null;

        return JsonSerializer.Deserialize<CatalogSeries>(reader.GetString(1), JsonOptions);
    }

    public async Task SetAsync(
        CatalogProviderKind provider, string normalizedTitle,
        CatalogSeries series, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(series, JsonOptions);
        using var conn = await OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO CatalogCache (Provider, LookupKey, CachedAt, SeriesJson)
            VALUES (@provider, @key, @cachedAt, @json)
            ON CONFLICT(Provider, LookupKey) DO UPDATE SET
                CachedAt   = excluded.CachedAt,
                SeriesJson = excluded.SeriesJson;
            """;
        cmd.Parameters.AddWithValue("@provider", provider.ToString());
        cmd.Parameters.AddWithValue("@key", normalizedTitle.ToLowerInvariant());
        cmd.Parameters.AddWithValue("@cachedAt", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@json", json);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync(ct);
        if (!_schemaEnsured)
        {
            await _schemaSemaphore.WaitAsync(ct);
            try
            {
                if (!_schemaEnsured)
                {
                    await EnsureSchemaAsync(conn);
                    _schemaEnsured = true;
                }
            }
            finally
            {
                _schemaSemaphore.Release();
            }
        }
        return conn;
    }

    private static async Task EnsureSchemaAsync(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS AuditRuns (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                RunDate     TEXT    NOT NULL,
                LibraryPath TEXT    NOT NULL,
                ResultJson  TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_AuditRuns_LibraryPath
                ON AuditRuns (LibraryPath, RunDate DESC);
            CREATE TABLE IF NOT EXISTS CatalogCache (
                Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                Provider   TEXT    NOT NULL,
                LookupKey  TEXT    NOT NULL,
                CachedAt   TEXT    NOT NULL,
                SeriesJson TEXT    NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS UX_CatalogCache
                ON CatalogCache (Provider, LookupKey);
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
