namespace MediaLibraryNormalizer.Scanner;

/// <summary>
/// Detects video and associated media files.
/// </summary>
public interface IMediaFileDetector
{
    /// <summary>Returns true if the file extension is a supported video format.</summary>
    bool IsVideoFile(string filePath);

    /// <summary>Returns true if the file is an associated file that should move with a video.</summary>
    bool IsAssociatedFile(string filePath);

    /// <summary>Returns true if the file is a subtitle file that should be preserved.</summary>
    bool IsSubtitleFile(string filePath);

    /// <summary>Returns true if the file is a movie metadata/artifact file that can be discarded.</summary>
    bool IsMovieArtifactFile(string filePath);

    /// <summary>Returns true if the file is a sample file that should be ignored.</summary>
    bool IsSampleFile(string filePath);

    /// <summary>Returns true if the file is a video file matching the sample naming pattern (video extension + sample pattern).</summary>
    bool IsSampleVideoFile(string filePath);

    /// <summary>
    /// Enumerate all video files under the given directory (streaming).
    /// Excludes sample files.
    /// </summary>
    IEnumerable<string> EnumerateVideoFiles(string directoryPath);

    /// <summary>
    /// Find associated files that share the same base name as the given video file.
    /// </summary>
    IEnumerable<string> FindAssociatedFiles(string videoFilePath);
}
