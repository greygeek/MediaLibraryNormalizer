using MediaLibraryNormalizer.Audit;

namespace MediaLibraryNormalizer.Tests.Audit;

public sealed class SabnzbdHistoryMatcherTests
{
    [Fact]
    public void BuildFailedReleaseKeysByEpisode_UsesDuplicateKeyWhenPresent()
    {
        IReadOnlyList<SabnzbdHistoryItem> items =
        [
            new(
                "Show.S01E01.1080p.x265",
                "Show.S01E01.1080p.x265.nzb",
                "Failed",
                "missing articles",
                "TV",
                "Show/1/1",
                "SABnzbd_nzo_1")
        ];

        var lookup = SabnzbdHistoryMatcher.BuildFailedReleaseKeysByEpisode("Show", ["S01E01"], items);

        Assert.True(lookup.ContainsKey("S01E01"));
        Assert.Contains("title:show.s01e01.1080p.x265", lookup["S01E01"]);
    }

    [Fact]
    public void BuildFailedReleaseKeysByEpisode_FallsBackToParsingReleaseName()
    {
        IReadOnlyList<SabnzbdHistoryItem> items =
        [
            new(
                "Show.S01E02.720p.x264-GRP",
                null,
                "Failed",
                "missing articles",
                "TV",
                null,
                "SABnzbd_nzo_2")
        ];

        var lookup = SabnzbdHistoryMatcher.BuildFailedReleaseKeysByEpisode("Show", ["S01E02"], items);

        Assert.True(lookup.ContainsKey("S01E02"));
        Assert.Contains("title:show.s01e02.720p.x264-grp", lookup["S01E02"]);
    }

    [Fact]
    public void BuildFailedNzoIdsByEpisode_ReturnsRetryableJobIds()
    {
        IReadOnlyList<SabnzbdHistoryItem> items =
        [
            new(
                "Show.S01E03.1080p.x265",
                "Show.S01E03.1080p.x265.nzb",
                "Failed",
                "missing articles",
                "TV",
                "Show/1/3",
                "SABnzbd_nzo_retry")
        ];

        var lookup = SabnzbdHistoryMatcher.BuildFailedNzoIdsByEpisode("Show", ["S01E03"], items);

        Assert.True(lookup.ContainsKey("S01E03"));
        Assert.Contains("SABnzbd_nzo_retry", lookup["S01E03"]);
    }
}