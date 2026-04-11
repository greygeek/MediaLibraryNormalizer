using FuzzySharp;
using MediaLibraryNormalizer.AI;
using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Models;
using Microsoft.Extensions.Logging;

namespace MediaLibraryNormalizer.Matching;

/// <summary>
/// Groups series folders by exact series key match, then fuzzy matching,
/// with optional AI verification for borderline cases.
/// </summary>
public class SeriesMatcher(
    NormalizerConfig config,
    IAiResolver aiResolver,
    ILogger<SeriesMatcher> logger) : ISeriesMatcher
{
    public async Task<List<SeriesGroup>> MatchAsync(List<MediaItem> items)
    {
        var groups = new List<SeriesGroup>();

        // Step 1: Group by exact SeriesKey
        var exactGroups = items
            .GroupBy(i => i.SeriesKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        logger.LogInformation("Exact key grouping: {Count} unique series keys from {Total} folders",
            exactGroups.Count, items.Count);

        foreach (var (key, folderList) in exactGroups)
        {
            if (folderList.Count > 1)
            {
                var group = CreateGroup(key, folderList, MatchMethod.ExactKey);
                groups.Add(group);
            }
        }

        // Step 2: Merge by catalog source ID (e.g. TheTVDB ID from audit data)
        var singles = exactGroups
            .Where(g => g.Value.Count == 1)
            .Select(g => (Key: g.Key, Item: g.Value[0]))
            .ToList();

        singles = MergeByCatalogSourceId(singles, groups);

        // Step 3: Fuzzy match remaining single-folder keys
        var fuzzyGroups = await FuzzyMatchAsync(singles, groups);
        groups.AddRange(fuzzyGroups);

        // Step 3: Select canonical folders for each group
        foreach (var group in groups)
        {
            group.CanonicalFolder = SelectCanonicalFolder(group);
            group.CanonicalName = group.CanonicalFolder?.NormalizedName ?? group.SeriesKey;
        }

        logger.LogInformation("Found {Count} duplicate series groups", groups.Count);
        return groups;
    }

    private List<(string Key, MediaItem Item)> MergeByCatalogSourceId(
        List<(string Key, MediaItem Item)> singles,
        List<SeriesGroup> existingGroups)
    {
        // Build a CatalogSourceId → existing group lookup
        var groupByCatalogId = new Dictionary<string, SeriesGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in existingGroups)
        {
            foreach (var folder in group.AllFolders)
            {
                if (!string.IsNullOrEmpty(folder.CatalogSourceId)
                    && !groupByCatalogId.ContainsKey(folder.CatalogSourceId))
                {
                    groupByCatalogId[folder.CatalogSourceId] = group;
                }
            }
        }

        var remaining = new List<(string Key, MediaItem Item)>();
        var ungroupedByCatalogId = new Dictionary<string, List<(string Key, MediaItem Item)>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var single in singles)
        {
            var sourceId = single.Item.CatalogSourceId;
            if (string.IsNullOrEmpty(sourceId))
            {
                remaining.Add(single);
                continue;
            }

            // Merge into an existing exact-key group that shares the same catalog ID
            if (groupByCatalogId.TryGetValue(sourceId, out var existingGroup))
            {
                existingGroup.AllFolders.Add(single.Item);
                if (existingGroup.MatchMethod == MatchMethod.ExactKey)
                    existingGroup.MatchMethod = MatchMethod.CatalogId;

                logger.LogInformation(
                    "Catalog ID merge: '{Name}' → existing group '{Group}' (source {Id})",
                    single.Item.NormalizedName, existingGroup.SeriesKey, sourceId);
                continue;
            }

            // Accumulate singles that share a catalog ID for a new group
            if (!ungroupedByCatalogId.TryGetValue(sourceId, out var bucket))
            {
                bucket = [];
                ungroupedByCatalogId[sourceId] = bucket;
            }

            bucket.Add(single);
        }

        // Create new groups from singles that share a catalog source ID
        foreach (var (sourceId, bucket) in ungroupedByCatalogId)
        {
            if (bucket.Count > 1)
            {
                var items = bucket.Select(b => b.Item).ToList();
                var group = CreateGroup(bucket[0].Key, items, MatchMethod.CatalogId);
                existingGroups.Add(group);
                groupByCatalogId[sourceId] = group;

                logger.LogInformation(
                    "Catalog ID group: {Count} folders merged under '{Key}' (source {Id})",
                    items.Count, bucket[0].Key, sourceId);
            }
            else
            {
                remaining.Add(bucket[0]);
            }
        }

        return remaining;
    }

    private async Task<List<SeriesGroup>> FuzzyMatchAsync(
        List<(string Key, MediaItem Item)> singles,
        List<SeriesGroup> existingGroups)
    {
        var newGroups = new List<SeriesGroup>();
        var matched = new HashSet<int>();

        for (var i = 0; i < singles.Count; i++)
        {
            if (matched.Contains(i)) continue;

            var candidates = new List<MediaItem> { singles[i].Item };
            var bestScore = 0;

            for (var j = i + 1; j < singles.Count; j++)
            {
                if (matched.Contains(j)) continue;

                if (singles[i].Item.Kind != MediaKind.Unknown
                    && singles[j].Item.Kind != MediaKind.Unknown
                    && singles[i].Item.Kind != singles[j].Item.Kind)
                {
                    continue;
                }

                var leftYear = singles[i].Item.Year;
                var rightYear = singles[j].Item.Year;
                if (leftYear.HasValue
                    && rightYear.HasValue
                    && leftYear.Value != rightYear.Value)
                {
                    continue;
                }

                // Token pre-filter: skip if no tokens overlap
                if (!TokensOverlap(singles[i].Item.NormalizedName, singles[j].Item.NormalizedName))
                    continue;

                var score = Fuzz.Ratio(
                    singles[i].Item.NormalizedName.ToLowerInvariant(),
                    singles[j].Item.NormalizedName.ToLowerInvariant());

                if (score > config.FuzzyThreshold)
                {
                    // Auto merge
                    candidates.Add(singles[j].Item);
                    matched.Add(j);
                    bestScore = Math.Max(bestScore, score);

                    if (config.Verbose)
                        logger.LogDebug("Fuzzy auto-merge ({Score}): '{A}' ↔ '{B}'",
                            score, singles[i].Item.NormalizedName, singles[j].Item.NormalizedName);
                }
                else if (score >= config.AiThreshold && score <= config.FuzzyThreshold)
                {
                    // AI verification range
                    var verdict = await TryAiVerification(singles[i].Item, singles[j].Item, score);

                    if (verdict == AiVerdict.SameSeries)
                    {
                        candidates.Add(singles[j].Item);
                        matched.Add(j);
                        bestScore = Math.Max(bestScore, score);
                    }
                }
            }

            if (candidates.Count > 1)
            {
                matched.Add(i);
                var group = CreateGroup(singles[i].Key, candidates,
                    bestScore > 0 ? MatchMethod.FuzzyMatch : MatchMethod.ExactKey);
                group.FuzzyScore = bestScore;
                newGroups.Add(group);
            }
        }

        return newGroups;
    }

    private async Task<AiVerdict?> TryAiVerification(MediaItem a, MediaItem b, int fuzzyScore)
    {
        if (!config.UseAi)
        {
            logger.LogInformation(
                "Borderline match ({Score}): '{A}' ↔ '{B}' — skipped (AI not enabled)",
                fuzzyScore, a.NormalizedName, b.NormalizedName);
            return null;
        }

        logger.LogInformation(
            "AI verification ({Score}): '{A}' ↔ '{B}'",
            fuzzyScore, a.NormalizedName, b.NormalizedName);

        var context = new AiContext
        {
            TitleA = a.NormalizedName,
            TitleB = b.NormalizedName,
            FolderAFileCount = a.FileCount,
            FolderBFileCount = b.FileCount,
            DetectedYear = a.Year ?? b.Year
        };

        try
        {
            var verdict = await aiResolver.ResolveAsync(context);

            logger.LogInformation("AI verdict: {Verdict}", verdict);

            if (verdict == AiVerdict.Uncertain)
            {
                logger.LogWarning(
                    "AI returned UNCERTAIN for '{A}' ↔ '{B}' — skipping merge",
                    a.NormalizedName, b.NormalizedName);
            }

            return verdict;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI resolution failed for '{A}' ↔ '{B}'",
                a.NormalizedName, b.NormalizedName);
            return null;
        }
    }

    private static bool TokensOverlap(string a, string b)
    {
        var tokensA = a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tokensB = new HashSet<string>(
            b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        // At least one significant token must overlap (ignore very short tokens)
        return tokensA.Any(t => t.Length >= 3 && tokensB.Contains(t));
    }

    private static SeriesGroup CreateGroup(string key, List<MediaItem> folders, MatchMethod method)
    {
        return new SeriesGroup
        {
            SeriesKey = key,
            AllFolders = folders,
            MatchMethod = method
        };
    }

    /// <summary>
    /// Select the canonical folder by priority:
    /// 1. Most video files
    /// 2. Name without encoding tags
    /// 3. Name matches normalized title
    /// 4. Alphabetically first
    /// </summary>
    private static MediaItem SelectCanonicalFolder(SeriesGroup group)
    {
        return group.AllFolders
            .OrderByDescending(f => f.FileCount)
            .ThenBy(f => HasEncodingTags(f.OriginalName) ? 1 : 0)
            .ThenBy(f => string.Equals(f.OriginalName.Trim(), f.NormalizedName,
                StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(f => f.OriginalName, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static bool HasEncodingTags(string name)
    {
        var lower = name.ToLowerInvariant();
        string[] tags = ["x264", "x265", "h264", "h265", "hevc", "avc",
                         "xvid", "divx", "vp9", "av1", "720p", "1080p", "2160p"];
        return tags.Any(t => lower.Contains(t));
    }
}
