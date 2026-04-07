using MediaLibraryNormalizer.Normalization;
using MediaLibraryNormalizer.Parser;

namespace MediaLibraryNormalizer.Audit;

public static class SabnzbdHistoryMatcher
{
    private static readonly NameNormalizer Normalizer = new();
    private static readonly EpisodeParser EpisodeParser = new();

    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> BuildFailedReleaseKeysByEpisode(
        string normalizedSeriesTitle,
        IEnumerable<string> episodeKeys,
        IReadOnlyList<SabnzbdHistoryItem> historyItems)
    {
        var wantedEpisodeKeys = new HashSet<string>(episodeKeys, StringComparer.OrdinalIgnoreCase);
        var lookup = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in historyItems)
        {
            if (!TryMatchSeriesEpisode(item, out var matchedSeriesTitle, out var matchedEpisodeKeys))
                continue;

            if (!string.Equals(matchedSeriesTitle, normalizedSeriesTitle, StringComparison.OrdinalIgnoreCase))
                continue;

            var releaseKeys = GetFailedReleaseKeys(item);
            if (releaseKeys.Count == 0)
                continue;

            foreach (var episodeKey in matchedEpisodeKeys)
            {
                if (!wantedEpisodeKeys.Contains(episodeKey))
                    continue;

                if (!lookup.TryGetValue(episodeKey, out var keys))
                {
                    keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    lookup[episodeKey] = keys;
                }

                foreach (var releaseKey in releaseKeys)
                    keys.Add(releaseKey);
            }
        }

        return lookup.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyCollection<string>)pair.Value.ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildFailedNzoIdsByEpisode(
        string normalizedSeriesTitle,
        IEnumerable<string> episodeKeys,
        IReadOnlyList<SabnzbdHistoryItem> historyItems)
    {
        var wantedEpisodeKeys = new HashSet<string>(episodeKeys, StringComparer.OrdinalIgnoreCase);
        var lookup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in historyItems)
        {
            if (string.IsNullOrWhiteSpace(item.NzoId))
                continue;

            if (!TryMatchSeriesEpisode(item, out var matchedSeriesTitle, out var matchedEpisodeKeys))
                continue;

            if (!string.Equals(matchedSeriesTitle, normalizedSeriesTitle, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var episodeKey in matchedEpisodeKeys)
            {
                if (!wantedEpisodeKeys.Contains(episodeKey))
                    continue;

                if (!lookup.TryGetValue(episodeKey, out var nzoIds))
                {
                    nzoIds = [];
                    lookup[episodeKey] = nzoIds;
                }

                if (!nzoIds.Contains(item.NzoId, StringComparer.OrdinalIgnoreCase))
                    nzoIds.Add(item.NzoId);
            }
        }

        return lookup.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> GetFailedReleaseKeys(SabnzbdHistoryItem item)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in NzbReleaseIdentity.GetComparableReleaseKeys(item.Name, null))
            keys.Add(key);

        var nzbNameWithoutExtension = string.IsNullOrWhiteSpace(item.NzbName)
            ? null
            : Path.GetFileNameWithoutExtension(item.NzbName);

        foreach (var key in NzbReleaseIdentity.GetComparableReleaseKeys(nzbNameWithoutExtension, null))
            keys.Add(key);

        return keys;
    }

    private static bool TryMatchSeriesEpisode(
        SabnzbdHistoryItem item,
        out string normalizedSeriesTitle,
        out IReadOnlyList<string> episodeKeys)
    {
        if (TryParseDuplicateKey(item.DuplicateKey, out normalizedSeriesTitle, out episodeKeys))
            return true;

        return TryParseFromReleaseName(item.Name, out normalizedSeriesTitle, out episodeKeys)
            || TryParseFromReleaseName(item.NzbName, out normalizedSeriesTitle, out episodeKeys);
    }

    private static bool TryParseDuplicateKey(
        string? duplicateKey,
        out string normalizedSeriesTitle,
        out IReadOnlyList<string> episodeKeys)
    {
        normalizedSeriesTitle = string.Empty;
        episodeKeys = [];

        if (string.IsNullOrWhiteSpace(duplicateKey))
            return false;

        var parts = duplicateKey.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
            return false;

        if (!int.TryParse(parts[1], out var seasonNumber) || !int.TryParse(parts[2], out var episodeNumber))
            return false;

        normalizedSeriesTitle = Normalizer.Normalize(parts[0], isFilename: false).Title;
        episodeKeys = [$"S{seasonNumber:D2}E{episodeNumber:D2}"];
        return !string.IsNullOrWhiteSpace(normalizedSeriesTitle);
    }

    private static bool TryParseFromReleaseName(
        string? releaseName,
        out string normalizedSeriesTitle,
        out IReadOnlyList<string> episodeKeys)
    {
        normalizedSeriesTitle = string.Empty;
        episodeKeys = [];

        if (string.IsNullOrWhiteSpace(releaseName))
            return false;

        var parsed = EpisodeParser.Parse(releaseName);
        if (parsed is null || parsed.Episodes.Count == 0)
            return false;

        normalizedSeriesTitle = Normalizer.Normalize(releaseName, isFilename: true).Title;
        episodeKeys = parsed.Episodes
            .Select(episode => $"S{parsed.Season:D2}E{episode:D2}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return !string.IsNullOrWhiteSpace(normalizedSeriesTitle);
    }
}