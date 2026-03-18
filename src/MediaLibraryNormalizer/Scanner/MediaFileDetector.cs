namespace MediaLibraryNormalizer.Scanner;

/// <summary>
/// Detects video and associated media files by extension and naming patterns.
/// </summary>
public partial class MediaFileDetector : IMediaFileDetector
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".m4v", ".mov", ".ts"
    };

    private static readonly HashSet<string> SubtitleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".srt", ".ssa", ".ass", ".sub", ".idx"
    };

    private static readonly HashSet<string> MovieArtifactExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nfo", ".txt", ".sfv", ".jpg", ".jpeg", ".png"
    };

    private static readonly HashSet<string> AssociatedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".srt", ".ssa", ".ass", ".sub", ".idx", ".nfo", ".txt", ".jpg", ".jpeg", ".png"
    };

    public bool IsVideoFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return VideoExtensions.Contains(ext) && !IsSampleFile(filePath);
    }

    public bool IsAssociatedFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return AssociatedExtensions.Contains(ext);
    }

    public bool IsSubtitleFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return SubtitleExtensions.Contains(ext);
    }

    public bool IsMovieArtifactFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return MovieArtifactExtensions.Contains(ext);
    }

    // "sample" must be preceded by a dot or start-of-string, and followed by a dot, dash,
    // underscore, or end-of-string.  This prevents release group names like "qpel-sample"
    // (where sample is joined to the group token by a dash) from triggering false positives.
    [System.Text.RegularExpressions.GeneratedRegex(@"(^|\.)sample($|[._-])",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex SampleIndicatorRegex();

    public bool IsSampleFile(string filePath)
    {
        var stem = Path.GetFileNameWithoutExtension(filePath);
        return SampleIndicatorRegex().IsMatch(stem);
    }

    public bool IsSampleVideoFile(string filePath) =>
        VideoExtensions.Contains(Path.GetExtension(filePath)) && IsSampleFile(filePath);

    public IEnumerable<string> EnumerateVideoFiles(string directoryPath)
    {
        directoryPath = ResolveAccessibleDirectoryPath(directoryPath) ?? directoryPath;

        if (!Directory.Exists(directoryPath))
            yield break;

        foreach (var file in Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories))
        {
            if (IsVideoFile(file))
                yield return file;
        }
    }

    public IEnumerable<string> FindAssociatedFiles(string videoFilePath)
    {
        var dir = Path.GetDirectoryName(videoFilePath);
        dir = dir is null ? null : ResolveAccessibleDirectoryPath(dir) ?? dir;

        if (dir is null || !Directory.Exists(dir))
            yield break;

        var baseName = Path.GetFileNameWithoutExtension(videoFilePath);

        foreach (var file in Directory.EnumerateFiles(dir))
        {
            if (string.Equals(file, videoFilePath, StringComparison.OrdinalIgnoreCase))
                continue;

            var otherBase = Path.GetFileNameWithoutExtension(file);
            if (string.Equals(otherBase, baseName, StringComparison.OrdinalIgnoreCase)
                && IsAssociatedFile(file))
            {
                yield return file;
            }
        }
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
