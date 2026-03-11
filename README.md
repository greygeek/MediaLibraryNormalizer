# Media Library Normalizer

Media Library Normalizer is a .NET 10 toolset for cleaning and consolidating TV libraries.

It scans a library, normalizes folder names, detects duplicate series folders, compares duplicate episode files, plans safe file operations, and can execute those operations in either a CLI workflow or a desktop review-and-approval workflow.

## Highlights

- .NET 10 solution with shared core library, CLI, and desktop UI
- Top-level duplicate series detection using:
  - exact normalized series keys
  - fuzzy title matching
  - optional AI-assisted verification for borderline matches
- Duplicate episode resolution using quality ranking:
  - multi-episode files preferred when applicable
  - higher resolution
  - better codec
  - larger file size
  - newer modification date
- Optional discard mode for inferior duplicate files
- Empty-folder cleanup with detailed failure reporting
- Windows trailing-space path handling via extended paths
- Similar subfolder consolidation inside a single series folder
  - for example `Season 2` -> `Season 02`
- JSON report and transaction log output
- Undo support based on the transaction log
- Avalonia desktop UI with staged approval before live merge
- Automated test suite

## Solution Layout

- [MediaLibraryNormalizer.slnx](MediaLibraryNormalizer.slnx) — solution entry point
- [src/MediaLibraryNormalizer](src/MediaLibraryNormalizer) — shared normalization and merge engine
- [src/MediaLibraryNormalizer.Cli](src/MediaLibraryNormalizer.Cli) — command-line application
- [src/MediaLibraryNormalizer.Desktop](src/MediaLibraryNormalizer.Desktop) — Avalonia desktop application
- [tests/MediaLibraryNormalizer.Tests](tests/MediaLibraryNormalizer.Tests) — xUnit test project
- [normalizer.config.json](normalizer.config.json) — sample runtime configuration
- [.work/specs](.work/specs) — working specifications
- [.work/epics](.work/epics) — implementation epics and tasks

## Core Workflow

1. Scan top-level library folders.
2. Detect video files, sidecar files, season folders, and `_UNPACK_` folders.
3. Normalize titles and extract year metadata.
4. Group duplicates by exact key, then fuzzy matching, with optional AI verification.
5. Select canonical folders.
6. Merge duplicate folders into canonical folders.
7. Compare duplicate episodes and either:
   - keep the better copy and skip the worse one, or
   - delete inferior duplicates when discard mode is enabled.
8. Consolidate similarly named season folders inside a single series folder.
9. Remove empty folders.
10. Write a JSON report and, for live runs, a transaction log.

## Requirements

- Windows is the primary tested environment
- .NET SDK 10.x
- Optional: OpenAI-compatible API access for AI verification

## Configuration

Configuration is loaded from [normalizer.config.json](normalizer.config.json).

Important settings:

- `LibraryPath`
- `FuzzyThreshold`
- `AiThreshold`
- `HashSizeMB`
- `MaxConcurrency`
- `DeleteSamples`
- `ExactMatchesWithFilesOnly`
- `DiscardInferiorDuplicates`
- `UseAi`
- `UseHash`
- `Verbose`
- `AiEndpoint`
- `AiApiKey`
- `AiModel`

For AI authentication, prefer the `NORMALIZER_AI_KEY` environment variable over putting secrets in config.

## CLI Usage

Run a dry run:

- `dotnet run --project .\src\MediaLibraryNormalizer.Cli -- F:\TV`

Run a live merge:

- `dotnet run --project .\src\MediaLibraryNormalizer.Cli -- F:\TV --merge`

Common flags:

- `--dry-run`
- `--merge`
- `--exact-with-files-only`
- `--discard-inferior-duplicates`
- `--ai`
- `--hash`
- `--verbose`
- `--delete-samples`
- `--undo <transactionLogPath>`

Examples:

Dry run with hashing, AI, and discard-mode planning:

- `dotnet run --project .\src\MediaLibraryNormalizer.Cli -- F:\TV --ai --hash --discard-inferior-duplicates --delete-samples --verbose`

Undo a previous live run:

- `dotnet run --project .\src\MediaLibraryNormalizer.Cli -- F:\TV --undo F:\TV\media-clean-transactions.json`

## Desktop UI

Run the desktop app:

- `dotnet run --project .\src\MediaLibraryNormalizer.Desktop`

Desktop workflow:

1. Run a dry run.
2. Review duplicate groups and planned operations.
3. Stage approved groups.
4. Run approved live merge.
5. Review the generated report and transaction log.

The desktop shell uses the same core pipeline as the CLI.

## Output Files

The tool writes output into the library root:

- `media-clean-report.json` — run summary and planned/executed operations
- `media-clean-transactions.json` — live-run transaction log for undo

## Safety Notes

- Dry run is the default mode.
- Live operations are only performed when explicitly requested.
- Transaction logs are written for live runs.
- Empty-folder cleanup and delete operations are tracked.
- Windows extended-path handling is used for problematic names such as trailing-space folders.

## Testing

Build the core and CLI:

- `dotnet build .\src\MediaLibraryNormalizer\MediaLibraryNormalizer.csproj`
- `dotnet build .\src\MediaLibraryNormalizer.Cli\MediaLibraryNormalizer.Cli.csproj`

Run tests:

- `dotnet test .\tests\MediaLibraryNormalizer.Tests\MediaLibraryNormalizer.Tests.csproj`

## Current Capabilities Covered by Tests

- title normalization
- episode parsing
- duplicate episode resolution
- duplicate group selection rules
- hashing
- media file detection
- discard-inferior-duplicate behavior
- canonical season-folder reuse and normalization
- similar season-folder consolidation within one series folder

## Notes

- The desktop app can lock its output assembly while running. Close it before rebuilding if copy errors occur.
- The project currently focuses on TV-library workflows rather than movie-library workflows.

## License

No license file is currently included in this repository.
