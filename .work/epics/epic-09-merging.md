# Epic 9: Folder Merge Engine

**Goal:** Merge duplicate series folders safely — move files, preserve structure, handle conflicts, and support undo.

**Spec references:** Advanced Spec §9, §10, §11; v1.1 §10, §12, §13, §14; Copilot Prompts §9, §10, §14, §15

---

## Tasks

### 9.1 — Implement `ISeriesMerger` interface and `SeriesMerger` class
Orchestrates the merge for a `SeriesGroup`:
- Determine canonical folder (from Epic 6)
- Plan move operations for all non-canonical folders
- Execute or preview (dry-run)

### 9.2 — Implement `IFileMover` interface and `FileMover` class
Single-file move with safety:
- Auto-create destination directories
- Check for existing file at destination before moving
- If duplicate exists, apply conflict resolution (best quality wins per v1.1 §4)
- Retry on failure: max 3 retries, 500 ms delay (v1.1 §13)
- Log every operation

### 9.3 — Implement associated file handling
When moving a video file, also move associated files with the same base name:
- Extensions: `.srt`, `.ssa`, `.ass`, `.sub`, `.idx`, `.nfo`, `.jpg`, `.png`
- Example: moving `E01.mkv` also moves `E01.srt`, `E01.nfo`

### 9.4 — Implement season directory preservation
- Detect season folder structure in source
- Create matching season folders in destination if missing
- Handle flat structures (no season folders) — place in root of canonical folder

### 9.5 — Implement `_UNPACK_` folder processing
- Strip `_UNPACK_` prefix (all variants)
- Normalize the extracted series name
- Merge files into existing series folder or create new one
- Delete `_UNPACK_` folder when empty

### 9.6 — Implement empty folder cleanup
- After all merges, scan for empty folders
- A folder is empty if it contains no video files and no non-empty subfolders
- Delete empty folders
- Log each deletion

### 9.7 — Implement transaction logging
- Log every file operation to JSON (v1.1 §14):
  ```json
  {"operation": "move", "source": "...", "destination": "..."}
  {"operation": "delete_folder", "path": "..."}
  {"operation": "create_directory", "path": "..."}
  {"operation": "rename", "source": "...", "destination": "..."}
  ```
- Write to `media-clean-report.json`
- All destructive operations must be logged (moves, deletes, renames)

### 9.8 — Implement `--undo` command
- Read a transaction log JSON file
- Reverse operations in reverse order:
  - `move` → move file back to source
  - `delete_folder` → recreate folder
  - `create_directory` → remove if now empty
  - `rename` → rename back
- Validate source/destination existence before each undo step

### 9.9 — Implement dry-run mode
- When `--dry-run` (default), perform all analysis but **no** filesystem changes
- Output planned operations to console and report
- Same transaction log format, marked as `"dryRun": true`

### 9.10 — Implement `--delete-samples` support
- When flag is set, delete sample files detected during scanning
- Log each deletion in transaction log

### 9.11 — Serialize file move operations
- All file moves must be serialized (not parallel) per spec
- Use a single-threaded executor or sequential loop
- Prevents race conditions on shared filesystem

### 9.12 — Write unit tests for merge engine
- Test merge of two folders with non-overlapping seasons
- Test merge with duplicate episodes (best quality wins)
- Test associated file co-movement
- Test `_UNPACK_` merge
- Test empty folder cleanup
- Test dry-run produces no filesystem changes
- Test undo reverses operations

---

## Acceptance Criteria
- [ ] Files are moved (not copied) to canonical folder
- [ ] Season directory structure is preserved
- [ ] Associated files move with their video
- [ ] Conflicts resolved by quality ranking
- [ ] Retries on transient failures (3 retries, 500 ms)
- [ ] Transaction log captures all destructive operations
- [ ] Undo reverses operations from transaction log
- [ ] Dry-run mode makes zero filesystem changes
- [ ] File moves are serialized
