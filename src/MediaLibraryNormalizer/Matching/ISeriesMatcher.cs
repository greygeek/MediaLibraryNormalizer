using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Matching;

/// <summary>
/// Groups series folders by normalized title and fuzzy matching.
/// </summary>
public interface ISeriesMatcher
{
    /// <summary>
    /// Group media items into series groups by exact key and fuzzy matching.
    /// </summary>
    Task<List<SeriesGroup>> MatchAsync(List<MediaItem> items);
}
