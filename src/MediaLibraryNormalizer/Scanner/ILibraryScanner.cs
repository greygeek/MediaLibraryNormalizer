using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Scanner;

/// <summary>
/// Scans a media library directory and returns media items.
/// </summary>
public interface ILibraryScanner
{
    /// <summary>
    /// Scan top-level directories in the library path.
    /// Uses streaming enumeration for low memory usage.
    /// </summary>
    IEnumerable<MediaItem> Scan(string libraryPath);
}
