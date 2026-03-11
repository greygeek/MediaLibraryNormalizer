namespace MediaLibraryNormalizer.Models;

/// <summary>
/// Summary result of a full library scan.
/// </summary>
public class ScanResult
{
    /// <summary>All scanned media items (series folders).</summary>
    public List<MediaItem> AllItems { get; set; } = [];

    /// <summary>Groups of duplicate series folders.</summary>
    public List<SeriesGroup> DuplicateGroups { get; set; } = [];

    /// <summary>Folders detected as _UNPACK_ variants.</summary>
    public List<MediaItem> UnpackFolders { get; set; } = [];

    /// <summary>Folders that contain no video files and no non-empty subfolders.</summary>
    public List<string> EmptyFolders { get; set; } = [];

    /// <summary>Total number of top-level folders scanned.</summary>
    public int TotalFolders { get; set; }

    /// <summary>Total number of video files found.</summary>
    public int TotalFiles { get; set; }

    /// <summary>Matches in the 85–92 range where AI returned UNCERTAIN or was not enabled.</summary>
    public List<UncertainMatch> UncertainMatches { get; set; } = [];

    /// <summary>Non-fatal errors encountered during scanning.</summary>
    public List<string> Errors { get; set; } = [];
}

/// <summary>
/// A pair of folders whose fuzzy score was borderline and couldn't be resolved.
/// </summary>
public class UncertainMatch
{
    public required string TitleA { get; init; }
    public required string TitleB { get; init; }
    public int FuzzyScore { get; init; }
    public int FileCountA { get; init; }
    public int FileCountB { get; init; }
    public int? YearA { get; init; }
    public int? YearB { get; init; }
    public AiVerdict? AiResult { get; init; }
}
