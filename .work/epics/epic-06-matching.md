# Epic 6: Series Matching & Duplicate Detection

**Goal:** Group duplicate series folders using normalized titles, fuzzy matching, and optional AI verification.

**Spec references:** Advanced Spec §3, §7, §12; v1.1 §6, §8; Normalization Algorithm §16–§18; Copilot Prompts §7, §8

---

## Tasks

### 6.1 — Implement `ISeriesMatcher` interface and `SeriesMatcher` class
- Accept list of `MediaItem` (from scanner)
- Group by `SeriesKey` (exact match first)
- Return `List<SeriesGroup>`

### 6.2 — Implement fuzzy matching for non-exact matches
- After exact-key grouping, run FuzzySharp on remaining unmatched items
- Thresholds (from v1.1 §6):
  - `> 92` → auto-merge into group
  - `85–92` → flag for AI verification
  - `< 85` → do not merge
- Thresholds configurable via `normalizer.config.json`

### 6.3 — Implement token-based pre-filtering
Before fuzzy matching:
- Tokenize normalized titles (split on space)
- Compare token overlap
- Only run fuzzy match if tokens share significant overlap
- Dramatically reduces unnecessary comparisons for large libraries

### 6.4 — Implement canonical folder selection
For each `SeriesGroup`, select the canonical folder by priority:
1. Folder with most video files
2. Folder without encoding/resolution tags in its original name
3. Folder whose original name matches the normalized title
4. Alphabetically first

### 6.5 — Implement `IDuplicateDetector` and `DuplicateDetector` class
Detect duplicate **episodes** across folders:
- Compare parsed `EpisodeInfo` (season + episode number)
- When duplicates found, select best version per v1.1 §4:
  1. Higher resolution
  2. Better codec
  3. Larger file size
  4. Newer modification date
- Multi-episode file beats individual episode files (v1.1 §5)

### 6.6 — Write unit tests for matching
Test cases:
- Exact match: `Broadchurch`, `Broadchurch.`, `Broadchurch ` → same group
- Fuzzy match: `Marvels Jessica Jones` vs `Jessica Jones` → borderline (score check)
- Year disambiguation: `Doctor Who (1963)` vs `Doctor Who (2005)` → different groups
- Canonical selection with varying file counts

---

## Acceptance Criteria
- [ ] Exact-key grouping is fast and correct
- [ ] Fuzzy matching uses configurable thresholds
- [ ] Token pre-filtering reduces comparisons
- [ ] Canonical folder selection follows the 4-tier priority
- [ ] Duplicate episodes are resolved to best quality version
