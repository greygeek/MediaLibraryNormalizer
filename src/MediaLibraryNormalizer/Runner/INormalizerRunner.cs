using MediaLibraryNormalizer.Config;
using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Runner;

public interface INormalizerRunner
{
    Task<NormalizerRunResult> RunAsync(
        NormalizerConfig config,
        IReadOnlyCollection<string>? approvedSeriesKeys = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? catalogSourceIds = null);

    Task UndoAsync(
        NormalizerConfig config,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
