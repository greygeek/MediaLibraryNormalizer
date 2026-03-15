namespace MediaLibraryNormalizer.Audit;

public interface ICatalogCache
{
    Task<CatalogSeries?> GetAsync(
        CatalogProviderKind provider, string normalizedTitle,
        int expiryHours, CancellationToken ct = default);

    Task SetAsync(
        CatalogProviderKind provider, string normalizedTitle,
        CatalogSeries series, CancellationToken ct = default);
}
