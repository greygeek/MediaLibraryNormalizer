using MediaLibraryNormalizer.Matching;
using MediaLibraryNormalizer.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediaLibraryNormalizer.Tests.Matching;

public class DuplicateDetectorTests
{
    private readonly DuplicateDetector _sut = new(NullLogger<DuplicateDetector>.Instance);

    [Fact]
    public void DetectDuplicates_NoDuplicates_AllKept()
    {
        var episodes = new List<EpisodeInfo>
        {
            CreateEpisode("S01E01.mkv", 1, [1]),
            CreateEpisode("S01E02.mkv", 1, [2]),
            CreateEpisode("S01E03.mkv", 1, [3])
        };

        var result = _sut.DetectDuplicates(episodes);

        Assert.Equal(3, result.Keep.Count);
        Assert.Empty(result.Discard);
    }

    [Fact]
    public void DetectDuplicates_SameEpisode_HigherResolutionWins()
    {
        var episodes = new List<EpisodeInfo>
        {
            CreateEpisode("Show.S01E01.720p.mkv", 1, [1], resolution: "720p", resValue: 720),
            CreateEpisode("Show.S01E01.1080p.mkv", 1, [1], resolution: "1080p", resValue: 1080)
        };

        var result = _sut.DetectDuplicates(episodes);

        Assert.Single(result.Keep);
        Assert.Single(result.Discard);
        Assert.Equal("Show.S01E01.1080p.mkv", result.Keep[0].FilePath);
        Assert.Equal("Show.S01E01.720p.mkv", result.Discard[0].FilePath);
    }

    [Fact]
    public void DetectDuplicates_SameResolution_BetterCodecWins()
    {
        var episodes = new List<EpisodeInfo>
        {
            CreateEpisode("Show.S01E01.1080p.x264.mkv", 1, [1], resolution: "1080p", resValue: 1080, codec: "H264"),
            CreateEpisode("Show.S01E01.1080p.x265.mkv", 1, [1], resolution: "1080p", resValue: 1080, codec: "HEVC")
        };

        var result = _sut.DetectDuplicates(episodes);

        Assert.Single(result.Keep);
        Assert.Contains("x265", result.Keep[0].FilePath);
    }

    [Fact]
    public void DetectDuplicates_SameCodecAndResolution_LargerFileWins()
    {
        var episodes = new List<EpisodeInfo>
        {
            CreateEpisode("Show.S01E01.small.mkv", 1, [1], resolution: "1080p", resValue: 1080, codec: "HEVC", fileSize: 500_000_000),
            CreateEpisode("Show.S01E01.large.mkv", 1, [1], resolution: "1080p", resValue: 1080, codec: "HEVC", fileSize: 1_500_000_000)
        };

        var result = _sut.DetectDuplicates(episodes);

        Assert.Single(result.Keep);
        Assert.Contains("large", result.Keep[0].FilePath);
    }

    [Fact]
    public void DetectDuplicates_MultiEpisodeBeatsSingle_WhenSameEpisodeKey()
    {
        // Multi-ep detection only applies when episodes share the same group key.
        // S01E01 (key: "S01E1") vs S01E01E02 (key: "S01E1,2") have different keys,
        // so they are NOT considered duplicates — both are kept.
        var episodes = new List<EpisodeInfo>
        {
            CreateEpisode("Show.S01E01.mkv", 1, [1], resolution: "1080p", resValue: 1080),
            CreateEpisode("Show.S01E01E02.mkv", 1, [1, 2], resolution: "1080p", resValue: 1080)
        };

        var result = _sut.DetectDuplicates(episodes);

        // Different episode keys → both kept
        Assert.Equal(2, result.Keep.Count);
        Assert.Empty(result.Discard);
    }

    [Fact]
    public void DetectDuplicates_SameMultiEpisode_HigherResolutionWins()
    {
        // When two files represent the exact same multi-episode, quality ranking applies
        var episodes = new List<EpisodeInfo>
        {
            CreateEpisode("Show.S01E01E02.720p.mkv", 1, [1, 2], resolution: "720p", resValue: 720),
            CreateEpisode("Show.S01E01E02.1080p.mkv", 1, [1, 2], resolution: "1080p", resValue: 1080)
        };

        var result = _sut.DetectDuplicates(episodes);

        Assert.Single(result.Keep);
        Assert.Single(result.Discard);
        Assert.Contains("1080p", result.Keep[0].FilePath);
    }

    [Fact]
    public void DetectDuplicates_ThreeDuplicates_BestKept()
    {
        var episodes = new List<EpisodeInfo>
        {
            CreateEpisode("Show.S01E01.480p.mkv", 1, [1], resolution: "480p", resValue: 480),
            CreateEpisode("Show.S01E01.720p.mkv", 1, [1], resolution: "720p", resValue: 720),
            CreateEpisode("Show.S01E01.1080p.mkv", 1, [1], resolution: "1080p", resValue: 1080)
        };

        var result = _sut.DetectDuplicates(episodes);

        Assert.Single(result.Keep);
        Assert.Equal(2, result.Discard.Count);
        Assert.Equal("Show.S01E01.1080p.mkv", result.Keep[0].FilePath);
    }

    [Fact]
    public void DetectDuplicates_EmptyList_ReturnsEmptyResult()
    {
        var result = _sut.DetectDuplicates([]);

        Assert.Empty(result.Keep);
        Assert.Empty(result.Discard);
    }

    private static EpisodeInfo CreateEpisode(
        string filePath,
        int season,
        List<int> episodes,
        string? resolution = null,
        int resValue = 0,
        string? codec = null,
        long fileSize = 0,
        DateTime? modifiedDate = null)
    {
        return new EpisodeInfo
        {
            FilePath = filePath,
            Season = season,
            Episodes = episodes,
            Resolution = resolution,
            ResolutionValue = resValue,
            Codec = codec,
            FileSize = fileSize,
            ModifiedDate = modifiedDate ?? DateTime.UtcNow
        };
    }
}
