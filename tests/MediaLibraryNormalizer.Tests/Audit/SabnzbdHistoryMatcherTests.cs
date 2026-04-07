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
        Assert.Contains("title:shows01e011080px265", lookup["S01E01"]);
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
        Assert.Contains("title:shows01e02720px264-grp", lookup["S01E02"]);
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

    [Fact]
    public void BuildFailedReleaseKeysByEpisode_MatchesDespiteApostropheStrippedBySabnzbd()
    {
        // SABnzbd strips apostrophes from its Name field, so "Here's" becomes "Heres".
        // The failed history key must still match the NZBPlanet search result key.
        var nzbPlanetTitle = "For.All.Mankind.S02E08.And.Here's.to.You.EAC3.5.1.1080p.WEBRip.x265-SiQ";
        var sabHistoryName = "For All Mankind S02E08 And Heres to You EAC3 5 1 1080p WEBRip x265-SiQ";

        IReadOnlyList<SabnzbdHistoryItem> items =
        [
            new(
                sabHistoryName,
                nzbPlanetTitle + ".nzb",
                "Failed",
                "Aborted, cannot be completed",
                "TV",
                "For.All.Mankind/2/8",
                "SABnzbd_nzo_abc")
        ];

        var lookup = SabnzbdHistoryMatcher.BuildFailedReleaseKeysByEpisode(
            "for all mankind", ["S02E08"], items);

        Assert.True(lookup.ContainsKey("S02E08"));
        var failedKeys = lookup["S02E08"];

        // The NZBPlanet result should produce a key that matches one of the failed keys
        var nzbPlanetKeys = NzbReleaseIdentity.GetComparableReleaseKeys(nzbPlanetTitle, null);
        Assert.True(
            nzbPlanetKeys.Any(k => failedKeys.Contains(k)),
            $"Expected NZBPlanet keys [{string.Join(", ", nzbPlanetKeys)}] to overlap with failed keys [{string.Join(", ", failedKeys)}]");
    }
}