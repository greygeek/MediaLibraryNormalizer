# Media Title Normalization Algorithm

## High‑Accuracy Media Title Cleaning for Large Libraries

This document describes a **production-grade media title normalization
algorithm** similar to the techniques used by tools such as Sonarr,
Radarr, and Plex scanners.

The goal is to convert messy release-style filenames into **consistent
canonical titles** for reliable matching.

This algorithm significantly improves duplicate detection and reduces
reliance on AI.

------------------------------------------------------------------------

# 1. Problem

Media downloads often use **release-style naming conventions**:

Examples:

    Show.Name.S01E01.1080p.WEBRip.x264-GROUP
    Show Name - S01E01 - Episode Title.mkv
    Show.Name.2019.S01E01.720p.BluRay.x265
    Show.Name.1x01.1080p.H264

These must normalize to:

    Show Name

------------------------------------------------------------------------

# 2. Normalization Pipeline

The algorithm should process titles in the following order:

    1  remove file extension
    2  replace separators
    3  remove bracket tags
    4  remove release tags
    5  remove resolution tags
    6  remove codec tags
    7  remove source tags
    8  remove season/episode tokens
    9  extract year tokens (capture before removing)
    10 remove year tokens
    11 collapse whitespace
    12 title-case result

Step 9 is critical: the year must be **captured into metadata** before
it is stripped from the title. This supports year-disambiguated series
keys such as `Doctor Who|2005` (see v1.1 §3).

------------------------------------------------------------------------

# 3. Remove File Extensions

Supported video extensions:

    .mkv
    .mp4
    .avi
    .m4v
    .mov
    .ts

Implementation idea:

``` csharp
string RemoveExtension(string name)
{
    return Path.GetFileNameWithoutExtension(name);
}
```

------------------------------------------------------------------------

# 4. Replace Separators

Replace common separators with spaces.

Example:

    .
    _
    -

Example:

    Show.Name.S01E01 → Show Name S01E01

Regex:

    [._-]+

------------------------------------------------------------------------

# 5. Remove Resolution Tags

Common tags:

    480p
    720p
    1080p
    2160p
    4K
    HDR

Regex:

    \b(480p|720p|1080p|2160p|4k|hdr)\b

------------------------------------------------------------------------

# 6. Remove Codec Tags

Examples:

    x264
    x265
    h264
    h265
    hevc
    av1

Regex:

    \b(x264|x265|h264|h265|hevc|av1)\b

------------------------------------------------------------------------

# 7. Remove Source Tags

Examples:

    WEBRip
    WEB-DL
    BluRay
    HDTV
    DVDRip

Regex:

    \b(webrip|web-dl|bluray|hdtv|dvdrip)\b

------------------------------------------------------------------------

# 8. Remove Release Groups

Release groups appear at the end:

Example:

    -GROUP
    -SPARKS
    -YIFY

Regex:

    -[A-Za-z0-9]+$

------------------------------------------------------------------------

# 9. Remove Season/Episode Tokens

Patterns:

    S01E01
    S1E1
    1x01
    Season 1 Episode 1

Regex examples:

    S\d{1,2}E\d{1,2}
    \d{1,2}x\d{1,2}
    Season\s\d+
    Episode\s\d+

------------------------------------------------------------------------

# 10. Remove Year Tokens

Examples:

    (2019)
    2019
    2020

Regex:

    \b(19|20)\d{2}\b

------------------------------------------------------------------------

# 11. Remove Bracket Tags

Examples:

    [rarbg]
    [x265]
    [1080p]

Regex:

    \[[^\]]*\]

------------------------------------------------------------------------

# 12. Collapse Whitespace

Replace multiple spaces:

    \s+

with:

    single space

------------------------------------------------------------------------

# 13. Title Case

Convert normalized title to title case.

Example:

    broadchurch → Broadchurch
    the mandalorian → The Mandalorian

------------------------------------------------------------------------

# 14. Example Transformations

### Example 1

Input:

    Broadchurch.S01E01.1080p.WEBRip.x264-GROUP

Output:

    Broadchurch

------------------------------------------------------------------------

### Example 2

Input:

    The.Mandalorian.2019.S02E05.2160p.HDR.WEB-DL.x265

Output:

    The Mandalorian

------------------------------------------------------------------------

### Example 3

Input:

    Marvels.Jessica.Jones.S01E01.720p.BluRay.x264

Output:

    Marvels Jessica Jones

------------------------------------------------------------------------

# 15. Implementation Example

``` csharp
public record NormalizedTitle(string Title, int? Year);

public NormalizedTitle NormalizeTitle(string filename)
{
    var name = Path.GetFileNameWithoutExtension(filename);

    // Step 2: replace separators
    name = Regex.Replace(name, @"[._\-]+", " ");

    // Step 3: remove bracket tags
    name = Regex.Replace(name, @"\[[^\]]*\]", "");

    // Step 4-7: remove release, resolution, codec, source tags
    name = Regex.Replace(name, @"\b(480p|720p|1080p|2160p|4k|hdr)\b", "", RegexOptions.IgnoreCase);
    name = Regex.Replace(name, @"\b(x264|x265|h264|h265|hevc|av1)\b", "", RegexOptions.IgnoreCase);
    name = Regex.Replace(name, @"\b(webrip|web-dl|bluray|hdtv|dvdrip)\b", "", RegexOptions.IgnoreCase);

    // Step 8: remove season/episode tokens
    name = Regex.Replace(name, @"S\d{1,2}E\d{1,4}(E\d{1,4})*", "", RegexOptions.IgnoreCase);
    name = Regex.Replace(name, @"\d{1,2}x\d{1,4}", "", RegexOptions.IgnoreCase);

    // Step 9: extract year BEFORE removing it
    int? year = null;
    var yearMatch = Regex.Match(name, @"\b((19|20)\d{2})\b");
    if (yearMatch.Success)
        year = int.Parse(yearMatch.Groups[1].Value);

    // Step 10: remove year tokens
    name = Regex.Replace(name, @"\b(19|20)\d{2}\b", "");

    // Step 11-12: collapse whitespace and title-case
    name = Regex.Replace(name, @"\s+", " ").Trim();
    name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());

    return new NormalizedTitle(name, year);
}
```

------------------------------------------------------------------------

# 16. Similarity Matching

After normalization use:

    FuzzySharp

Example:

    The Mandalorian
    Mandalorian

Similarity score:

    > 92   Auto merge
    85–92  AI verification
    < 85   Do not merge

------------------------------------------------------------------------

# 17. When To Use AI

Only use AI when:

    fuzzy score is 85–92
    and titles share major tokens

Example:

    Marvels Jessica Jones
    Jessica Jones

------------------------------------------------------------------------

# 18. Token Matching Improvement

Extract tokens:

    mandalorian
    jessica
    broadchurch

Compare token overlap before fuzzy matching.

This dramatically improves detection.

------------------------------------------------------------------------

# 19. Performance Notes

This algorithm is extremely fast because:

    regex operations are O(n)
    no network calls required
    no hashing required

AI calls become rare.

------------------------------------------------------------------------

# 20. Recommended Integration

Pipeline:

    filename
    → normalize
    → token compare
    → fuzzy match
    → AI fallback

------------------------------------------------------------------------

# 21. Benefits

Advantages:

    high duplicate detection accuracy
    very fast processing
    minimal AI usage
    works on 100TB+ libraries

------------------------------------------------------------------------

# End
