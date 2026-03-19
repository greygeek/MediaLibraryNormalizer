using System.Text.Json.Serialization;

namespace MediaLibraryNormalizer.AI;

/// <summary>
/// A move operation suggested by the AI organizer.
/// </summary>
public record AiMoveInstruction(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("destination")] string Destination);

/// <summary>
/// Uses an AI model to suggest destination paths for video files that have
/// no parseable episode token and could not be organized by standard patterns.
/// </summary>
public interface IAiOrganizer
{
    /// <summary>
    /// Given absolute paths of video files the standard parser could not identify,
    /// ask the model to suggest move destinations under <paramref name="libraryRoot"/>.
    /// Returns only suggestions the implementation is confident about.
    /// </summary>
    Task<IReadOnlyList<AiMoveInstruction>> SuggestMovesAsync(
        string libraryRoot,
        IReadOnlyList<string> videoFilePaths,
        CancellationToken ct = default);
}
