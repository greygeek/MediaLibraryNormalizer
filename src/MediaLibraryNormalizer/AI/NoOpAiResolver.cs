using MediaLibraryNormalizer.Models;
using Microsoft.Extensions.Logging;

namespace MediaLibraryNormalizer.AI;

/// <summary>
/// No-op AI resolver used when --ai flag is not set.
/// Always returns Uncertain so no merges are triggered.
/// </summary>
public class NoOpAiResolver(ILogger<NoOpAiResolver> logger) : IAiResolver
{
    public Task<AiVerdict> ResolveAsync(AiContext context)
    {
        logger.LogDebug("AI disabled — returning Uncertain for '{A}' ↔ '{B}'",
            context.TitleA, context.TitleB);
        return Task.FromResult(AiVerdict.Uncertain);
    }
}
