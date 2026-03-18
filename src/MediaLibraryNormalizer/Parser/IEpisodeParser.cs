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

    /// <summary>
    /// Returns the new absolute path if the filename should be renamed to standard
    /// <c>S##E##</c> format, or <see langword="null"/> if it is already standard or unparseable.
    /// </summary>
    string? TryNormalizeFilename(string filePath);
}
