namespace MediaLibraryNormalizer.Models;

/// <summary>
/// A group of folders that all represent the same TV series.
/// </summary>
public class SeriesGroup
{
    /// <summary>Composite series key shared by all folders in this group.</summary>
    public required string SeriesKey { get; init; }

    /// <summary>Human-readable canonical name for the series.</summary>
    public string CanonicalName { get; set; } = string.Empty;

    /// <summary>The folder chosen as the merge target.</summary>
    public MediaItem? CanonicalFolder { get; set; }

    /// <summary>All folders in this group (including canonical).</summary>
    public List<MediaItem> AllFolders { get; set; } = [];

    /// <summary>Non-canonical folders that should be merged into the canonical one.</summary>
    public IEnumerable<MediaItem> DuplicateFolders =>
        AllFolders.Where(f => f != CanonicalFolder);

    /// <summary>Planned merge operations (populated during planning phase).</summary>
    public List<MergeOperation> PlannedOperations { get; set; } = [];

    /// <summary>How the match was determined.</summary>
    public MatchMethod MatchMethod { get; set; } = MatchMethod.ExactKey;

    /// <summary>Fuzzy match score (if applicable).</summary>
    public int? FuzzyScore { get; set; }
}

/// <summary>How a series group match was determined.</summary>
public enum MatchMethod
{
    ExactKey,
    FuzzyMatch,
    AiVerified
}
