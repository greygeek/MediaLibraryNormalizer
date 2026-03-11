using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Normalization;

/// <summary>
/// Normalizes media folder and file names into canonical titles.
/// </summary>
public interface INameNormalizer
{
    /// <summary>
    /// Normalize a folder or file name, extracting year before stripping.
    /// </summary>
    /// <param name="name">The raw folder or file name.</param>
    /// <param name="isFilename">True for filenames (full pipeline), false for folder names.</param>
    NormalizedTitle Normalize(string name, bool isFilename = false);

    /// <summary>
    /// Strip the _UNPACK_ prefix and normalize the remaining series name.
    /// </summary>
    NormalizedTitle NormalizeUnpackFolder(string folderName);
}
