using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Merging;

/// <summary>
/// Logs all filesystem operations for undo support.
/// </summary>
public interface ITransactionLog
{
    /// <summary>Log a single operation.</summary>
    Task LogAsync(MergeOperation operation);

    /// <summary>Write all collected operations to a JSON report file.</summary>
    Task SaveAsync(string filePath);

    /// <summary>Load operations from a JSON report file for undo.</summary>
    Task<List<MergeOperation>> LoadAsync(string filePath);

    /// <summary>Get all logged operations.</summary>
    IReadOnlyList<MergeOperation> Operations { get; }
}
