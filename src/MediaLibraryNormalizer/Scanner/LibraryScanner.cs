using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Models;
using Microsoft.Extensions.Logging;

namespace MediaLibraryNormalizer.Scanner;

/// <summary>
/// High-performance library scanner using streaming enumeration.
/// </summary>
public class LibraryScanner(
    IMediaFileDetector fileDetector,
    NormalizerConfig config,
    ILogger<LibraryScanner> logger) : ILibraryScanner
{
    public IEnumerable<MediaItem> Scan(string libraryPath)
    {
        if (!Directory.Exists(libraryPath))
        {
            logger.LogError("Library path does not exist: {Path}", libraryPath);
            yield break;
        }

        logger.LogInformation("Scanning library: {Path}", libraryPath);

        foreach (var dir in Directory.EnumerateDirectories(libraryPath))
        {
            MediaItem? item = null;
            try
            {
                item = ScanDirectory(dir);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error scanning directory: {Dir}", dir);
            }

            if (item is not null)
                yield return item;
        }
    }

    private MediaItem ScanDirectory(string dirPath)
    {
        dirPath = ResolveAccessibleDirectoryPath(dirPath) ?? dirPath;

        var dirName = Path.GetFileName(dirPath);
        var isUnpack = IsUnpackFolder(dirName);

        var videoFiles = fileDetector.EnumerateVideoFiles(dirPath).ToList();
        var seasonFolders = DiscoverSeasonFolders(dirPath);

        if (config.Verbose)
            logger.LogDebug("Scanned {Dir}: {Count} video files, {Seasons} seasons",
                dirName, videoFiles.Count, seasonFolders.Count);

        return new MediaItem
        {
            Path = dirPath,
            OriginalName = dirName,
            FileCount = videoFiles.Count,
            VideoFiles = videoFiles,
            SeasonFolders = seasonFolders,
            IsUnpackFolder = isUnpack
        };
    }

    private static List<string> DiscoverSeasonFolders(string seriesPath)
    {
        var seasons = new List<string>();

        seriesPath = ResolveAccessibleDirectoryPath(seriesPath) ?? seriesPath;

        if (!Directory.Exists(seriesPath))
            return seasons;

        foreach (var subDir in Directory.EnumerateDirectories(seriesPath))
        {
            var folderName = Path.GetFileName(subDir);
            if (folderName.StartsWith("Season", StringComparison.OrdinalIgnoreCase)
                || folderName.StartsWith("Series", StringComparison.OrdinalIgnoreCase)
                || folderName.StartsWith("Specials", StringComparison.OrdinalIgnoreCase))
            {
                seasons.Add(subDir);
            }
        }

        return seasons;
    }

    private static bool IsUnpackFolder(string folderName)
    {
        return folderName.StartsWith("_UNPACK_", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveAccessibleDirectoryPath(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
            return directoryPath;

        var extendedDirectoryPath = ToExtendedPath(directoryPath);
        return Directory.Exists(extendedDirectoryPath)
            ? extendedDirectoryPath
            : null;
    }

    private static string ToExtendedPath(string path)
    {
        if (!OperatingSystem.IsWindows())
            return path;

        if (path.StartsWith("\\\\?\\", StringComparison.Ordinal))
            return path;

        if (path.StartsWith("\\\\", StringComparison.Ordinal))
            return "\\\\?\\UNC\\" + path[2..];

        return "\\\\?\\" + path;
    }
}
