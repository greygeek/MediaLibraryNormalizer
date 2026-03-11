using MediaLibraryNormalizer.Config;

namespace MediaLibraryNormalizer.Models;

/// <summary>
/// Result of a pipeline execution.
/// </summary>
public class NormalizerRunResult
{
    /// <summary>Effective configuration used for the run.</summary>
    public required NormalizerConfig Config { get; init; }

    /// <summary>Scan and matching results.</summary>
    public required ScanResult ScanResult { get; init; }

    /// <summary>Planned or executed file-system operations.</summary>
    public List<MergeOperation> Operations { get; init; } = [];

    /// <summary>Number of duplicate groups selected for merge processing.</summary>
    public int SelectedDuplicateGroups { get; init; }

    /// <summary>Series keys selected for merge processing in this run.</summary>
    public List<string> SelectedSeriesKeys { get; init; } = [];

    /// <summary>Per-folder cleanup failures captured during the run.</summary>
    public List<FolderCleanupFailure> CleanupFailures { get; init; } = [];

    /// <summary>Count of duplicate folders cleaned as part of the approved-group cleanup phase.</summary>
    public int ApprovedCleanupFolderCount { get; init; }

    /// <summary>Count of folders removed by the global empty-folder cleanup sweep.</summary>
    public int GlobalCleanupSweepFolderCount { get; init; }

    /// <summary>Path of the JSON report written for the run.</summary>
    public string? ReportPath { get; init; }

    /// <summary>Path of the transaction log written for a live run.</summary>
    public string? TransactionLogPath { get; init; }
}
