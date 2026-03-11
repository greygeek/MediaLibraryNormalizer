# Media Library Normalizer

## Specification v1.1 Clarifications

This document resolves ambiguities identified during review of the
**Advanced Specification v1.0**.\
It provides deterministic rules so the system can be implemented
consistently by developers and AI tools such as GitHub Copilot.

------------------------------------------------------------------------

# Technology Stack

Primary implementation stack:

    Language: C#
    Runtime: .NET 10
    Project Type: Console Application
    IDE: VS Code + GitHub Copilot

Recommended libraries:

    System.CommandLine
    Spectre.Console
    FuzzySharp
    xxHash
    Microsoft.Extensions.DependencyInjection

Optional integrations:

    OpenAI / Azure OpenAI
    TMDB API
    TVDB API

------------------------------------------------------------------------

# 1. Platform Scope

Target platforms:

    Primary: Windows
    Secondary: Linux / macOS (best effort)

Windows path edge cases (such as trailing spaces or dots) are a primary
use case.

Illegal Windows filename characters:

    < > : " / \ | ? *
    ASCII control characters (0–31)

These must be removed or replaced during normalization.

------------------------------------------------------------------------

# 2. Supported Library Types

### v1 Scope

The first release targets:

    TV series libraries only

Example structure:

    TV
     ├ Broadchurch
     │  ├ Season 1
     │  └ Season 2

### Movies

Movies are **out of scope for v1** and planned for v2.

Typical movie layout (future support):

    Movies
     ├ Dune (2021)
     │   └ Dune (2021).mkv

------------------------------------------------------------------------

# 3. Year Handling

Years must **not always be removed**.

Shows with reboots must preserve year disambiguation.

Examples:

    Doctor Who (1963)
    Doctor Who (2005)

Internal normalized key:

    Doctor Who|1963
    Doctor Who|2005

Series identifier model:

    SeriesKey = NormalizedTitle + "|" + Year(optional)

The episode parser extracts the year before normalization occurs.

------------------------------------------------------------------------

# 4. Duplicate File Conflict Resolution

When duplicate episodes are found the tool must select the **best
version**.

Priority order:

    1 Higher resolution
    2 Better codec
    3 Larger file size
    4 Newer modification date

Resolution ranking:

    2160p
    1080p
    720p
    480p

Codec ranking:

    AV1
    HEVC / x265
    H264 / x264
    Xvid
    DivX

------------------------------------------------------------------------

# 5. Multi‑Episode Files

Supported patterns:

    S01E01E02
    S01E01-E03
    S01E01-S01E03

Parser output example:

    Season: 1
    Episodes: [1,2,3]

Duplicate rule:

    A multi‑episode file beats individual episode files.

------------------------------------------------------------------------

# 6. Fuzzy Matching Policy

Similarity thresholds:

    > 92   Auto merge
    85–92  AI verification
    < 85   Do not merge

Example:

    Marvels Jessica Jones
    Jessica Jones

AI is used only for borderline matches.

------------------------------------------------------------------------

# 7. AI Resolution Prompt

Improved AI prompt format:

    Determine whether these titles refer to the same television series.

    Title A: Marvels Jessica Jones
    Title B: Jessica Jones

    Context:
    Folder A contains 18 files
    Folder B contains 20 files
    Detected year: 2015

    Respond with one of:

    SAME_SERIES
    DIFFERENT_SERIES
    UNCERTAIN

------------------------------------------------------------------------

# 8. Hashing Strategy

Full file hashing is too expensive for large libraries.

Use partial hashing:

    Hash first 1 MB
    Hash last 1 MB
    Combine with file size

Hash algorithm:

    xxHash64

------------------------------------------------------------------------

# 9. Concurrency Limits

Parallel operations allowed:

    directory scanning
    metadata parsing
    hash calculation

Maximum parallelism:

    MaxDegreeOfParallelism = min(8, CPU cores)

File moves must be serialized.

------------------------------------------------------------------------

# 10. File Types

Video file extensions:

    mkv
    mp4
    avi
    m4v
    mov
    ts

Associated files that should move with videos:

    srt
    ssa
    ass
    sub
    idx
    nfo
    jpg
    png

These follow the video during merges.

------------------------------------------------------------------------

# 11. Sample Files

Ignore filenames containing:

    .sample.
    -sample
    sample-

Examples:

    movie.sample.mkv
    episode-sample.mkv

Policy:

    Ignored during scanning
    Optional removal with --delete-samples

------------------------------------------------------------------------

# 12. *UNPACK* Folder Handling

Supported patterns:

    _UNPACK_ShowName
    _UNPACK_ ShowName
    _UNPACK_/ShowName

Processing steps:

    strip _UNPACK_ prefix
    normalize series name
    merge contents into series folder
    delete unpack folder when empty

If the target series folder does not exist:

    create new series folder

------------------------------------------------------------------------

# 13. Error Handling Strategy

File operations must implement retries.

Retry policy:

    max retries: 3
    retry delay: 500 ms

Failure scenarios:

    locked files
    network interruptions
    permission errors

Failures are logged but do not halt the scan.

------------------------------------------------------------------------

# 14. Transaction Log and Undo

All file operations must be logged.

Log format:

    JSON

Example:

``` json
{
 "operation": "move",
 "source": "F:\\TV\\Broadchurch [x265]\\Season2\\E01.mkv",
 "destination": "F:\\TV\\Broadchurch\\Season2\\E01.mkv"
}
```

Undo command:

    MediaLibraryNormalizer --undo report.json

------------------------------------------------------------------------

# 15. Default CLI Behavior

Running the tool without flags:

    MediaLibraryNormalizer F:\TV

Defaults to:

    --dry-run

To apply changes:

    --merge

------------------------------------------------------------------------

# 16. CLI Framework

Recommended CLI framework:

    System.CommandLine

Benefits:

    structured argument parsing
    tab completion
    clean help output

------------------------------------------------------------------------

# 17. Configuration File

Configuration file name:

    normalizer.config.json

Example:

``` json
{
  "fuzzyThreshold": 92,
  "aiThreshold": 85,
  "hashSizeMB": 1,
  "maxConcurrency": 8,
  "deleteSamples": false
}
```

------------------------------------------------------------------------

# 18. Encoding and Resolution Tags

Encoding tags to remove:

    x264
    x265
    h264
    h265
    hevc
    avc
    xvid
    divx
    vp9
    av1

Resolution tags:

    480p
    576p
    720p
    1080p
    2160p
    4k

------------------------------------------------------------------------

# 19. Dependency Injection

Use the built‑in .NET DI framework:

    Microsoft.Extensions.DependencyInjection

Benefits:

    testability
    replaceable AI providers
    replaceable metadata providers

------------------------------------------------------------------------

# 20. Testing Strategy

Testing layers:

### Unit Tests

    title normalization
    episode parser
    duplicate detection

### Integration Tests

Use temporary filesystem datasets.

### Performance Tests

Simulated media library:

    100k folders
    500k files

------------------------------------------------------------------------

# 21. Scale Target

The application must support:

    20–100 TB media libraries
    up to 1 million files

------------------------------------------------------------------------

# 22. Future Movie Support (v2)

Movie naming convention:

    Movie Title (Year)

Example:

    Dune (2021)

Movie parsing rules will be separate from TV logic.

------------------------------------------------------------------------

# End of Specification v1.1 Clarifications
