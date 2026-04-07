using MediaLibraryNormalizer.Audit;

namespace MediaLibraryNormalizer.Tests.Audit;

public sealed class SeriesAuditRunnerTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"SeriesAuditRunnerTests_{Guid.NewGuid():N}");

    public SeriesAuditRunnerTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task RunAsync_BuildsInventorySummary_ForParseableAndPartialSeries()
    {
        var parseableSeries = CreateDirectory("Ahsoka");
        CreateFile(Path.Combine(CreateSeason(parseableSeries, "Season 01"), "Ahsoka.S01E01.mkv"));
        CreateFile(Path.Combine(CreateSeason(parseableSeries, "Season 01"), "Ahsoka.S01E02.mkv"));

        var partialSeries = CreateDirectory("Andor");
        CreateFile(Path.Combine(CreateSeason(partialSeries, "Season 01"), "Andor.S01E01.mkv"));
        CreateFile(Path.Combine(CreateSeason(partialSeries, "Season 01"), "Andor.Extras.Featurette.mkv"));

        var sut = new SeriesAuditRunner();

        var result = await sut.RunAsync(new SeriesAuditOptions
        {
            LibraryPath = _tempRoot
        });

        Assert.Equal(2, result.Summary.SeriesScanned);
        Assert.Equal(1, result.Summary.ReadySeries);
        Assert.Equal(1, result.Summary.PartialSeries);
        Assert.Equal(0, result.Summary.NoParsedEpisodeSeries);
        Assert.Equal(3, result.Summary.ParsedEpisodeCount);
        Assert.Equal(1, result.Summary.UnparseableFileCount);

        var ahsoka = Assert.Single(result.Series, series => series.NormalizedTitle == "Ahsoka");
        Assert.Equal(AuditSeriesStatus.ReadyForCatalogLookup, ahsoka.Status);
        Assert.Equal(["S01E01", "S01E02"], ahsoka.EpisodeKeys);

        var andor = Assert.Single(result.Series, series => series.NormalizedTitle == "Andor");
        Assert.Equal(AuditSeriesStatus.PartialInventory, andor.Status);
        Assert.Single(andor.UnparseableFiles);
    }

    [Fact]
    public async Task RunAsync_MarksSeriesWithoutParsedEpisodes_AsNoParsedEpisodes()
    {
        var seriesDir = CreateDirectory("Skeleton Crew");
        CreateFile(Path.Combine(seriesDir, "Skeleton.Crew.Trailer.mkv"));

        var sut = new SeriesAuditRunner();

        var result = await sut.RunAsync(new SeriesAuditOptions
        {
            LibraryPath = _tempRoot
        });

        var series = Assert.Single(result.Series);
        Assert.Equal(AuditSeriesStatus.NoParsedEpisodes, series.Status);
        Assert.Equal(0, series.ParsedEpisodeCount);
        Assert.Single(series.UnparseableFiles);
        Assert.Equal(1, result.Summary.NoParsedEpisodeSeries);
    }

    [Fact]
    public async Task RunAsync_WithCatalogProvider_ReportsMissingEpisodes()
    {
        var seriesDir = CreateDirectory("Ahsoka");
        CreateFile(Path.Combine(CreateSeason(seriesDir, "Season 01"), "Ahsoka.S01E01.mkv"));

        var sut = new SeriesAuditRunner(new FakeCatalogProvider());

        var result = await sut.RunAsync(new SeriesAuditOptions
        {
            LibraryPath = _tempRoot,
            CatalogProvider = CatalogProviderKind.TvMaze
        });

        var series = Assert.Single(result.Series);
        Assert.Equal(CatalogLookupStatus.Matched, series.CatalogStatus);
        Assert.Equal("Ahsoka", series.CatalogMatchedTitle);
        Assert.Equal(2, series.CatalogEpisodeCount);
        Assert.Equal(1, series.MissingEpisodeCount);
        Assert.Equal(["S01E02"], series.MissingEpisodeKeys);
        Assert.Equal(1, result.Summary.CatalogMatchedSeries);
        Assert.Equal(1, result.Summary.SeriesWithMissingEpisodes);
        Assert.Equal(1, result.Summary.MissingEpisodeCount);
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateSeason(string seriesDir, string seasonName)
    {
        var path = Path.Combine(seriesDir, seasonName);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateFile(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllBytes(path, [1, 2, 3, 4]);
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

    private sealed class FakeCatalogProvider : ISeriesCatalogProvider
    {
        public CatalogProviderKind Kind => CatalogProviderKind.TvMaze;

        public string DisplayName => "Fake TVMaze";

        public Task<IReadOnlyList<CatalogSeriesCandidate>> SearchSeriesAsync(string title, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CatalogSeriesCandidate> results =
            [
                new CatalogSeriesCandidate
                {
                    SourceId = "ahsoka-1",
                    Title = "Ahsoka",
                    Year = 2023
                }
            ];

            return Task.FromResult(results);
        }

        public Task<CatalogSeries> GetSeriesAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CatalogSeries
            {
                SourceId = sourceId,
                SourceName = DisplayName,
                Title = "Ahsoka",
                Year = 2023,
                Episodes =
                [
                    new CatalogEpisode { SeasonNumber = 1, EpisodeNumber = 1, Title = "Part One", AirDate = new DateOnly(2023, 8, 22) },
                    new CatalogEpisode { SeasonNumber = 1, EpisodeNumber = 2, Title = "Part Two", AirDate = new DateOnly(2023, 8, 22) }
                ]
            });
        }
    }
}