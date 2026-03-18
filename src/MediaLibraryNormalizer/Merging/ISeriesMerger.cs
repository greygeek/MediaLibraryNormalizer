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

    /// <summary>
    /// Deduplicate top-level movie files already located in the library root.
    /// </summary>
    Task<List<MergeOperation>> DeduplicateTopLevelMovieFilesAsync(string libraryRoot, bool dryRun);

    /// <summary>
    /// Move video files from per-episode release folders at the library root into
    /// {SeriesTitle}/Season {N}/ and let the empty-folder sweep clean up afterward.
    /// </summary>
    Task<List<MergeOperation>> FlattenEpisodeReleaseFoldersAsync(
        IEnumerable<MediaItem> items, string libraryRoot, bool dryRun);
}
