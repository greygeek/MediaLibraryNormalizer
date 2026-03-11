using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Merging;

/// <summary>
/// Orchestrates merging of duplicate series folders.
/// </summary>
public interface ISeriesMerger
{
    /// <summary>
    /// Plan and optionally execute merge operations for all duplicate groups.
    /// </summary>
    Task<List<MergeOperation>> MergeAsync(List<SeriesGroup> groups, bool dryRun);

    /// <summary>
    /// Plan and optionally execute merges for equivalent subfolders within a series folder.
    /// </summary>
    Task<List<MergeOperation>> MergeSimilarSubfoldersAsync(IEnumerable<MediaItem> items, bool dryRun);

    /// <summary>
    /// Move movie files from per-movie folders into the library root and remove empty folders.
    /// </summary>
    Task<List<MergeOperation>> FlattenMovieFoldersAsync(IEnumerable<MediaItem> items, bool dryRun);
}
