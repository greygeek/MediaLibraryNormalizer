using MediaLibraryNormalizer.Models;
using Microsoft.Extensions.Logging;

namespace MediaLibraryNormalizer.Matching;

/// <summary>
/// Detects duplicate episodes and selects the best version using quality ranking.
/// Priority: resolution > codec > file size > modification date.
/// Multi-episode files beat individual episode files.
/// </summary>
public class DuplicateDetector(ILogger<DuplicateDetector> logger) : IDuplicateDetector
{
    // Codec ranking — higher is better
    private static readonly Dictionary<string, int> CodecRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AV1"] = 5,
        ["HEVC"] = 4,
        ["H264"] = 3,
        ["VP9"] = 2,
        ["XviD"] = 1,
        ["DivX"] = 1
    };

    public DuplicateResult DetectDuplicates(List<EpisodeInfo> episodes)
    {
        var result = new DuplicateResult();

        // Group by season + episode number(s) key
        var grouped = episodes
            .GroupBy(e => BuildEpisodeKey(e))
            .ToList();

        foreach (var group in grouped)
        {
            if (group.Count() == 1)
            {
                result.Keep.Add(group.First());
                continue;
            }

            // Select best version
            var sorted = group
                .OrderByDescending(e => e.Episodes.Count) // multi-ep beats single
                .ThenByDescending(e => e.ResolutionValue)  // higher resolution
                .ThenByDescending(e => GetCodecRank(e.Codec)) // better codec
                .ThenByDescending(e => e.FileSize)          // larger file
                .ThenByDescending(e => e.ModifiedDate)      // newer
                .ToList();

            var best = sorted.First();
            result.Keep.Add(best);
            result.Discard.AddRange(sorted.Skip(1));

            logger.LogDebug(
                "Duplicate S{Season:D2}E{Ep}: keeping '{Best}' ({Res}, {Codec}, {Size}B) over {Count} duplicate(s)",
                best.Season, best.Episodes.FirstOrDefault(),
                Path.GetFileName(best.FilePath),
                best.Resolution ?? "unknown", best.Codec ?? "unknown", best.FileSize,
                sorted.Count - 1);
        }

        return result;
    }

    private static string BuildEpisodeKey(EpisodeInfo ep)
    {
        var episodes = string.Join(",", ep.Episodes.OrderBy(e => e));
        return $"S{ep.Season:D2}E{episodes}";
    }

    private static int GetCodecRank(string? codec)
    {
        if (codec is null) return 0;
        return CodecRank.GetValueOrDefault(codec, 0);
    }
}
