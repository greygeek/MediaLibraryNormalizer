using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Merging;

/// <summary>
/// Moves files safely with retry logic and transaction logging.
/// </summary>
public interface IFileMover
{
    /// <summary>
    /// Optional callback for per-file transfer progress.
    /// Format: "COPYING filename.mkv|bytesCopied|totalBytes" during copy,
    /// "MOVED filename.mkv" on completion.
    /// </summary>
    IProgress<string>? FileTransferProgress { get; set; }

    /// <summary>
    /// Token checked between file operations. The current copy+verify+delete
    /// always runs to completion so no partial files or duplicates are left behind.
    /// </summary>
    CancellationToken CancellationToken { get; set; }

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

    /// <summary>
    /// Rename a file in-place (same directory) and also rename any associated sidecar files.
    /// </summary>
    Task<List<MergeOperation>> RenameInPlaceAsync(string source, string destination, bool dryRun);
}
