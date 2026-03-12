using System.Globalization;
using System.Text.RegularExpressions;
using MediaLibraryNormalizer.Models;

namespace MediaLibraryNormalizer.Normalization;

/// <summary>
/// Multi-step normalization pipeline for media titles.
/// Extracts year metadata before stripping it from the title.
/// </summary>
public partial class NameNormalizer : INameNormalizer
{
    // --- Compiled regex patterns ---

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex BracketTagsRegex();

    [GeneratedRegex(@"\b(480p|576p|720p|1080p|2160p|4k|hdr)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ResolutionRegex();

    [GeneratedRegex(@"\b(x264|x265|h264|h265|hevc|avc|xvid|divx|vp9|av1)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CodecRegex();

    [GeneratedRegex(@"\b(webrip|web[\-\s]?dl|bluray|hdtv|dvdrip|bdrip|brrip|pdtv|sdtv)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SourceRegex();

    [GeneratedRegex(@"S\d{1,2}E\d{1,4}(E\d{1,4})*", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonEpisodeRegex();

    [GeneratedRegex(@"\d{1,2}x\d{1,4}", RegexOptions.IgnoreCase)]
    private static partial Regex AltSeasonEpisodeRegex();

    [GeneratedRegex(@"\bSeason\s+\d+", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonWordRegex();

    [GeneratedRegex(@"\bEpisode\s+\d+", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodeWordRegex();

    [GeneratedRegex(@"\b((19|20)\d{2})\b")]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"\(([^)]*)\)")]
    private static partial Regex ParenthesesRegex();

    [GeneratedRegex(@"[._]+")]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    // Illegal Windows filename characters + control chars
    [GeneratedRegex(@"[<>:""/\\|?*\x00-\x1F]")]
    private static partial Regex IllegalCharsRegex();

    // Release group at end: only match after a known tag pattern has been stripped
    // This avoids mangling titles like "Spider-Man"
    [GeneratedRegex(@"\s*-\s*[A-Za-z0-9]+$")]
    private static partial Regex ReleaseGroupRegex();

    // SABnzbd and similar tools may append ".1" or " (1)" to make names unique.
    [GeneratedRegex(@"(?:\.\d{1,3}|\s*\(\d{1,3}\))$")]
    private static partial Regex SabSuffixRegex();

    public NormalizedTitle Normalize(string name, bool isFilename = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new NormalizedTitle(string.Empty, null);

        // Step 1: remove file extension (filenames only)
        if (isFilename)
            name = Path.GetFileNameWithoutExtension(name);

        name = StripSabUniqueSuffix(name);

        // Remove illegal Windows characters
        name = IllegalCharsRegex().Replace(name, "");

        // Step 2: replace separators (dots, underscores) with spaces
        // But preserve hyphens in word-context (like "Spider-Man")
        name = SeparatorRegex().Replace(name, " ");

        // Step 3: remove bracket tags [...]
        name = BracketTagsRegex().Replace(name, "");

        // Step 9: extract year BEFORE removing it
        int? year = ExtractYear(name);

        // Step 4: remove resolution tags
        name = ResolutionRegex().Replace(name, "");

        // Step 5: remove codec tags
        name = CodecRegex().Replace(name, "");

        // Step 6: remove source tags
        name = SourceRegex().Replace(name, "");

        // Step 7: remove release group (only for filenames where tags have been stripped)
        if (isFilename)
            name = ReleaseGroupRegex().Replace(name, "");

        // Step 8: remove season/episode tokens (filenames only)
        if (isFilename)
        {
            name = SeasonEpisodeRegex().Replace(name, "");
            name = AltSeasonEpisodeRegex().Replace(name, "");
            name = SeasonWordRegex().Replace(name, "");
            name = EpisodeWordRegex().Replace(name, "");
        }

        // Step 10: remove year tokens from parentheses and bare
        name = RemoveYearTokens(name);

        // Step 11: collapse whitespace and trim
        name = WhitespaceRegex().Replace(name, " ").Trim();

        // Remove trailing punctuation (dots, dashes, etc.)
        name = name.TrimEnd('.', '-', ' ');

        // Step 12: title-case (preserving existing uppercase for short words like "NCIS")
        name = TitleCase(name);

        return new NormalizedTitle(name, year);
    }

    public NormalizedTitle NormalizeUnpackFolder(string folderName)
    {
        // Strip _UNPACK_ prefix variants
        var stripped = folderName;
        if (stripped.StartsWith("_UNPACK_", StringComparison.OrdinalIgnoreCase))
            stripped = stripped[8..].TrimStart();

        return Normalize(stripped, isFilename: false);
    }

    private static int? ExtractYear(string name)
    {
        // Try parenthesized year first: (2019)
        var parenMatch = ParenthesesRegex().Match(name);
        while (parenMatch.Success)
        {
            if (int.TryParse(parenMatch.Groups[1].Value, out var parenYear)
                && parenYear >= 1900 && parenYear <= 2100)
            {
                return parenYear;
            }
            parenMatch = parenMatch.NextMatch();
        }

        // Then try bare year: 2019
        var yearMatch = YearRegex().Match(name);
        if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var bareYear)
            && bareYear >= 1900 && bareYear <= 2100)
        {
            return bareYear;
        }

        return null;
    }

    private static string RemoveYearTokens(string name)
    {
        // Remove (year) patterns
        name = ParenthesesRegex().Replace(name, match =>
        {
            if (int.TryParse(match.Groups[1].Value, out var y) && y >= 1900 && y <= 2100)
                return "";
            return match.Value;
        });

        // Remove bare year tokens
        name = YearRegex().Replace(name, "");

        return name;
    }

    private static string TitleCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // If the entire input is uppercase and short (likely an acronym like NCIS), keep it
        if (input.Length <= 5 && input == input.ToUpperInvariant())
            return input;

        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];

            // Preserve fully uppercase short words (acronyms: NCIS, DC, FBI, etc.)
            if (word.Length <= 4 && word == word.ToUpperInvariant()
                && word.Any(char.IsLetter))
            {
                continue;
            }

            // Otherwise title-case
            words[i] = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word.ToLowerInvariant());
        }

        return string.Join(' ', words);
    }

    private static string StripSabUniqueSuffix(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        var hasMetadataContext = YearRegex().IsMatch(name)
            || ResolutionRegex().IsMatch(name)
            || CodecRegex().IsMatch(name)
            || SourceRegex().IsMatch(name);

        if (!hasMetadataContext)
            return name;

        return SabSuffixRegex().Replace(name, "");
    }
}
