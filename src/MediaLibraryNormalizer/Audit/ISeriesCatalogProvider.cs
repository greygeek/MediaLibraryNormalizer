namespace MediaLibraryNormalizer.Audit;

public interface ISeriesCatalogProvider
{
    CatalogProviderKind Kind { get; }

    string DisplayName { get; }

    Task<IReadOnlyList<CatalogSeriesCandidate>> SearchSeriesAsync(string title, CancellationToken cancellationToken = default);

    Task<CatalogSeries> GetSeriesAsync(string sourceId, CancellationToken cancellationToken = default);
}