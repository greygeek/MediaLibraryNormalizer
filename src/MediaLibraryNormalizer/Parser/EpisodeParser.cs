using System.Text.RegularExpressions;
using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Parser;

/// <summary>
/// Parses TV episode filenames using multiple regex patterns.
/// Supports S01E01, S01E01E02, S01E01-E03, 1x01, Season N Episode N.
/// </summary>
public partial class EpisodeParser : IEpisodeParser
{
    // Pattern priority order matters — try most specific first

    // S01E01E02 or S01E01-E03 or S01E01-S01E03 (multi-episode); also handles SE01E01 variant
    [GeneratedRegex(@"SE?(\d{1,2})E(\d{1,4})(?:[\-]?E(\d{1,4}))*", RegexOptions.IgnoreCase)]
    private static partial Regex MultiEpisodeRegex();

    // S01E01-S01E03 cross-season range (rare but supported); also handles SE01E01 variant
    [GeneratedRegex(@"SE?(\d{1,2})E(\d{1,4})\-SE?\d{1,2}E(\d{1,4})", RegexOptions.IgnoreCase)]
    private static partial Regex CrossSeasonRangeRegex();

    // 1x01 format
    [GeneratedRegex(@"\b(\d{1,2})x(\d{1,4})\b", RegexOptions.IgnoreCase)]
    private static partial Regex AltFormatRegex();

    // Season N Episode N (verbose)
    [GeneratedRegex(@"Season\s+(\d+)\s+Episode\s+(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex VerboseFormatRegex();

    // Metadata extraction patterns
    [GeneratedRegex(@"\b(2160p|1080p|720p|480p|576p|4k)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ResolutionRegex();

    [GeneratedRegex(@"\b(x264|x265|h\.?264|h\.?265|hevc|avc|xvid|divx|vp9|av1)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CodecRegex();

    [GeneratedRegex(@"\b(webrip|web[\-\s]?dl|bluray|hdtv|dvdrip|bdrip|brrip|pdtv)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SourceRegex();

    [GeneratedRegex(@"\b((19|20)\d{2})\b")]
    private static partial Regex YearRegex();

    // NofM format: "04of10" — episode N of M total (season comes from parent folder)
    [GeneratedRegex(@"\b(\d{1,2})of\d{1,2}\b", RegexOptions.IgnoreCase)]
    private static partial Regex NofMRegex();

    // Extracts season number from a parent folder name such as "Season 14"
    [GeneratedRegex(@"Season\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonFolderRegex();

    // Detects filenames that already contain a standard S##E## token
    [GeneratedRegex(@"S\d{1,2}E\d{1,4}", RegexOptions.IgnoreCase)]
    private static partial Regex StandardFormatRegex();

    public EpisodeInfo? Parse(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var (season, episodes) = ParseSeasonEpisode(fileName);
        if (season < 0 && episodes.Count > 0)
            season = ExtractSeasonFromPath(filePath);

        // Fallback: if the filename itself carries no episode info (e.g. a hash-named file),
        // try parsing the immediate parent folder name (e.g. New.Amsterdam.2018.S03E05.…).
        if (season < 0 || episodes.Count == 0)
        {
            var parentFolder = Path.GetFileName(Path.GetDirectoryName(filePath));
            if (!string.IsNullOrWhiteSpace(parentFolder))
            {
                var (folderSeason, folderEpisodes) = ParseSeasonEpisode(parentFolder);
                if (folderSeason >= 0 && folderEpisodes.Count > 0)
                {
                    season = folderSeason;
                    episodes = folderEpisodes;
                    fileName = parentFolder; // use folder name for metadata extraction below
                }
            }
        }

        if (season < 0 || episodes.Count == 0)
            return null;

        var info = new EpisodeInfo
        {
            FilePath = filePath,
            Season = season,
            Episodes = episodes
        };

        // Extract metadata
        info.Resolution = ExtractResolution(fileName);
        info.ResolutionValue = ParseResolutionValue(info.Resolution);
        info.Codec = ExtractCodec(fileName);
        info.Source = ExtractSource(fileName);
        info.Year = ExtractYear(fileName);

        // Populate file info if file exists
        try
        {
            if (File.Exists(filePath))
            {
                var fi = new FileInfo(filePath);
                info.FileSize = fi.Length;
                info.ModifiedDate = fi.LastWriteTimeUtc;
            }
        }
        catch
        {
            // Ignore filesystem errors during metadata read
        }

        return info;
    }

    private static (int season, List<int> episodes) ParseSeasonEpisode(string fileName)
    {
        // Try cross-season range first: S01E01-S01E03
        var crossMatch = CrossSeasonRangeRegex().Match(fileName);
        if (crossMatch.Success)
        {
            var season = int.Parse(crossMatch.Groups[1].Value);
            var startEp = int.Parse(crossMatch.Groups[2].Value);
            var endEp = int.Parse(crossMatch.Groups[3].Value);
            var episodes = Enumerable.Range(startEp, endEp - startEp + 1).ToList();
            return (season, episodes);
        }

        // Try multi-episode: S01E01E02 or S01E01-E03
        var multiMatch = MultiEpisodeRegex().Match(fileName);
        if (multiMatch.Success)
        {
            var season = int.Parse(multiMatch.Groups[1].Value);
            var episodes = new List<int> { int.Parse(multiMatch.Groups[2].Value) };

            // Capture additional episode numbers
            for (var i = 3; i < multiMatch.Groups.Count; i++)
            {
                foreach (Capture cap in multiMatch.Groups[i].Captures)
                {
                    if (int.TryParse(cap.Value, out var ep))
                        episodes.Add(ep);
                }
            }

            // If we have start and end (like E01-E03), fill the range
            if (episodes.Count == 2 && episodes[1] > episodes[0] + 1)
            {
                var start = episodes[0];
                var end = episodes[1];
                episodes = Enumerable.Range(start, end - start + 1).ToList();
            }

            return (season, episodes);
        }

        // Try 1x01 format
        var altMatch = AltFormatRegex().Match(fileName);
        if (altMatch.Success)
        {
            return (int.Parse(altMatch.Groups[1].Value),
                    [int.Parse(altMatch.Groups[2].Value)]);
        }

        // Try verbose: Season N Episode N
        var verboseMatch = VerboseFormatRegex().Match(fileName);
        if (verboseMatch.Success)
        {
            return (int.Parse(verboseMatch.Groups[1].Value),
                    [int.Parse(verboseMatch.Groups[2].Value)]);
        }

        // Try NofM: "04of10" — episode-only; season is resolved from the folder path in Parse()
        var nofMMatch = NofMRegex().Match(fileName);
        if (nofMMatch.Success)
        {
            return (-1, [int.Parse(nofMMatch.Groups[1].Value)]);
        }

        return (-1, []);
    }

    private static string? ExtractResolution(string fileName)
    {
        var match = ResolutionRegex().Match(fileName);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static int ParseResolutionValue(string? resolution)
    {
        if (resolution is null) return 0;

        return resolution.ToLowerInvariant() switch
        {
            "4k" or "2160p" => 2160,
            "1080p" => 1080,
            "720p" => 720,
            "576p" => 576,
            "480p" => 480,
            _ => 0
        };
    }

    private static string? ExtractCodec(string fileName)
    {
        var match = CodecRegex().Match(fileName);
        return match.Success ? NormalizeCodecName(match.Groups[1].Value) : null;
    }

    private static string NormalizeCodecName(string codec)
    {
        return codec.ToLowerInvariant().Replace(".", "") switch
        {
            "x265" or "h265" or "hevc" => "HEVC",
            "x264" or "h264" or "avc" => "H264",
            "av1" => "AV1",
            "xvid" => "XviD",
            "divx" => "DivX",
            "vp9" => "VP9",
            _ => codec
        };
    }

    private static string? ExtractSource(string fileName)
    {
        var match = SourceRegex().Match(fileName);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static int? ExtractYear(string fileName)
    {
        var match = YearRegex().Match(fileName);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var year)
            && year >= 1900 && year <= 2100)
        {
            return year;
        }
        return null;
    }

    /// <summary>
    /// Returns the new absolute file path if the filename should be renamed to standard
    /// <c>S##E##</c> format, or <see langword="null"/> if it already is standard.
    /// </summary>
    public string? TryNormalizeFilename(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (IsStandardFormat(fileName)) return null;

        var info = Parse(filePath);
        if (info is null) return null;

        var newName = BuildStandardName(fileName, info);
        if (string.Equals(newName, fileName, StringComparison.OrdinalIgnoreCase)) return null;

        return Path.Combine(Path.GetDirectoryName(filePath)!, newName + Path.GetExtension(filePath));
    }

    private static bool IsStandardFormat(string fileName) =>
        StandardFormatRegex().IsMatch(fileName);

    private string BuildStandardName(string fileName, EpisodeInfo info)
    {
        var epTag = $"S{info.Season:D2}E{string.Join("E", info.Episodes.Select(e => e.ToString("D2")))}";

        foreach (var regex in new Regex[] { CrossSeasonRangeRegex(), MultiEpisodeRegex(), AltFormatRegex(), VerboseFormatRegex(), NofMRegex() })
        {
            var m = regex.Match(fileName);
            if (m.Success)
                return fileName[..m.Index] + epTag + fileName[(m.Index + m.Length)..];
        }

        return epTag;
    }

    private static int ExtractSeasonFromPath(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(dir))
        {
            var folderName = Path.GetFileName(dir);
            if (folderName is not null)
            {
                var m = SeasonFolderRegex().Match(folderName);
                if (m.Success && int.TryParse(m.Groups[1].Value, out var s))
                    return s;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return -1;
    }
}
