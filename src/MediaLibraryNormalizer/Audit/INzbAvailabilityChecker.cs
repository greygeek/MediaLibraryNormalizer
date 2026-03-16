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
    /// Adds an NZB to the user's cart using <paramref name="nzbId"/> (the hash from the search result).
    /// Uses the NZBPlanet <c>t=cartadd</c> endpoint: <c>?t=cartadd&amp;id=HASH</c>.
    /// Returns <see langword="true"/> when the response indicates success.
    /// </summary>
    Task<bool> AddToCartAsync(string nzbId, string? userId = null, CancellationToken ct = default);
}
