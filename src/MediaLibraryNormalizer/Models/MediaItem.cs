namespace MediaLibraryNormalizer.Models;

/// <summary>
/// Represents a top-level series folder in the media library.
/// </summary>
public class MediaItem
{
    /// <summary>Full filesystem path to the folder.</summary>
    public required string Path { get; init; }

    /// <summary>Original folder name as it appears on disk.</summary>
    public required string OriginalName { get; init; }

    /// <summary>Cleaned title after normalization.</summary>
    public string NormalizedName { get; set; } = string.Empty;

    /// <summary>Year extracted during normalization (null if none found).</summary>
    public int? Year { get; set; }

    /// <summary>Composite series key for grouping: "Title|Year" or "Title".</summary>
    public string SeriesKey => Year.HasValue ? $"{NormalizedName}|{Year}" : NormalizedName;

    /// <summary>Total count of video files across all season subfolders.</summary>
    public int FileCount { get; set; }

    /// <summary>Paths of all detected video files.</summary>
    public List<string> VideoFiles { get; set; } = [];

    /// <summary>Paths of season sub-folders (e.g. "Season 1").</summary>
    public List<string> SeasonFolders { get; set; } = [];

    /// <summary>Whether the original folder name is an _UNPACK_ variant.</summary>
    public bool IsUnpackFolder { get; set; }
}
