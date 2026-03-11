namespace MediaLibraryNormalizer.Models;

/// <summary>
/// Result of title normalization — contains the cleaned title and optionally extracted year.
/// </summary>
public record NormalizedTitle(string Title, int? Year)
{
    /// <summary>
    /// Composite key for series grouping. Year-disambiguated when present.
    /// Example: "Doctor Who|2005" or "Broadchurch"
    /// </summary>
    public string SeriesKey => Year.HasValue ? $"{Title}|{Year}" : Title;
}
