using MediaLibraryNormalizer.Audit;

namespace MediaLibraryNormalizer.Tests.Audit;

public sealed class NzbPlanetAvailabilityCheckerTests
{
    [Fact]
    public void SelectPreferred_PrefersH265AmongUntriedReleases()
    {
        IReadOnlyList<NzbSearchResult> results =
        [
            new("Show.S01E01.720p.x264", 700_000_000, DateTimeOffset.UtcNow, "https://example.invalid/1", "alpha"),
            new("Show.S01E01.1080p.x265", 900_000_000, DateTimeOffset.UtcNow, "https://example.invalid/2", "beta"),
            new("Show.S01E01.1080p.x264", 950_000_000, DateTimeOffset.UtcNow, "https://example.invalid/3", "gamma")
        ];

        var preferred = NzbPlanetAvailabilityChecker.SelectPreferred(results, ["id:alpha"]);

        Assert.NotNull(preferred);
        Assert.Equal("beta", preferred.NzbId);
    }

    [Fact]
    public void SelectPreferred_SkipsPreviouslyAttemptedRelease()
    {
        IReadOnlyList<NzbSearchResult> results =
        [
            new("Show.S01E01.1080p.x265", 900_000_000, DateTimeOffset.UtcNow, "https://example.invalid/2", "beta"),
            new("Show.S01E01.1080p.x264", 950_000_000, DateTimeOffset.UtcNow, "https://example.invalid/3", "gamma")
        ];

        var preferred = NzbPlanetAvailabilityChecker.SelectPreferred(results, ["id:beta"]);

        Assert.NotNull(preferred);
        Assert.Equal("gamma", preferred.NzbId);
    }

    [Fact]
    public void SelectPreferred_ReturnsNullWhenEveryResultWasAlreadyAttempted()
    {
        IReadOnlyList<NzbSearchResult> results =
        [
            new("Show.S01E01.1080p.x265", 900_000_000, DateTimeOffset.UtcNow, "https://example.invalid/2", "beta"),
            new("Show.S01E01.1080p.x264", 950_000_000, DateTimeOffset.UtcNow, "https://example.invalid/3", "gamma")
        ];

        var preferred = NzbPlanetAvailabilityChecker.SelectPreferred(results, ["id:beta", "id:gamma"]);

        Assert.Null(preferred);
    }
}