using MediaLibraryNormalizer.Normalization;

namespace MediaLibraryNormalizer.Tests.Normalization;

public class NameNormalizerTests
{
    private readonly NameNormalizer _sut = new();

    // === Basic normalization ===

    [Theory]
    [InlineData("Breaking Bad", "Breaking Bad", null)]
    [InlineData("breaking bad", "Breaking Bad", null)]
    [InlineData("BREAKING BAD", "Breaking BAD", null)] // BAD (<=4 chars, uppercase) preserved as potential acronym
    public void Normalize_SimpleNames_ReturnsTitleCase(string input, string expectedTitle, int? expectedYear)
    {
        var result = _sut.Normalize(input);
        Assert.Equal(expectedTitle, result.Title);
        Assert.Equal(expectedYear, result.Year);
    }

    // === Separator handling ===

    [Theory]
    [InlineData("Breaking.Bad", "Breaking Bad")]
    [InlineData("Breaking_Bad", "Breaking Bad")]
    [InlineData("The.Walking.Dead", "The Walking Dead")]
    [InlineData("Better_Call_Saul", "Better Call Saul")]
    public void Normalize_DotsAndUnderscores_ReplacedWithSpaces(string input, string expectedTitle)
    {
        var result = _sut.Normalize(input);
        Assert.Equal(expectedTitle, result.Title);
    }

    // === Year extraction ===

    [Theory]
    [InlineData("Doctor Who (2005)", "Doctor Who", 2005)]
    [InlineData("Battlestar Galactica (2003)", "Battlestar Galactica", 2003)]
    [InlineData("The Flash 2014", "The Flash", 2014)]
    [InlineData("Doctor.Who.2005", "Doctor Who", 2005)]
    public void Normalize_YearExtracted_BeforeStripping(string input, string expectedTitle, int expectedYear)
    {
        var result = _sut.Normalize(input);
        Assert.Equal(expectedTitle, result.Title);
        Assert.Equal(expectedYear, result.Year);
    }

    [Fact]
    public void Normalize_NoYear_ReturnsNull()
    {
        var result = _sut.Normalize("Breaking Bad");
        Assert.Null(result.Year);
    }

    // === Bracket tag removal ===

    [Theory]
    [InlineData("Show Name [1080p]", "Show Name")]
    [InlineData("[GROUP] Show Name [x265]", "Show Name")]
    [InlineData("Show [BluRay] [HEVC]", "Show")]
    public void Normalize_BracketTags_Removed(string input, string expectedTitle)
    {
        var result = _sut.Normalize(input);
        Assert.Equal(expectedTitle, result.Title);
    }

    // === Resolution, codec, source tag removal ===

    [Theory]
    [InlineData("Show 720p", "Show")]
    [InlineData("Show 1080p x265", "Show")]
    [InlineData("Show 2160p HEVC", "Show")]
    [InlineData("Show 4k HDR", "Show")]
    public void Normalize_ResolutionTags_Removed(string input, string expectedTitle)
    {
        var result = _sut.Normalize(input);
        Assert.Equal(expectedTitle, result.Title);
    }

    [Theory]
    [InlineData("Show x264", "Show")]
    [InlineData("Show HEVC", "Show")]
    [InlineData("Show h265", "Show")]
    public void Normalize_CodecTags_Removed(string input, string expectedTitle)
    {
        var result = _sut.Normalize(input);
        Assert.Equal(expectedTitle, result.Title);
    }

    [Theory]
    [InlineData("Show WEBRip", "Show")]
    [InlineData("Show BluRay", "Show")]
    [InlineData("Show HDTV", "Show")]
    [InlineData("Show WEB-DL", "Show")]
    public void Normalize_SourceTags_Removed(string input, string expectedTitle)
    {
        var result = _sut.Normalize(input);
        Assert.Equal(expectedTitle, result.Title);
    }

    // === Filename mode: release group and season/episode ===

    [Fact]
    public void Normalize_Filename_RemovesSeasonEpisode()
    {
        var result = _sut.Normalize("Show.Name.S01E01.720p.HDTV.x264-GROUP.mkv", isFilename: true);
        Assert.Equal("Show Name", result.Title);
    }

    [Fact]
    public void Normalize_Filename_RemovesExtension()
    {
        var result = _sut.Normalize("Show.Name.mkv", isFilename: true);
        Assert.Equal("Show Name", result.Title);
    }

    [Theory]
    [InlineData("Gladiator II (2024).1.mkv", "Gladiator II", 2024)]
    [InlineData("Godzilla x Kong The New Empire 2024 2160p WEB-DL H265 (1).mkv", "Godzilla X Kong The New Empire", 2024)]
    public void Normalize_Filename_StripsSabUniqueSuffix(string input, string expectedTitle, int expectedYear)
    {
        var result = _sut.Normalize(input, isFilename: true);
        Assert.Equal(expectedTitle, result.Title);
        Assert.Equal(expectedYear, result.Year);
    }

    // === Acronym preservation ===

    [Theory]
    [InlineData("NCIS", "NCIS")]
    [InlineData("ncis", "Ncis")] // lowercase input is title-cased; acronym detection only preserves existing uppercase
    public void Normalize_ShortAcronyms_PreservedUppercase(string input, string expectedTitle)
    {
        var result = _sut.Normalize(input);
        Assert.Equal(expectedTitle, result.Title);
    }

    [Theory]
    [InlineData("DC", "DC")]
    [InlineData("FBI", "FBI")]
    [InlineData("CSI", "CSI")]
    public void Normalize_TwoAndThreeLetterAcronyms_Preserved(string input, string expectedTitle)
    {
        var result = _sut.Normalize(input);
        Assert.Equal(expectedTitle, result.Title);
    }

    // === UNPACK folder handling ===

    [Theory]
    [InlineData("_UNPACK_Breaking Bad (2008)", "Breaking Bad", 2008)]
    [InlineData("_UNPACK_The.Flash.2014", "The Flash", 2014)]
    public void NormalizeUnpackFolder_StripsPrefix(string input, string expectedTitle, int expectedYear)
    {
        var result = _sut.NormalizeUnpackFolder(input);
        Assert.Equal(expectedTitle, result.Title);
        Assert.Equal(expectedYear, result.Year);
    }

    // === Empty / null input ===

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Normalize_EmptyOrNullInput_ReturnsEmpty(string? input)
    {
        var result = _sut.Normalize(input!);
        Assert.Equal(string.Empty, result.Title);
        Assert.Null(result.Year);
    }

    // === SeriesKey integration ===

    [Fact]
    public void NormalizedTitle_SeriesKey_IncludesYear()
    {
        var result = _sut.Normalize("Doctor Who (2005)");
        Assert.Equal("Doctor Who|2005", result.SeriesKey);
    }

    [Fact]
    public void NormalizedTitle_SeriesKey_NoYear_TitleOnly()
    {
        var result = _sut.Normalize("Breaking Bad");
        Assert.Equal("Breaking Bad", result.SeriesKey);
    }

    // === Complex real-world examples ===

    [Theory]
    [InlineData("The.Big.Bang.Theory.S01-S12.1080p.BluRay.x265-RARBG", "The Big Bang Theory S01-S12 -Rarbg")] // Season range and release group not stripped in folder mode
    [InlineData("Stranger.Things.2016.S01.1080p.NF.WEB-DL", "Stranger Things S01 NF", 2016)] // S01, NF remain in folder mode
    public void Normalize_ComplexRealWorldNames(string input, string expectedTitle, int? expectedYear = null)
    {
        var result = _sut.Normalize(input);
        Assert.Equal(expectedTitle, result.Title);
        if (expectedYear.HasValue)
            Assert.Equal(expectedYear, result.Year);
    }

    // === Whitespace collapsing ===

    [Fact]
    public void Normalize_MultipleSpaces_Collapsed()
    {
        var result = _sut.Normalize("Show   Name    Here");
        Assert.Equal("Show Name Here", result.Title);
    }
}
