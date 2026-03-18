namespace MediaLibraryNormalizer.Models;

/// <summary>
/// A single planned or executed filesystem operation.
/// </summary>
public record MergeOperation(
    string Source,
    string Destination,
    OperationType Type,
    bool DryRun = false
);

/// <summary>Types of filesystem operations the tool can perform.</summary>
public enum OperationType
{
    Move,
    Delete,
    CreateDirectory,
    Rename,
    DeleteSample,
    DeleteDuplicate,
    DeleteNonEpisode
}
