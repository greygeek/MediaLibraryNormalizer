namespace MediaLibraryNormalizer.Models;

/// <summary>
/// Parsed episode metadata extracted from a video filename.
/// </summary>
public class EpisodeInfo
{
    /// <summary>Full path to the video file.</summary>
    public required string FilePath { get; init; }

    /// <summary>Series name extracted from the filename.</summary>
    public string SeriesName { get; set; } = string.Empty;

    /// <summary>Season number.</summary>
    public int Season { get; set; }

    /// <summary>
    /// Episode number(s). A list to support multi-episode files (e.g. S01E01E02 → [1,2]).
    /// </summary>
    public List<int> Episodes { get; set; } = [];

    /// <summary>Year extracted from filename, if present.</summary>
    public int? Year { get; set; }

    /// <summary>Resolution string (e.g. "1080p", "2160p").</summary>
    public string? Resolution { get; set; }

    /// <summary>Parsed resolution as integer for ranking (e.g. 1080, 2160).</summary>
    public int ResolutionValue { get; set; }

    /// <summary>Codec (e.g. "x265", "HEVC", "AV1").</summary>
    public string? Codec { get; set; }

    /// <summary>Source tag (e.g. "BluRay", "WEB-DL").</summary>
    public string? Source { get; set; }

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>Last write time of the file.</summary>
    public DateTime ModifiedDate { get; set; }
}
