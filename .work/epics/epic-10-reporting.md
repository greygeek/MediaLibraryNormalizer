# Epic 10: Reporting System

**Goal:** Generate structured reports of scan results, planned operations, and completed operations.

**Spec references:** Advanced Spec §19, §21; v1.1 §14; Copilot Prompts §16

---

## Tasks

### 10.1 — Implement `IReportGenerator` interface and `ReportGenerator` class
Generate reports in multiple formats:
- Console summary (always)
- JSON report (`media-clean-report.json`)

### 10.2 — Console summary output
Using `Spectre.Console` for rich formatting:
- Total folders scanned
- Duplicate series groups found
- Files moved / to be moved (dry-run)
- Duplicate episodes skipped
- Empty folders removed / to be removed
- `_UNPACK_` folders processed
- Uncertain AI matches requiring manual review

### 10.3 — JSON report output
Full structured report including:
```json
{
  "timestamp": "...",
  "libraryPath": "...",
  "dryRun": true,
  "summary": {
    "totalFolders": 0,
    "totalFiles": 0,
    "duplicateGroups": 0,
    "filesMoved": 0,
    "duplicatesSkipped": 0,
    "foldersRemoved": 0,
    "unpackFoldersProcessed": 0
  },
  "duplicateGroups": [...],
  "operations": [...],
  "uncertainMatches": [...],
  "errors": [...]
}
```

### 10.4 — Include uncertain matches in report
Pairs where fuzzy score was 85–92 and AI returned `UNCERTAIN` or AI was not enabled:
- Both folder names
- Fuzzy score
- File counts
- Detected years

### 10.5 — Include errors in report
Log all non-fatal errors:
- Permission denied
- Locked files (after retries exhausted)
- Network failures

### 10.6 — Write tests for report generation
- Test console output format
- Test JSON report structure
- Test report includes all operation types

---

## Acceptance Criteria
- [ ] Console shows clear, colorized summary
- [ ] JSON report is complete and machine-parseable
- [ ] Uncertain matches are surfaced for manual review
- [ ] Errors are captured without crashing the scan
