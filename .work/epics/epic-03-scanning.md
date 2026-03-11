# Epic 3: Library Scanner

**Goal:** Implement high-performance directory scanning that works reliably on 100k+ folder libraries.

**Spec references:** Advanced Spec §2, §6, §16, §17; v1.1 §9, §10, §11; Copilot Prompts §3, §6

---

## Tasks

### 3.1 — Implement `ILibraryScanner` interface and `LibraryScanner` class
- Accept a root path
- Use `Directory.EnumerateDirectories()` (streaming, not `GetDirectories`)
- Return `IEnumerable<MediaItem>` (lazy)
- Scan top-level directories only for series folders

### 3.2 — Implement `IMediaFileDetector` and `MediaFileDetector` class
- Supported video extensions: `.mkv`, `.mp4`, `.avi`, `.m4v`, `.mov`, `.ts`
- Associated file extensions (move with video): `.srt`, `.ssa`, `.ass`, `.sub`, `.idx`, `.nfo`, `.jpg`, `.png`
- Ignore sample files: filenames containing `.sample.`, `-sample`, `sample-`
- Use `Directory.EnumerateFiles()` for streaming enumeration

### 3.3 — Implement `_UNPACK_` folder detection
- Detect folders matching: `_UNPACK_ShowName`, `_UNPACK_ ShowName`, `_UNPACK_/ShowName`
- Flag these in scan results as `UnpackFolders`
- Strip `_UNPACK_` prefix to extract series name

### 3.4 — Implement empty folder detection
- A folder is empty if it contains no video files AND no non-empty subfolders
- Recursively check subfolder emptiness
- Return list of empty folder paths

### 3.5 — Add parallel scanning support
- Use `Parallel.ForEach` with `MaxDegreeOfParallelism = min(8, CPU cores)`
- Safe to parallelize: directory scanning, file enumeration, metadata extraction
- Use thread-safe collections (`ConcurrentBag<T>` or similar)

### 3.6 — Implement season folder discovery
- Within each series folder, detect `Season N` subfolders
- Also detect flat structures (episodes directly in series folder)
- Populate `MediaItem.SeasonFolders`

---

## Acceptance Criteria
- [ ] Scanner uses streaming enumeration (no `GetFiles(recursive)`)
- [ ] Memory usage stays bounded for 100k+ folder scans
- [ ] Sample files are excluded from video file counts
- [ ] `_UNPACK_` folders are correctly identified and series name extracted
- [ ] Empty folders are correctly detected (recursive check)
