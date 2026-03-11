# Epic 2: Domain Models

**Goal:** Define the core data models used throughout the application.

**Spec references:** Advanced Spec §5, §8; v1.1 §3, §4, §5; Copilot Prompts §2

---

## Tasks

### 2.1 — Create `MediaItem` model
Represents a top-level series folder in the library:
```csharp
string Path
string OriginalName
string NormalizedName
int? Year
string SeriesKey          // NormalizedName + "|" + Year (optional)
int FileCount
List<string> VideoFiles
List<string> SeasonFolders
```

### 2.2 — Create `EpisodeInfo` model
Parsed from a video filename:
```csharp
string FilePath
string SeriesName
int Season
List<int> Episodes        // supports multi-episode files (S01E01E02)
int? Year
string? Resolution        // 480p, 720p, 1080p, 2160p
string? Codec             // x264, x265, hevc, av1, etc.
string? Source            // WEBRip, BluRay, HDTV, etc.
long FileSize
DateTime ModifiedDate
```

### 2.3 — Create `SeriesGroup` model
A group of folders that represent the same show:
```csharp
string SeriesKey
string CanonicalName
MediaItem CanonicalFolder
List<MediaItem> DuplicateFolders
List<MergeOperation> PlannedOperations
```

### 2.4 — Create `NormalizedTitle` record
Result of title normalization:
```csharp
record NormalizedTitle(string Title, int? Year)
{
    string SeriesKey => Year.HasValue ? $"{Title}|{Year}" : Title;
}
```

### 2.5 — Create operation models
```csharp
record MergeOperation(string Source, string Destination, OperationType Type)
enum OperationType { Move, Delete, CreateDirectory, Rename }
```

### 2.6 — Create `ScanResult` model
Summary result of a full library scan:
```csharp
List<SeriesGroup> DuplicateGroups
List<MediaItem> UnpackFolders
List<string> EmptyFolders
int TotalFolders
int TotalFiles
```

### 2.7 — Create `NormalizerConfig` model
(Also referenced in Epic 1, Task 1.6)

---

## Acceptance Criteria
- [ ] All models are immutable or use records where appropriate
- [ ] `SeriesKey` correctly encodes year-disambiguated titles
- [ ] `EpisodeInfo.Episodes` is a list to support multi-episode files
