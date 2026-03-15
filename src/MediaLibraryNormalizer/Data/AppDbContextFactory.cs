namespace MediaLibraryNormalizer.Data;

/// <summary>
/// Creates <see cref="AppDbContext"/> instances and ensures the database schema is initialised
/// exactly once per application lifetime.
/// </summary>
public sealed class AppDbContextFactory
{
    private readonly string _dbPath;
    private volatile bool _initialized;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);

    public AppDbContextFactory(string dbPath)
    {
        _dbPath = dbPath;
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    }

    /// <summary>Returns a new <see cref="AppDbContext"/> for a single unit of work.</summary>
    public AppDbContext Create() => new(_dbPath);

    /// <summary>
    /// Ensures the database schema exists. Safe to call concurrently; only runs once.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        await _initSemaphore.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            await using var db = Create();
            await db.EnsureSchemaAsync(ct);
            _initialized = true;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }
}
