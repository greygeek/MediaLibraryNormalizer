namespace MediaLibraryNormalizer.Audit;

public static class NzbReleaseIdentity
{
    public static string GetSeriesIdentityKey(string normalizedSeriesTitle, int? seriesYear)
    {
        var title = string.IsNullOrWhiteSpace(normalizedSeriesTitle)
            ? string.Empty
            : normalizedSeriesTitle.Trim().ToLowerInvariant();

        return seriesYear is null ? title : $"{title}|{seriesYear.Value}";
    }

    public static string GetReleaseKey(NzbSearchResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.NzbId))
            return $"id:{result.NzbId.Trim().ToLowerInvariant()}";

        return $"title:{NormalizeReleaseTitle(result.Title)}";
    }

    public static IReadOnlyList<string> GetComparableReleaseKeys(NzbSearchResult result) =>
        GetComparableReleaseKeys(result.Title, result.NzbId, GetReleaseKey(result));

    public static IReadOnlyList<string> GetComparableReleaseKeys(
        string? releaseTitle,
        string? nzbId,
        string? primaryReleaseKey = null)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(primaryReleaseKey))
            keys.Add(primaryReleaseKey.Trim());

        if (!string.IsNullOrWhiteSpace(nzbId))
            keys.Add($"id:{nzbId.Trim().ToLowerInvariant()}");

        var normalizedTitle = NormalizeReleaseTitle(releaseTitle);
        if (!string.IsNullOrWhiteSpace(normalizedTitle))
            keys.Add($"title:{normalizedTitle}");

        return keys.ToList();
    }

    public static string NormalizeReleaseTitle(string? title) =>
        string.IsNullOrWhiteSpace(title)
            ? string.Empty
            : title.Trim().ToLowerInvariant();
}