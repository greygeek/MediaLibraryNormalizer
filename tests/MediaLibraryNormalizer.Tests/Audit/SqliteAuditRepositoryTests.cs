using MediaLibraryNormalizer.Audit;
using MediaLibraryNormalizer.Data;

namespace MediaLibraryNormalizer.Tests.Audit;

public sealed class SqliteAuditRepositoryTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"SqliteAuditRepositoryTests_{Guid.NewGuid():N}");

    public SqliteAuditRepositoryTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task RecordAttemptAsync_PersistsReleaseKeysPerEpisode()
    {
        var factory = new AppDbContextFactory(Path.Combine(_tempRoot, "audit.db"));
        await factory.InitializeAsync();

        var sut = new SqliteAuditRepository(factory);
        var first = new NzbSearchResult("Show.S01E01.1080p.x265", 900_000_000, DateTimeOffset.UtcNow, "https://example.invalid/2", "beta");
        var second = new NzbSearchResult("Show.S01E01.1080p.x264", 950_000_000, DateTimeOffset.UtcNow, "https://example.invalid/3", "gamma");

        await sut.RecordAttemptAsync("D:/Library", "Show", 2025, "S01E01", first, "SABnzbd_nzo_1");
        await sut.RecordAttemptAsync("D:/Library", "Show", 2025, "S01E01", first);
        await sut.RecordAttemptAsync("D:/Library", "Show", 2025, "S01E01", second);
        await sut.RecordAttemptAsync("D:/Library", "Show", 2025, "S01E02", first);

        var episodeOne = await sut.GetAttemptsAsync("D:/Library", "Show", 2025, "S01E01");
        var episodeTwo = await sut.GetAttemptsAsync("D:/Library", "Show", 2025, "S01E02");

        Assert.Equal(2, episodeOne.Count);
        Assert.Contains(episodeOne, attempt => attempt.ReleaseKey == "id:beta");
        Assert.Contains(episodeOne, attempt => attempt.ReleaseKey == "id:gamma");
        Assert.Contains(episodeOne, attempt => attempt.ReleaseKey == "id:beta" && attempt.SabNzoId == "SABnzbd_nzo_1");
        Assert.Single(episodeTwo);
        Assert.Contains(episodeTwo, attempt => attempt.ReleaseKey == "id:beta");
    }

    [Fact]
    public async Task ClearAttemptsAsync_RemovesOnlyRequestedEpisodeHistory()
    {
        var factory = new AppDbContextFactory(Path.Combine(_tempRoot, "audit-clear.db"));
        await factory.InitializeAsync();

        var sut = new SqliteAuditRepository(factory);
        var result = new NzbSearchResult("Show.S01E01.1080p.x265", 900_000_000, DateTimeOffset.UtcNow, "https://example.invalid/2", "beta");

        await sut.RecordAttemptAsync("D:/Library", "Show", 2025, "S01E01", result);
        await sut.RecordAttemptAsync("D:/Library", "Show", 2025, "S01E02", result);

        await sut.ClearAttemptsAsync("D:/Library", "Show", 2025, "S01E01");

        Assert.Empty(await sut.GetAttemptsAsync("D:/Library", "Show", 2025, "S01E01"));
        Assert.Single(await sut.GetAttemptsAsync("D:/Library", "Show", 2025, "S01E02"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // best effort cleanup
        }
    }
}