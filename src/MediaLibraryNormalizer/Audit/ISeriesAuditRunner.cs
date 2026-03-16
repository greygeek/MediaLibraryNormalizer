namespace MediaLibraryNormalizer.Audit;

public interface ISeriesAuditRunner
{
    Task<SeriesAuditRunResult> RunAsync(
        SeriesAuditOptions options,
        IProgress<AuditProgressReport>? progress = null,
        ICatalogCache? catalogCache = null,
        CancellationToken cancellationToken = default);
}