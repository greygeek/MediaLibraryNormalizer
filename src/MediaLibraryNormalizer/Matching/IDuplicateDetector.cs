using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Matching;

/// <summary>
/// Detects and resolves duplicate episodes across folders.
/// </summary>
public interface IDuplicateDetector
{
    /// <summary>
    /// Given a list of episode infos from multiple folders, identify duplicates
    /// and select the best version of each.
    /// Returns the episodes to keep and the duplicates to discard.
    /// </summary>
    DuplicateResult DetectDuplicates(List<EpisodeInfo> episodes);
}

/// <summary>
/// Result of duplicate detection for a set of episodes.
/// </summary>
public class DuplicateResult
{
    /// <summary>Episodes to keep (best version of each).</summary>
    public List<EpisodeInfo> Keep { get; set; } = [];

    /// <summary>Episodes that are duplicates and should be skipped/discarded.</summary>
    public List<EpisodeInfo> Discard { get; set; } = [];
}
