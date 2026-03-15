namespace MediaLibraryNormalizer.Audit;

public interface ISeriesAuditRunner
{
    Task<SeriesAuditRunResult> RunAsync(
        SeriesAuditOptions options,
        IProgress<string>? progress = null,
        ICatalogCache? catalogCache = null,
        CancellationToken cancellationToken = default);
}