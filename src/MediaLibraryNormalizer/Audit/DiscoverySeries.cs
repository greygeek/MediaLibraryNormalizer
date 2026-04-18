namespace MediaLibraryNormalizer.Audit;

/// <summary>
/// A series discovered via genre-based browsing from an online catalog.
/// </summary>
public sealed class DiscoverySeries
{
    public required string SourceId { get; init; }
    public required string Title { get; init; }
    public int? Year { get; init; }
    public string? Overview { get; init; }
    public string? ImageUrl { get; init; }
    public double? Score { get; init; }
    public string? Status { get; init; }
    public string? Country { get; init; }
    public string? FirstAired { get; init; }
    public IReadOnlyList<string> Genres { get; init; } = [];
}
