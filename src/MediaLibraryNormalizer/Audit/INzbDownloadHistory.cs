namespace MediaLibraryNormalizer.Audit;

public interface INzbDownloadHistory
{
    Task<IReadOnlyList<NzbDownloadAttempt>> GetAttemptsAsync(
        string libraryPath,
        string normalizedSeriesTitle,
        int? seriesYear,
        string episodeKey,
        CancellationToken ct = default);

    Task RecordAttemptAsync(
        string libraryPath,
        string normalizedSeriesTitle,
        int? seriesYear,
        string episodeKey,
        NzbSearchResult result,
        string? sabNzoId = null,
        CancellationToken ct = default);

    Task ClearAttemptsAsync(
        string libraryPath,
        string normalizedSeriesTitle,
        int? seriesYear,
        string episodeKey,
        CancellationToken ct = default);

    Task<IReadOnlyList<NzbDownloadAttempt>> GetAllAttemptsAsync(CancellationToken ct = default);
}