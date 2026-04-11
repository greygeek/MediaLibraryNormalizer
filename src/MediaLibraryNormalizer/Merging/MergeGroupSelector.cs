using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Merging;

/// <summary>
/// Selects which duplicate groups should be merged based on the configured merge mode.
/// </summary>
public static class MergeGroupSelector
{
    public static List<SeriesGroup> SelectForApprovalReview(
        List<SeriesGroup> groups,
        NormalizerConfig config,
        IEnumerable<string>? approvedSeriesKeys = null)
    {
        var selected = config.ExactMatchesWithFilesOnly
            ? groups.Where(IsExactMatchEligibleForApproval).ToList()
            : groups;

        if (approvedSeriesKeys is null)
            return selected;

        var approved = approvedSeriesKeys
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return selected
            .Where(group => approved.Contains(group.SeriesKey))
            .ToList();
    }

    public static List<SeriesGroup> Select(
        List<SeriesGroup> groups,
        NormalizerConfig config,
        IEnumerable<string>? approvedSeriesKeys = null)
    {
        var selected = config.ExactMatchesWithFilesOnly
            ? groups.Where(IsExactMatchWithRealFiles).ToList()
            : groups;

        if (approvedSeriesKeys is null)
            return selected;

        var approved = approvedSeriesKeys
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return selected
            .Where(group => approved.Contains(group.SeriesKey))
            .ToList();
    }

    public static bool IsExactMatchEligibleForApproval(SeriesGroup group)
    {
        return group.MatchMethod is MatchMethod.ExactKey or MatchMethod.CatalogId;
    }

    public static bool IsExactMatchWithRealFiles(SeriesGroup group)
    {
        return group.MatchMethod is MatchMethod.ExactKey or MatchMethod.CatalogId
            && group.DuplicateFolders.Any(folder => folder.FileCount > 0);
    }
}