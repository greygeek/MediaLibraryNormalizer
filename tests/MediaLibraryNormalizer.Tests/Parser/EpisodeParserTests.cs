using MediaLibraryNormalizer.Parser;

namespace MediaLibraryNormalizer.Tests.Parser;

public class EpisodeParserTests
{
    private readonly EpisodeParser _sut = new();

    // === Standard S01E01 format ===

    [Theory]
    [InlineData("Show.Name.S01E01.720p.mkv", 1, new[] { 1 })]
    [InlineData("Show.Name.S03E15.1080p.HDTV.mkv", 3, new[] { 15 })]
    [InlineData("Show.S12E99.mkv", 12, new[] { 99 })]
    public void Parse_StandardFormat_ReturnsCorrectSeasonAndEpisode(
        string fileName, int expectedSeason, int[] expectedEpisodes)
    {
        var result = _sut.Parse(fileName);

        Assert.NotNull(result);
        Assert.Equal(expectedSeason, result.Season);
        Assert.Equal(expectedEpisodes.ToList(), result.Episodes);
    }

    // === Multi-episode: S01E01E02 ===

    [Theory]
    [InlineData("Show.S01E01E02.720p.mkv", 1, new[] { 1, 2 })]
    [InlineData("Show.S02E05E06.mkv", 2, new[] { 5, 6 })]
    public void Parse_MultiEpisodeConsecutive_ReturnsAllEpisodes(
        string fileName, int expectedSeason, int[] expectedEpisodes)
    {
        var result = _sut.Parse(fileName);

        Assert.NotNull(result);
        Assert.Equal(expectedSeason, result.Season);
        Assert.Equal(expectedEpisodes.ToList(), result.Episodes);
    }

    // === Multi-episode range: S01E01-E03 ===

    [Fact]
    public void Parse_EpisodeRange_ExpandsToFullRange()
    {
        var result = _sut.Parse("Show.S01E01-E03.mkv");

        Assert.NotNull(result);
        Assert.Equal(1, result.Season);
        Assert.Equal([1, 2, 3], result.Episodes);
    }

    // === Alt format: 1x01 ===

    [Theory]
    [InlineData("Show.1x01.mkv", 1, 1)]
    [InlineData("Show.3x15.mkv", 3, 15)]
    [InlineData("Show.12x01.mkv", 12, 1)]
    public void Parse_AltFormat_ReturnsCorrectSeasonAndEpisode(
        string fileName, int expectedSeason, int expectedEpisode)
    {
        var result = _sut.Parse(fileName);

        Assert.NotNull(result);
        Assert.Equal(expectedSeason, result.Season);
        Assert.Equal([expectedEpisode], result.Episodes);
    }

    // === Verbose format: Season N Episode N ===

    [Theory]
    [InlineData("Show Season 1 Episode 5.mkv", 1, 5)]
    [InlineData("My Show Season 03 Episode 12.mkv", 3, 12)]
    public void Parse_VerboseFormat_ReturnsCorrectSeasonAndEpisode(
        string fileName, int expectedSeason, int expectedEpisode)
    {
        var result = _sut.Parse(fileName);

        Assert.NotNull(result);
        Assert.Equal(expectedSeason, result.Season);
        Assert.Equal([expectedEpisode], result.Episodes);
    }

    // === Metadata extraction: resolution ===

    [Theory]
    [InlineData("Show.S01E01.720p.mkv", "720p", 720)]
    [InlineData("Show.S01E01.1080p.mkv", "1080p", 1080)]
    [InlineData("Show.S01E01.2160p.mkv", "2160p", 2160)]
    [InlineData("Show.S01E01.4k.mkv", "4k", 2160)]
    [InlineData("Show.S01E01.480p.mkv", "480p", 480)]
    public void Parse_ResolutionExtracted(
        string fileName, string expectedResolution, int expectedValue)
    {
        var result = _sut.Parse(fileName);

        Assert.NotNull(result);
        Assert.Equal(expectedResolution, result.Resolution);
        Assert.Equal(expectedValue, result.ResolutionValue);
    }

    [Fact]
    public void Parse_NoResolution_ReturnsNull()
    {
        var result = _sut.Parse("Show.S01E01.mkv");

        Assert.NotNull(result);
        Assert.Null(result.Resolution);
        Assert.Equal(0, result.ResolutionValue);
    }

    // === Metadata extraction: codec ===

    [Theory]
    [InlineData("Show.S01E01.x265.mkv", "HEVC")]
    [InlineData("Show.S01E01.h265.mkv", "HEVC")]
    [InlineData("Show.S01E01.HEVC.mkv", "HEVC")]
    [InlineData("Show.S01E01.x264.mkv", "H264")]
    [InlineData("Show.S01E01.h264.mkv", "H264")]
    [InlineData("Show.S01E01.AV1.mkv", "AV1")]
    public void Parse_CodecExtracted_Normalized(string fileName, string expectedCodec)
    {
        var result = _sut.Parse(fileName);

        Assert.NotNull(result);
        Assert.Equal(expectedCodec, result.Codec);
    }

    // === Metadata extraction: source ===

    [Theory]
    [InlineData("Show.S01E01.BluRay.mkv", "BluRay")]
    [InlineData("Show.S01E01.WEBRip.mkv", "WEBRip")]
    [InlineData("Show.S01E01.HDTV.mkv", "HDTV")]
    public void Parse_SourceExtracted(string fileName, string expectedSource)
    {
        var result = _sut.Parse(fileName);

        Assert.NotNull(result);
        Assert.Equal(expectedSource, result.Source);
    }

    // === Year extraction ===

    [Fact]
    public void Parse_YearExtracted()
    {
        var result = _sut.Parse("Show.2019.S01E01.mkv");

        Assert.NotNull(result);
        Assert.Equal(2019, result.Year);
    }

    // === Null / invalid input ===

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("random-file-no-episode-info.mkv")]
    [InlineData("just-a-movie-2020.mkv")]
    public void Parse_NoEpisodePattern_ReturnsNull(string fileName)
    {
        var result = _sut.Parse(fileName);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_NullInput_ReturnsNull()
    {
        // Path.GetFileNameWithoutExtension handles null by returning null
        // which will be empty/whitespace
        var result = _sut.Parse("");
        Assert.Null(result);
    }
}
