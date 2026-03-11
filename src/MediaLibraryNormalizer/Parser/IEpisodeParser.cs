using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Parser;

/// <summary>
/// Parses TV episode filenames to extract structured metadata.
/// </summary>
public interface IEpisodeParser
{
    /// <summary>
    /// Parse a video filename and extract episode metadata.
    /// Returns null if the filename cannot be parsed.
    /// </summary>
    EpisodeInfo? Parse(string filePath);
}
