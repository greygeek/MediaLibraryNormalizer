namespace MediaLibraryNormalizer.Audit;

public class CatalogSeries
{
    public required string SourceId { get; init; }

    public required string SourceName { get; init; }

    public required string Title { get; init; }

    public int? Year { get; init; }

    public List<CatalogEpisode> Episodes { get; init; } = [];
}