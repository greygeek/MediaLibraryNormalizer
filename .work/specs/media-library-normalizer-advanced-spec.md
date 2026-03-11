# Media Library Normalizer -- Advanced Specification

## AI‑Assisted Large Media Library Cleanup Tool

**Target environment:** .NET 10 / C#\
**Primary development environment:** VS Code + GitHub Copilot\
**Target scale:** 20--100TB media libraries (100k+ files)

------------------------------------------------------------------------

# 1. Project Goal

Create a **high‑performance media library normalization tool** capable
of:

-   Cleaning large TV/Movie libraries
-   Merging duplicate series folders
-   Normalizing filenames
-   Detecting duplicate media
-   Using AI when metadata is ambiguous

The application must run safely on **very large media libraries** and
support **dry‑run preview mode**.

------------------------------------------------------------------------

# 2. Core Capabilities

## Folder normalization

Automatically fix folders containing:

-   trailing spaces
-   trailing dots
-   illegal Windows characters
-   failed rename artifacts
-   `_UNPACK_` folders

Example:

    Broadchurch
    Broadchurch.
    Broadchurch 
    Broadchurch [x265]

Normalized result:

    Broadchurch

------------------------------------------------------------------------

# 3. Duplicate Series Detection

Multiple folders representing the same show should be merged.

Example:

    The Mandalorian
    The Mandalorian [x265]
    The.Mandalorian
    The Mandalorian.

Algorithm:

1.  Normalize folder name
2.  Remove release tags
3.  Remove encoding tags
4.  Fuzzy match titles
5.  Optionally confirm using AI

------------------------------------------------------------------------

# 4. Folder Name Normalization Rules

Normalization should remove:

-   trailing whitespace
-   trailing punctuation
-   encoding tags
-   resolution tags
-   release group identifiers

Example regex pipeline:

    trim whitespace
    remove [tags]
    remove (year)
    remove release group
    collapse spaces

Example C# implementation idea:

``` csharp
string NormalizeTitle(string name)
{
    name = name.Trim();
    name = Regex.Replace(name, @"\[[^\]]*\]", "");
    name = Regex.Replace(name, @"\([^\)]*\)", "");
    name = Regex.Replace(name, @"\s+", " ");
    return name.Trim();
}
```

------------------------------------------------------------------------

# 5. Episode Filename Parsing Engine

The application must detect episodes from multiple naming formats.

Supported patterns:

    Show.Name.S01E01.mkv
    Show Name - S01E01.mkv
    Show.Name.1x01.mkv
    Show Name Season 1 Episode 1.mkv
    Show.Name.2019.S01E01.1080p.WEBRip.mkv

Regex examples:

    S(\d{2})E(\d{2})
    (\d+)x(\d+)
    Season\s(\d+)\sEpisode\s(\d+)

Parsed result model:

    Series
    Season
    Episode
    Year
    Resolution
    Encoding

------------------------------------------------------------------------

# 6. Media File Type Detection

Supported extensions:

    .mkv
    .mp4
    .avi
    .m4v
    .mov
    .ts

Ignore:

    .nfo
    .txt
    .jpg
    .srt
    .ssa
    .sample.mkv

------------------------------------------------------------------------

# 7. Duplicate Episode Detection

Episodes may appear in multiple folders.

Duplicate detection methods:

### Filename match

Compare parsed episode metadata.

### File size comparison

### Hash comparison (optional)

Use:

    xxHash
    SHA1

Recommended approach:

    size + hash prefix

------------------------------------------------------------------------

# 8. Canonical Folder Selection

When merging folders, choose canonical folder by priority:

1.  Folder with most video files
2.  Folder without encoding tags
3.  Folder matching normalized name
4.  Alphabetically first

------------------------------------------------------------------------

# 9. File Merge Logic

Example:

Before:

    Broadchurch
      Season 1

    Broadchurch [x265]
      Season 2

After:

    Broadchurch
      Season 1
      Season 2

Rules:

-   Preserve season structure
-   Create season folders if missing
-   Skip duplicates
-   Never overwrite files without confirmation

------------------------------------------------------------------------

# 10. Handling `_UNPACK_` Folders

Common in torrent/NZB pipelines.

Example:

    _UNPACK_Slow Horses

Logic:

-   move files into series folder
-   delete unpack folder if empty

------------------------------------------------------------------------

# 11. Detecting Empty Folders

Empty folders should be removed.

Definition:

    folder contains no video files
    and no non-empty subfolders

------------------------------------------------------------------------

# 12. Fuzzy Series Matching

Use **FuzzySharp** library.

Example:

    Marvels Jessica Jones
    Jessica Jones

Similarity scoring:

    > 92   Auto merge
    85–92  AI verification
    < 85   Do not merge

------------------------------------------------------------------------

# 13. AI Assisted Title Resolution

Use AI only when fuzzy matching is inconclusive.

Example prompt:

    Determine if these two titles represent the same television series.

    Title A: Marvels Jessica Jones
    Title B: Jessica Jones

    Answer only TRUE or FALSE.

Recommended providers:

-   Azure OpenAI
-   OpenAI API
-   local LLM

AI should be optional via CLI flag.

------------------------------------------------------------------------

# 14. Metadata Integration (Optional)

Integrate with:

    TMDB API
    TVDB API
    OMDb API

Use metadata to:

-   validate show titles
-   detect year
-   improve matching

------------------------------------------------------------------------

# 15. Architecture

    MediaLibraryNormalizer
    │
    ├─ Program.cs
    ├─ Config
    ├─ Scanner
    ├─ Parser
    ├─ Normalization
    ├─ Matching
    ├─ Merging
    ├─ AI
    ├─ Hashing
    └─ Reporting

Modules:

    LibraryScanner
    NameNormalizer
    EpisodeParser
    SeriesMatcher
    SeriesMerger
    DuplicateDetector
    AiResolver
    FileMover
    ReportGenerator

------------------------------------------------------------------------

# 16. Performance Design

Libraries can exceed:

    100,000 folders
    1,000,000 files

Performance rules:

Use

    Directory.EnumerateFiles()
    Directory.EnumerateDirectories()

Avoid

    Directory.GetFiles(recursive)

Use streaming enumeration.

------------------------------------------------------------------------

# 17. Parallel Scanning

Safe operations:

    directory scanning
    metadata parsing
    hash calculation

Use:

``` csharp
Parallel.ForEach()
```

Avoid parallel file moves.

------------------------------------------------------------------------

# 18. Safety Features

Required features:

    --dry-run
    --verbose
    --ai
    --hash
    --merge

Dry run must show planned changes.

------------------------------------------------------------------------

# 19. Logging

Output formats:

    JSON
    Console
    Markdown report

Example report:

    media-clean-report.json

------------------------------------------------------------------------

# 20. Example CLI Usage

    MediaLibraryNormalizer.exe F:\TV --dry-run

    MediaLibraryNormalizer.exe F:\TV --merge

    MediaLibraryNormalizer.exe F:\Movies --hash --ai

------------------------------------------------------------------------

# 21. Example Output

    Scanning library...

    Detected duplicate series:

    Broadchurch
      Broadchurch
      Broadchurch [x265]

    Merging folders...

    Moved:
    Season 2\Episode1.mkv

    Deleted empty folder:
    Broadchurch [x265]

    Scan complete.

------------------------------------------------------------------------

# 22. Test Dataset

Example:

    TestLibrary
     ├ Broadchurch
     ├ Broadchurch [x265]
     ├ Broadchurch.
     └ Broadchurch 

Expected result:

    Broadchurch

------------------------------------------------------------------------

# 23. Recommended Copilot Prompts

Example prompt for Copilot:

    Create a C# class that parses TV episode filenames using regex patterns for S01E01, 1x01, and Season 1 Episode 1.

Example prompt:

    Create a folder merging service that moves files between series folders while preserving season directories.

------------------------------------------------------------------------

# 24. Future Enhancements

Possible improvements:

-   Plex API integration
-   Jellyfin library refresh
-   duplicate movie detection
-   bitrate comparison
-   automatic quality upgrades

------------------------------------------------------------------------

# 25. Success Criteria

The application should:

-   normalize series folders
-   merge duplicates
-   detect duplicate episodes
-   clean empty folders
-   run safely on **40TB+ libraries**
