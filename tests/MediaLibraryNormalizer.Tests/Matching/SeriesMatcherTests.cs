using MediaLibraryNormalizer.AI;
using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Matching;
using MediaLibraryNormalizer.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediaLibraryNormalizer.Tests.Matching;

public class SeriesMatcherTests
{
    private static NormalizerConfig DefaultConfig() => new()
    {
        FuzzyThreshold = 92,
        AiThreshold = 85
    };

    private static SeriesMatcher CreateSut(NormalizerConfig? config = null) => new(
        config ?? DefaultConfig(),
        new NoOpAiResolver(NullLogger<NoOpAiResolver>.Instance),
        NullLogger<SeriesMatcher>.Instance);

    // === Exact key matching ===

    [Fact]
    public async Task MatchAsync_ExactDuplicates_GroupedTogether()
    {
        var items = new List<MediaItem>
        {
            CreateItem("/tv/Breaking Bad", "Breaking Bad", "Breaking Bad"),
            CreateItem("/tv/Breaking Bad (1)", "Breaking Bad (1)", "Breaking Bad"),
            CreateItem("/tv/Dexter", "Dexter", "Dexter")
        };

        var sut = CreateSut();
        var groups = await sut.MatchAsync(items);

        Assert.Single(groups);
        Assert.Equal(2, groups[0].AllFolders.Count);
        Assert.All(groups[0].AllFolders, f => Assert.Equal("Breaking Bad", f.SeriesKey));
    }

    [Fact]
    public async Task MatchAsync_NoMatches_EmptyGroups()
    {
        var items = new List<MediaItem>
        {
            CreateItem("/tv/Breaking Bad", "Breaking Bad", "Breaking Bad"),
            CreateItem("/tv/Dexter", "Dexter", "Dexter"),
            CreateItem("/tv/Lost", "Lost", "Lost")
        };

        var sut = CreateSut();
        var groups = await sut.MatchAsync(items);

        Assert.Empty(groups);
    }

    // === Year disambiguation ===

    [Fact]
    public async Task MatchAsync_SameNameDifferentYear_NotMerged()
    {
        var items = new List<MediaItem>
        {
            CreateItem("/tv/Doctor Who (1963)", "Doctor Who (1963)", "Doctor Who", 1963),
            CreateItem("/tv/Doctor Who (2005)", "Doctor Who (2005)", "Doctor Who", 2005)
        };

        var sut = CreateSut();
        var groups = await sut.MatchAsync(items);

        Assert.Empty(groups);
    }

    [Fact]
    public async Task MatchAsync_SameNameSameYear_Grouped()
    {
        var items = new List<MediaItem>
        {
            CreateItem("/tv/Doctor Who (2005)", "Doctor Who (2005)", "Doctor Who", 2005),
            CreateItem("/tv/Doctor.Who.2005", "Doctor.Who.2005", "Doctor Who", 2005)
        };

        var sut = CreateSut();
        var groups = await sut.MatchAsync(items);

        Assert.Single(groups);
        Assert.Equal(2, groups[0].AllFolders.Count);
    }

    // === Canonical folder selection ===

    [Fact]
    public async Task MatchAsync_CanonicalFolder_MostFilesWins()
    {
        var items = new List<MediaItem>
        {
            CreateItem("/tv/Breaking Bad", "Breaking Bad", "Breaking Bad", fileCount: 10),
            CreateItem("/tv/Breaking.Bad.COMPLETE", "Breaking.Bad.COMPLETE", "Breaking Bad", fileCount: 50)
        };

        var sut = CreateSut();
        var groups = await sut.MatchAsync(items);

        Assert.Single(groups);
        Assert.Equal("/tv/Breaking.Bad.COMPLETE", groups[0].CanonicalFolder?.Path);
    }

    // === Fuzzy matching ===

    [Fact]
    public async Task MatchAsync_FuzzyMatch_VeryCloseNames()
    {
        // These should be close enough for fuzzy matching  
        var items = new List<MediaItem>
        {
            CreateItem("/tv/The Simpsons", "The Simpsons", "The Simpsons"),
            CreateItem("/tv/The Simpsns", "The Simpsns", "The Simpsns") // typo
        };

        // Use a lower threshold to make fuzzy matching more likely
        var config = new NormalizerConfig { FuzzyThreshold = 85, AiThreshold = 70 };
        var sut = CreateSut(config);
        var groups = await sut.MatchAsync(items);

        Assert.Single(groups);
    }

    // === Three-way exact merge ===

    [Fact]
    public async Task MatchAsync_ThreeWayExactMatch_AllGrouped()
    {
        var items = new List<MediaItem>
        {
            CreateItem("/tv/Show A", "Show A", "Show A"),
            CreateItem("/tv/Show A (copy)", "Show A (copy)", "Show A"),
            CreateItem("/tv/Show A backup", "Show A backup", "Show A")
        };

        var sut = CreateSut();
        var groups = await sut.MatchAsync(items);

        Assert.Single(groups);
        Assert.Equal(3, groups[0].AllFolders.Count);
    }

    // === Catalog source ID matching ===

    [Fact]
    public async Task MatchAsync_SameCatalogSourceId_DifferentKeys_Grouped()
    {
        var items = new List<MediaItem>
        {
            CreateItem("/tv/Resident Alien", "Resident Alien", "Resident Alien",
                catalogSourceId: "tvdb-12345"),
            CreateItem("/tv/Resident Alien (2021)", "Resident Alien (2021)", "Resident Alien",
                year: 2021, catalogSourceId: "tvdb-12345")
        };

        var sut = CreateSut();
        var groups = await sut.MatchAsync(items);

        Assert.Single(groups);
        Assert.Equal(2, groups[0].AllFolders.Count);
        Assert.Equal(MatchMethod.CatalogId, groups[0].MatchMethod);
    }

    [Fact]
    public async Task MatchAsync_CatalogSourceId_MergesIntoExactKeyGroup()
    {
        var items = new List<MediaItem>
        {
            CreateItem("/tv/Shetland", "Shetland", "Shetland",
                catalogSourceId: "tvdb-99"),
            CreateItem("/tv/Shetland (copy)", "Shetland (copy)", "Shetland",
                catalogSourceId: "tvdb-99"),
            CreateItem("/tv/Shetland (2013)", "Shetland (2013)", "Shetland",
                year: 2013, catalogSourceId: "tvdb-99")
        };

        var sut = CreateSut();
        var groups = await sut.MatchAsync(items);

        Assert.Single(groups);
        Assert.Equal(3, groups[0].AllFolders.Count);
        Assert.Equal(MatchMethod.CatalogId, groups[0].MatchMethod);
    }

    [Fact]
    public async Task MatchAsync_DifferentCatalogSourceIds_NotGrouped()
    {
        var items = new List<MediaItem>
        {
            CreateItem("/tv/Show A", "Show A", "Show A",
                catalogSourceId: "tvdb-111"),
            CreateItem("/tv/Show B", "Show B", "Show B",
                catalogSourceId: "tvdb-222")
        };

        var sut = CreateSut();
        var groups = await sut.MatchAsync(items);

        Assert.Empty(groups);
    }

    [Fact]
    public async Task MatchAsync_NoCatalogSourceId_FallsBackToFuzzy()
    {
        var items = new List<MediaItem>
        {
            CreateItem("/tv/The Simpsons", "The Simpsons", "The Simpsons"),
            CreateItem("/tv/The Simpsns", "The Simpsns", "The Simpsns") // typo
        };

        var config = new NormalizerConfig { FuzzyThreshold = 85, AiThreshold = 70 };
        var sut = CreateSut(config);
        var groups = await sut.MatchAsync(items);

        Assert.Single(groups);
        Assert.Equal(MatchMethod.FuzzyMatch, groups[0].MatchMethod);
    }

    private static MediaItem CreateItem(
        string path,
        string originalName,
        string normalizedName,
        int? year = null,
        int fileCount = 5,
        string? catalogSourceId = null)
    {
        return new MediaItem
        {
            Path = path,
            OriginalName = originalName,
            NormalizedName = normalizedName,
            Year = year,
            FileCount = fileCount,
            CatalogSourceId = catalogSourceId
        };
    }
}
