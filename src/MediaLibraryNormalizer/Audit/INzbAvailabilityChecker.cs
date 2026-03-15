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
    /// Adds an NZB to the NZBPlanet cart using its <paramref name="nzbId"/> (GUID from the search results).
    /// Returns <see langword="true"/> when the API responds with a success status.
    /// </summary>
    Task<bool> AddToCartAsync(string nzbId, CancellationToken ct = default);
}
