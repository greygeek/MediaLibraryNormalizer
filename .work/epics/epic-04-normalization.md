# Epic 4: Title Normalization

**Goal:** Implement the multi-step normalization pipeline that converts messy folder/filenames into canonical titles while preserving year metadata.

**Spec references:** Advanced Spec §4; v1.1 §3, §18; Normalization Algorithm §1–§15

---

## Tasks

### 4.1 — Implement `INameNormalizer` interface and `NameNormalizer` class
Core method signature:
```csharp
NormalizedTitle Normalize(string name);
```
Returns both the cleaned title and extracted year.

### 4.2 — Implement the normalization pipeline (12 steps)
In order:
1. Remove file extension
2. Replace separators (`.`, `_`, `-`) with spaces
3. Remove bracket tags `[...]`
4. Remove release group tags (only after known tags, to avoid mangling hyphenated titles)
5. Remove resolution tags: `480p`, `576p`, `720p`, `1080p`, `2160p`, `4K`, `HDR`
6. Remove codec tags: `x264`, `x265`, `h264`, `h265`, `HEVC`, `AVC`, `XviD`, `DivX`, `VP9`, `AV1`
7. Remove source tags: `WEBRip`, `WEB-DL`, `BluRay`, `HDTV`, `DVDRip`
8. Remove season/episode tokens: `S01E01`, `S01E01E02`, `1x01`, `Season N Episode N`
9. **Extract year** into metadata (capture `(19|20)\d{2}` match)
10. Remove year tokens from the string
11. Collapse whitespace and trim
12. Title-case result

### 4.3 — Implement folder-name vs filename normalization
- Folder names: skip extension removal and season/episode token removal
- Filenames: full pipeline
- Both paths share the same core logic with mode flag

### 4.4 — Handle illegal Windows characters
- Remove or replace: `< > : " / \ | ? *` and ASCII control characters (0–31)
- Applied as part of normalization

### 4.5 — Implement `SeriesKey` generation
- `SeriesKey = NormalizedTitle + "|" + Year` (year optional)
- Used as the grouping key for duplicate detection
- Case-insensitive comparison

### 4.6 — Build encoding/resolution/source tag removal regex patterns
- Must be case-insensitive
- Must use word boundaries (`\b`) to avoid partial matches
- All tag lists sourced from v1.1 §18

### 4.7 — Write unit tests for normalization
Test cases:
- `Broadchurch.` → `Broadchurch`
- `Broadchurch [x265]` → `Broadchurch`
- `Doctor Who (2005)` → title: `Doctor Who`, year: `2005`
- `Show.Name.S01E01.1080p.WEBRip.x264-GROUP` → `Show Name`
- `The.Mandalorian.2019.S02E05.2160p.HDR.WEB-DL.x265` → title: `The Mandalorian`, year: `2019`
- `Spider-Man` → `Spider-Man` (hyphen preserved, not treated as release group)
- `NCIS` → `NCIS` (acronym preserved — verify casing behavior)

---

## Acceptance Criteria
- [ ] Year is extracted before removal, available in `NormalizedTitle.Year`
- [ ] Folder-name mode does not strip season/episode tokens
- [ ] Hyphenated legitimate titles are not mangled by release-group removal
- [ ] All tag lists from v1.1 §18 are covered
- [ ] Illegal Windows characters are removed
