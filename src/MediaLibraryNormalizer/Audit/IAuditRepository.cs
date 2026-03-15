namespace MediaLibraryNormalizer.Audit;

public interface IAuditRepository
{
    Task SaveRunAsync(SeriesAuditRunResult result, CancellationToken ct = default);

    Task<(SeriesAuditRunResult Result, DateTime RunDate)?> LoadLatestRunAsync(
        string libraryPath, CancellationToken ct = default);
}
