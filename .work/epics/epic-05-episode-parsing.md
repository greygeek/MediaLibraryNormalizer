# Epic 5: Episode Filename Parsing

**Goal:** Parse video filenames to extract structured episode metadata (series, season, episode, quality).

**Spec references:** Advanced Spec §5; v1.1 §4, §5; Copilot Prompts §5

---

## Tasks

### 5.1 — Implement `IEpisodeParser` interface and `EpisodeParser` class
Core method:
```csharp
EpisodeInfo? Parse(string filename);
```
Returns `null` if the filename cannot be parsed.

### 5.2 — Implement season/episode pattern matching
Supported patterns (in priority order):
1. `S01E01` — standard (also `S1E1`, `S01E100`)
2. `S01E01E02` / `S01E01-E03` / `S01E01-S01E03` — multi-episode
3. `1x01` — alternative format
4. `Season 1 Episode 1` — verbose format

Regex must handle:
- 1–2 digit seasons
- 1–4 digit episodes (for long-running shows)
- Multi-episode ranges

### 5.3 — Extract resolution metadata
Parse from filename:
- `2160p` / `4K` → 2160
- `1080p` → 1080
- `720p` → 720
- `480p` → 480

### 5.4 — Extract codec metadata
Parse from filename:
- `x265` / `h265` / `HEVC` → HEVC
- `x264` / `h264` / `AVC` → H264
- `AV1` → AV1
- `XviD` / `DivX` → legacy

### 5.5 — Extract source metadata
Parse: `WEBRip`, `WEB-DL`, `BluRay`, `HDTV`, `DVDRip`

### 5.6 — Extract year from filename
Capture 4-digit year `(19|20)\d{2}` that appears before the season/episode token.

### 5.7 — Populate `EpisodeInfo.FileSize` and `ModifiedDate`
Read from filesystem (`FileInfo`).

### 5.8 — Write unit tests for episode parsing
Test cases:
- `Show.Name.S01E01.mkv` → S1, E1
- `Show.Name.S01E01E02.mkv` → S1, E[1,2]
- `Show.Name.S01E01-E03.mkv` → S1, E[1,2,3]
- `Show Name - S01E01 - Episode Title.mkv` → S1, E1
- `Show.Name.1x01.mkv` → S1, E1
- `Show Name Season 1 Episode 1.mkv` → S1, E1
- `Show.Name.2019.S01E01.1080p.WEBRip.mkv` → S1, E1, year 2019, res 1080p
- `Show.Name.S01E100.mkv` → S1, E100

---

## Acceptance Criteria
- [ ] All documented patterns are correctly parsed
- [ ] Multi-episode files return a list of episode numbers
- [ ] Resolution, codec, source, and year are extracted when present
- [ ] Unparseable filenames return `null` (no exceptions)
