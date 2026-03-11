using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Merging;

/// <summary>
/// Detects and removes empty folders.
/// </summary>
public interface IEmptyFolderCleaner
{
    /// <summary>
    /// Find all empty folders under the given path.
    /// A folder is empty if it contains no video files and no non-empty subfolders.
    /// </summary>
    List<string> FindEmptyFolders(string rootPath);

    /// <summary>
    /// Remove empty folders. Returns list of deleted folder paths.
    /// </summary>
    Task<List<string>> CleanAsync(string rootPath, bool dryRun);

    /// <summary>
    /// Remove empty folders and capture any per-folder failures.
    /// </summary>
    Task<FolderCleanupResult> CleanDetailedAsync(string rootPath, bool dryRun);

    /// <summary>
    /// Remove the specified folders if they are empty. Returns list of deleted folder paths.
    /// </summary>
    Task<List<string>> CleanFoldersAsync(IEnumerable<string> folderPaths, bool dryRun);

    /// <summary>
    /// Remove the specified folders if they are empty and capture any per-folder failures.
    /// </summary>
    Task<FolderCleanupResult> CleanFoldersDetailedAsync(IEnumerable<string> folderPaths, bool dryRun);
}
