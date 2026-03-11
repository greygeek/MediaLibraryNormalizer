using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Merging;

/// <summary>
/// Moves files safely with retry logic and transaction logging.
/// </summary>
public interface IFileMover
{
    /// <summary>
    /// Move a file from source to destination, creating directories as needed.
    /// Also moves associated files (subtitles, nfo, etc.).
    /// Returns the list of operations performed.
    /// </summary>
    Task<List<MergeOperation>> MoveFileAsync(string source, string destination, bool dryRun);

    /// <summary>
    /// Delete a file and any associated sidecar files.
    /// </summary>
    Task<List<MergeOperation>> DeleteFileAsync(
        string filePath,
        bool dryRun,
        OperationType operationType = OperationType.DeleteSample);
}
