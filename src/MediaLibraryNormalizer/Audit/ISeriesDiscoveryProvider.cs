namespace MediaLibraryNormalizer.Audit;

/// <summary>
/// Provides genre-based series discovery against an online catalog.
/// </summary>
public interface ISeriesDiscoveryProvider
{
    /// <summary>Returns the list of available genres.</summary>
    Task<IReadOnlyList<DiscoveryGenre>> GetGenresAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns popular/highly-rated series for the given genre, sorted by score descending.
    /// </summary>
    Task<IReadOnlyList<DiscoverySeries>> GetSeriesByGenreAsync(
        int genreId,
        string country = "usa",
        string language = "eng",
        int page = 0,
        CancellationToken cancellationToken = default);
}
