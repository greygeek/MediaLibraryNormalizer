# Epic 13: Desktop Hub & Series Completeness Audit

**Goal:** Extend the existing desktop application with a top-level landing page that lets users launch major workflows, including the existing merge manager and a new series completeness audit that discovers authoritative season and episode catalogs from online metadata sources, compares them with media already present on disk, and produces a per-series missing-episodes report.

**Spec references:** None yet. This epic defines the initial implementation scope for a new companion tool.

---

## Tasks

### 13.1 — Define desktop hub scope and workflow boundaries
Reshape the desktop app into a hub with multiple workflows:
- Landing page shown on launch
- Tile for Merge Manager using the current workflow
- Tile for Missing Episode Finder using the new audit workflow
- Shared shell for library path, status, recent runs, and configuration handoff

Tooling boundary for the audit engine:
- Reusable core service behind the UI
- Read-only by default: no moves, renames, or deletes
- Primary output: completeness report by series
- Input: library root plus optional series filters

Document explicit non-goals for v1:
- No downloading of media files
- No subtitle acquisition
- No episode title renaming on disk
- No scraping of HTML pages; only supported APIs or feeds

### 13.2 — Implement desktop landing page and navigation shell
Add a top-level home screen to the Avalonia app:
- Large tiles or cards for primary workflows
- Merge Manager tile opens the existing review/approval experience
- Missing Episode Finder tile opens the new audit workspace
- Reserve space for future tools without redesigning the shell

UI requirements:
- Preserve current single-window desktop deployment
- Support back navigation to the landing page
- Keep workflow-specific state isolated per tool
- Make room for future tiles such as reporting or acquisition integrations

### 13.3 — Define online catalog abstraction
Create a provider interface such as `ISeriesCatalogProvider` with deterministic models:
- `CatalogSeries`
- `CatalogSeason`
- `CatalogEpisode`
- `CatalogLookupResult`

Required fields:
- Series title
- Alternate titles if available
- First air year
- Season number
- Episode number
- Episode title
- Air date
- Episode type: standard or special
- Source identifier and source name

### 13.4 — Implement online metadata providers
Support at least one primary source and one optional fallback source using documented APIs.

Preferred candidates:
- TVMaze API for open episode listings
- TMDb TV API when credentials are configured

Implementation requirements:
- Respect provider rate limits
- Retry transient failures with backoff
- Surface provider errors without aborting the full run
- Do not store copyrighted summaries when they are not needed for matching
- Cache provider responses on disk for repeat runs

### 13.5 — Build local library inventory extractor
Reuse existing scanning and episode parsing logic where possible to create a local inventory model:
- Series title
- Detected year if present
- Season number
- Episode numbers found
- File paths contributing each match
- Unparseable files for manual review

The inventory pass must distinguish:
- Standard episodes
- Multi-episode files
- Specials when detectable
- Files that cannot be mapped to a season and episode

### 13.6 — Implement series-to-catalog matching
Match each local series folder to an online catalog series deterministically:
- Exact normalized title plus year when available
- Alternate titles and common aliases
- Configurable ambiguity thresholds
- Manual override file for known mismatches

When multiple remote candidates remain plausible:
- Mark series as ambiguous
- Skip missing-episode calculation for that series
- Include it in the report under manual review

### 13.7 — Implement completeness comparison engine
Compare local inventory against the remote catalog on a per-series basis.

Rules:
- Treat a multi-episode file as satisfying all covered episodes
- Ignore unaired future episodes by default
- Optionally include specials via flag or config
- Allow exclusion of season 0 or specials entirely
- Distinguish missing episodes from unparseable local files
- Distinguish missing metadata from provider lookup failure

Comparison output per series should include:
- Total catalog episodes considered
- Total local episodes matched
- Missing episodes grouped by season
- Extra local episodes not found in catalog
- Ambiguous or unparseable files

### 13.8 — Design Missing Episode Finder workspace
Build a dedicated desktop workflow for reviewing audit results.

Recommended layout:
- Left panel: series list with status chips such as complete, missing, ambiguous, failed
- Top summary cards: series scanned, matched, missing episodes, ambiguous series
- Main detail panel: selected series with missing episodes grouped by season
- Secondary detail area: unparseable files, extras, provider diagnostics

Interaction requirements:
- Filter to only series with missing episodes
- Search by series name
- Open export report path from the UI
- Surface manual-review cases without mixing them into confirmed missing results

### 13.9 — Design report formats
Implement report generation optimized for collection review and desktop presentation.

Console summary:
- Series scanned
- Series matched to online catalogs
- Series with missing episodes
- Series with ambiguous matches
- Provider failures

Structured output:
- JSON report for automation
- Optional CSV export flattened by missing episode row

Example JSON shape:
```json
{
  "timestamp": "...",
  "libraryPath": "...",
  "provider": "tvmaze",
  "summary": {
    "seriesScanned": 0,
    "seriesMatched": 0,
    "seriesWithMissingEpisodes": 0,
    "missingEpisodeCount": 0,
    "ambiguousSeriesCount": 0,
    "providerFailureCount": 0
  },
  "series": [
    {
      "localTitle": "Ahsoka",
      "matchedCatalogTitle": "Ahsoka",
      "year": 2023,
      "status": "missing_episodes",
      "missing": [
        { "season": 1, "episode": 4, "title": "Part Four: Fallen Jedi", "airDate": "2023-09-05" }
      ],
      "extras": [],
      "unparseableFiles": []
    }
  ],
  "errors": []
}
```

### 13.10 — Add configuration and desktop options
Support configuration for:
- Library path
- Metadata provider selection
- API credentials where required
- Cache directory and TTL
- Include or exclude specials
- Include or exclude future episodes
- Series include filter
- Output format and output path
- Offline mode using cached responses only

Desktop requirements:
- Persist audit settings independently from merge settings where needed
- Allow pre-filling audit settings from the shared library path on the landing page
- Preserve current merge-manager defaults and behavior

Optional CLI support can still exist later for automation, but desktop is the primary entry point for this epic.

### 13.11 — Implement cache and rate-limit handling
To keep the tool practical for large libraries:
- Cache successful provider lookups by provider series ID and query key
- Cache negative lookups for a short TTL
- Respect per-provider request throttling
- Serialize or bound concurrent provider requests
- Emit cache hit and miss statistics in verbose mode

### 13.12 — Manual review workflow
Add support for a user-maintained override file for cases where deterministic matching is insufficient.

Examples:
- Map local folder names to provider IDs
- Rename a season numbering scheme
- Exclude known local extras from completeness checks

The report must clearly separate:
- Confirmed missing episodes
- Ambiguous matches needing review
- Provider lookup failures
- Local parsing failures

### 13.13 — Write tests for catalog reconciliation and desktop navigation
Unit and integration coverage should include:
- Landing page navigation to Merge Manager and Missing Episode Finder
- Workflow state isolation between the two tiles
- Provider response parsing
- Cache behavior and TTL expiry
- Exact title plus year matching
- Alias matching
- Multi-episode local files satisfying multiple catalog entries
- Specials included and excluded
- Future episodes ignored by default
- Ambiguous match reporting
- Unparseable local files surfaced in report
- Provider failure for one series does not abort the whole run

### 13.14 — Document provider and licensing constraints
Create documentation covering:
- Which online providers are supported
- Required API keys and setup steps
- Rate-limit behavior
- Data retention and cache behavior
- Restrictions on redistributing provider data

---

## Acceptance Criteria
- [ ] The desktop app opens to a landing page with tiles for Merge Manager and Missing Episode Finder
- [ ] The existing merge workflow remains available from the new shell without regression
- [ ] The missing-episode workflow can scan a TV library and match local series to an online catalog source
- [ ] Missing episodes are reported per series and grouped by season
- [ ] Multi-episode files are reconciled correctly
- [ ] Ambiguous matches are surfaced explicitly instead of guessed
- [ ] Provider failures are isolated and reported without stopping the full audit
- [ ] Cached metadata reduces repeat-run network traffic
- [ ] JSON output is machine-readable and suitable for later automation
- [ ] The missing-episode workflow performs no filesystem modifications in its default mode