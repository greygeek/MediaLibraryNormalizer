namespace MediaLibraryNormalizer.Audit;

/// <summary>
/// A genre available for series discovery filtering.
/// </summary>
public sealed class DiscoveryGenre
{
    public required int Id { get; init; }
    public required string Name { get; init; }
}
