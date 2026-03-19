namespace MediaLibraryNormalizer.AI;

/// <summary>No-op AI organizer used when AI organization is disabled.</summary>
public class NoOpAiOrganizer : IAiOrganizer
{
    public Task<IReadOnlyList<AiMoveInstruction>> SuggestMovesAsync(
        string libraryRoot,
        IReadOnlyList<string> videoFilePaths,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AiMoveInstruction>>([]);
}
