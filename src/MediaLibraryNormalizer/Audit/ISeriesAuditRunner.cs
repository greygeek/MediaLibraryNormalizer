namespace MediaLibraryNormalizer.Audit;

public interface ISeriesAuditRunner
{
    Task<SeriesAuditRunResult> RunAsync(
        SeriesAuditOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}