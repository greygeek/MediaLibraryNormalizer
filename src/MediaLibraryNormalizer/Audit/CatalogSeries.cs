namespace MediaLibraryNormalizer.Audit;

public class CatalogSeries
{
    public required string SourceId { get; init; }

    public required string SourceName { get; init; }

    public required string Title { get; init; }

    public int? Year { get; init; }

    public string? Summary { get; init; }

    public List<string> Genres { get; init; } = [];

    public string? Network { get; init; }

    public string? SeriesStatus { get; init; }

    public double? Rating { get; init; }

    public string? ImageUrl { get; init; }

    public List<CatalogEpisode> Episodes { get; init; } = [];
}