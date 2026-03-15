namespace MediaLibraryNormalizer.Audit;

public class CatalogSeriesCandidate
{
    public required string SourceId { get; init; }

    public required string Title { get; init; }

    public int? Year { get; init; }
}