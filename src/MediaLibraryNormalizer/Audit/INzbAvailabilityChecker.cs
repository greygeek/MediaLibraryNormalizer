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
    /// Downloads the NZB at <paramref name="downloadUrl"/> (the enclosure URL from search results).
    /// NZBPlanet does not support a cart API; fetching the NZB URL directly triggers the grab.
    /// Returns <see langword="true"/> when the response is a success with no Newznab error in the body.
    /// </summary>
    Task<bool> AddToCartAsync(string downloadUrl, CancellationToken ct = default);
}
