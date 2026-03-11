namespace MediaLibraryNormalizer.Models;

/// <summary>
/// Result returned by the AI title resolver.
/// </summary>
public enum AiVerdict
{
    SameSeries,
    DifferentSeries,
    Uncertain
}
