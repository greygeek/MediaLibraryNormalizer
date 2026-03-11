# Epic 11: End-to-End Pipeline

**Goal:** Wire all modules into the main processing pipeline in `Program.cs`.

**Spec references:** Advanced Spec §20, §21; Copilot Prompts §20

---

## Tasks

### 11.1 — Implement main pipeline orchestration
In `Program.cs`, the pipeline executes these steps in order:

1. Parse CLI arguments
2. Load configuration
3. Configure DI container
4. **Scan** library → `List<MediaItem>`
5. **Normalize** names → populate `SeriesKey` on each item
6. **Detect** `_UNPACK_` folders → add to processing queue
7. **Match** duplicate series → `List<SeriesGroup>` (exact + fuzzy + optional AI)
8. **Select** canonical folders
9. **Detect** duplicate episodes within groups
10. **Plan** merge operations
11. **Execute** or **preview** (dry-run vs merge mode)
12. **Clean** empty folders
13. **Generate** report and transaction log

### 11.2 — Implement progress reporting
- Use `Spectre.Console` progress bars / status spinners
- Show current phase (Scanning... Matching... Merging...)
- Show file count progress during scanning and merging

### 11.3 — Implement `--verbose` mode
When enabled:
- Log each folder as it's scanned
- Log each fuzzy match comparison and score
- Log each file operation
- Log AI requests and responses

### 11.4 — Implement graceful cancellation
- Support `Ctrl+C` cancellation
- Complete current operation, then stop
- Write partial transaction log so operations already done can be undone

### 11.5 — Write integration test for full pipeline
- Create a temporary directory with test data (duplicate folders, `_UNPACK_`, empty folders)
- Run pipeline in dry-run mode → verify no changes, report correct
- Run pipeline in merge mode → verify filesystem state matches expected result
- Run undo → verify filesystem returned to original state

---

## Acceptance Criteria
- [ ] Pipeline executes all steps in correct order
- [ ] Dry-run produces a report with no filesystem changes
- [ ] Merge mode produces correct filesystem + transaction log
- [ ] Progress is visible during long operations
- [ ] `Ctrl+C` is handled gracefully
