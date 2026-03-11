namespace MediaLibraryNormalizer.Models;

/// <summary>
/// Result of an empty-folder cleanup pass.
/// </summary>
public class FolderCleanupResult
{
    /// <summary>Folders that were successfully deleted or would be deleted in dry-run mode.</summary>
    public List<string> DeletedFolders { get; init; } = [];

    /// <summary>Folders that could not be deleted along with exception details.</summary>
    public List<FolderCleanupFailure> Failures { get; init; } = [];
}

/// <summary>
/// A folder cleanup failure with enough detail for UI review.
/// </summary>
public class FolderCleanupFailure
{
    public required string FolderPath { get; init; }

    public required string ExceptionType { get; init; }

    public required string Message { get; init; }
}
