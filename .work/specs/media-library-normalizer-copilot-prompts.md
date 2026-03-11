# Media Library Normalizer -- Copilot Build Prompts

## Step‑by‑Step Prompts for VS Code + GitHub Copilot

This guide contains **ready‑to‑paste prompts** you can use with GitHub
Copilot Chat in VS Code to build the application described in the
specification.

Use each prompt sequentially while developing the project.

Target stack:

    .NET 10
    C#
    Console Application

------------------------------------------------------------------------

# 1. Project Initialization

Create project:

    dotnet new console -n MediaLibraryNormalizer
    cd MediaLibraryNormalizer
    code .

Copilot prompt:

    Create the initial project structure for a .NET console application called MediaLibraryNormalizer designed to process large media libraries (20TB+).

    Create folders:
    Config
    Scanner
    Parser
    Normalization
    Matching
    Merging
    Hashing
    AI
    Reporting
    Models

    Each folder should contain placeholder classes with clear responsibilities.

------------------------------------------------------------------------

# 2. Media Models

Copilot prompt:

    Create C# model classes representing media library objects.

    Include:
    MediaItem
    SeriesGroup
    EpisodeInfo
    ScanResult

    MediaItem should contain:
    Path
    Name
    NormalizedName
    FileCount
    VideoFiles
    SeasonFolders

------------------------------------------------------------------------

# 3. Library Scanner

Copilot prompt:

    Create a high performance LibraryScanner class for large media libraries.

    Requirements:
    - Scan top level directories only
    - Use Directory.EnumerateDirectories()
    - Avoid loading all results into memory
    - Return MediaItem objects

    The scanner must support very large libraries (100k folders).

------------------------------------------------------------------------

# 4. Folder Name Normalizer

Copilot prompt:

    Create a NameNormalizer class that converts messy media folder names into canonical series titles.

    Normalization rules:
    - trim whitespace
    - remove trailing dots
    - remove [tags]
    - remove (year)
    - remove encoding tags (x264, x265, HEVC)
    - collapse multiple spaces

    Examples:

    Broadchurch.
    Broadchurch 
    Broadchurch [x265]
    Broadchurch (2013)

    should normalize to:

    Broadchurch

------------------------------------------------------------------------

# 5. Episode Filename Parser

Copilot prompt:

    Create an EpisodeParser class that extracts season and episode numbers from filenames.

    Support formats:

    S01E01
    1x01
    Season 1 Episode 1

    Return an EpisodeInfo object containing:

    SeriesName
    Season
    Episode
    Resolution
    Codec
    Year

------------------------------------------------------------------------

# 6. Video File Detection

Copilot prompt:

    Create a MediaFileDetector class.

    Supported video extensions:

    .mkv
    .mp4
    .avi
    .m4v
    .mov
    .ts

    Ignore sample files and subtitles.

    Return only actual video files.

------------------------------------------------------------------------

# 7. Duplicate Series Detection

Copilot prompt:

    Create a SeriesMatcher class.

    Responsibilities:
    - group folders by normalized title
    - use fuzzy matching to detect similar titles
    - return groups representing duplicate series folders

    Use FuzzySharp library for similarity scoring.

------------------------------------------------------------------------

# 8. Canonical Folder Selection

Copilot prompt:

    Create logic that selects the canonical folder for a series group.

    Selection priority:

    1. folder with most video files
    2. folder without encoding tags
    3. folder matching normalized name
    4. alphabetical order

------------------------------------------------------------------------

# 9. Folder Merge Engine

Copilot prompt:

    Create a SeriesMerger class.

    Responsibilities:

    - merge duplicate series folders
    - move files to canonical folder
    - preserve season directory structure
    - avoid overwriting existing files
    - skip duplicates

    Operations must be safe for large media libraries.

------------------------------------------------------------------------

# 10. File Move Service

Copilot prompt:

    Create a FileMover class.

    Features:

    - safe file move
    - auto create destination directories
    - detect duplicate files
    - log moves

    File operations must be resilient and retry on failure.

------------------------------------------------------------------------

# 11. Duplicate File Detection

Copilot prompt:

    Create a DuplicateDetector class.

    Duplicate detection methods:

    - filename comparison
    - file size comparison
    - optional hash verification

    Implement fast hashing using xxHash.

------------------------------------------------------------------------

# 12. Hashing Engine

Copilot prompt:

    Create a MediaHasher class.

    Requirements:

    - calculate partial file hash
    - avoid hashing entire large files when possible
    - support SHA1 or xxHash

    Use hash prefix + file size to detect duplicates efficiently.

------------------------------------------------------------------------

# 13. AI Title Resolver

Copilot prompt:

    Create an AiTitleResolver class.

    Purpose:

    When fuzzy matching is uncertain, call an AI model to determine whether two series titles represent the same show.

    Example input:

    Marvels Jessica Jones
    Jessica Jones

    The AI should respond TRUE or FALSE.

    The class should support OpenAI API or Azure OpenAI.

------------------------------------------------------------------------

# 14. Empty Folder Cleaner

Copilot prompt:

    Create an EmptyFolderCleaner class.

    A folder is empty if:

    - it contains no video files
    - it contains only empty subfolders

    Remove these folders safely.

------------------------------------------------------------------------

# 15. Unpack Folder Handler

Copilot prompt:

    Create logic to detect _UNPACK_ folders created by download tools.

    Example:

    _UNPACK_Slow Horses

    Move files from unpack folders to the correct series folder.

------------------------------------------------------------------------

# 16. Reporting System

Copilot prompt:

    Create a ReportGenerator class.

    Outputs:

    JSON report
    Console summary

    Include:

    folders merged
    files moved
    duplicates skipped
    folders removed

------------------------------------------------------------------------

# 17. CLI Interface

Copilot prompt:

    Create command line argument parsing.

    Supported flags:

    --dry-run
    --merge
    --ai
    --hash
    --verbose

Example:

    MediaLibraryNormalizer.exe F:\TV --dry-run

------------------------------------------------------------------------

# 18. Parallel Processing

Copilot prompt:

    Add parallel scanning capabilities.

    Use Parallel.ForEach for:

    directory scanning
    episode parsing
    hash calculation

    Ensure file move operations remain serialized.

------------------------------------------------------------------------

# 19. Performance Safeguards

Copilot prompt:

    Optimize the application for very large media libraries.

    Avoid:

    Directory.GetFiles(recursive)

    Use streaming enumeration instead.

    Ensure memory usage remains low.

------------------------------------------------------------------------

# 20. End-to-End Pipeline

Copilot prompt:

    Create the main pipeline in Program.cs.

    Steps:

    scan library
    normalize names
    detect duplicates
    merge folders
    move files
    remove empty folders
    generate report

------------------------------------------------------------------------

# 21. Testing Dataset

Copilot prompt:

    Create a test dataset generator that creates mock media libraries with duplicate folders and episode files.

    Use this to validate the merging engine.

------------------------------------------------------------------------

# 22. Optional Future Features

Possible enhancements:

-   Plex API integration
-   Jellyfin integration
-   TMDB metadata lookups
-   bitrate comparison
-   quality upgrade detection
