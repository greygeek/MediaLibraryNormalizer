namespace MediaLibraryNormalizer.Audit;

public interface INzbAvailabilityChecker
{
    /// <summary>
    /// Search for NZBs for a specific episode.
    /// Supply <paramref name="tvMazeId"/> or <paramref name="tvdbId"/> for best results;
    /// falls back to title-based search when neither is available.
    /// </summary>
    Task<IReadOnlyList<NzbSearchResult>> SearchAsync(
        string seriesTitle,
        int season,
        int episode,
        string? tvMazeId = null,
        string? tvdbId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Downloads the NZB file from <paramref name="downloadUrl"/> and saves it to <paramref name="destPath"/>.
    /// SABnzbd (or similar) should be configured to watch the parent folder.
    /// Returns <see langword="true"/> when the file was written successfully.
    /// </summary>
    Task<bool> DownloadNzbAsync(string downloadUrl, string destPath, CancellationToken ct = default);
}
