using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.AI;

/// <summary>
/// Context passed to the AI resolver for title comparison.
/// </summary>
public class AiContext
{
    public required string TitleA { get; init; }
    public required string TitleB { get; init; }
    public int FolderAFileCount { get; init; }
    public int FolderBFileCount { get; init; }
    public int? DetectedYear { get; init; }
}

/// <summary>
/// Resolves whether two titles refer to the same series using AI.
/// </summary>
public interface IAiResolver
{
    /// <summary>
    /// Ask the AI whether two titles refer to the same television series.
    /// Returns SameSeries, DifferentSeries, or Uncertain.
    /// </summary>
    Task<AiVerdict> ResolveAsync(AiContext context);
}
