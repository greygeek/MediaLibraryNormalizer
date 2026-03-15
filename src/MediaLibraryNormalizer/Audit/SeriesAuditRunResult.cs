namespace MediaLibraryNormalizer.Audit;

public class SeriesAuditRunResult
{
    public required string LibraryPath { get; init; }

    public required SeriesAuditSummary Summary { get; init; }

    public List<SeriesAuditItem> Series { get; init; } = [];

    public List<string> Errors { get; init; } = [];
}

public class SeriesAuditSummary
{
    public int SeriesScanned { get; init; }

    public int ReadySeries { get; init; }

    public int PartialSeries { get; init; }

    public int NoParsedEpisodeSeries { get; init; }

    public int ParsedEpisodeCount { get; init; }

    public int UnparseableFileCount { get; init; }

    public int CatalogMatchedSeries { get; init; }

    public int CatalogAmbiguousSeries { get; init; }

    public int CatalogErrorSeries { get; init; }

    public int SeriesWithMissingEpisodes { get; init; }

    public int MissingEpisodeCount { get; init; }
}

public class SeriesAuditItem
{
    public required string OriginalTitle { get; init; }

    public required string NormalizedTitle { get; init; }

    public int? Year { get; init; }

    public int TotalVideoFiles { get; init; }

    public int SeasonFolderCount { get; init; }

    public int ParsedEpisodeCount { get; init; }

    public int UnparseableFileCount { get; init; }

    public AuditSeriesStatus Status { get; init; }

    public CatalogProviderKind CatalogProvider { get; init; }

    public CatalogLookupStatus CatalogStatus { get; init; }

    public string CatalogStatusMessage { get; init; } = string.Empty;

    public string? CatalogMatchedTitle { get; init; }

    public int? CatalogMatchedYear { get; init; }

    public int CatalogEpisodeCount { get; init; }

    public string? CatalogSummary { get; init; }

    public List<string> CatalogGenres { get; init; } = [];

    public string? CatalogNetwork { get; init; }

    public string? CatalogSeriesStatus { get; init; }

    public double? CatalogRating { get; init; }

    public string? CatalogImageUrl { get; init; }

    public int MissingEpisodeCount { get; init; }

    public int ExtraEpisodeCount { get; init; }

    public List<string> EpisodeKeys { get; init; } = [];

    public List<string> MissingEpisodeKeys { get; init; } = [];

    public List<MissingEpisodeInfo> MissingEpisodes { get; init; } = [];

    public List<string> ExtraEpisodeKeys { get; init; } = [];

    public List<string> UnparseableFiles { get; init; } = [];
}