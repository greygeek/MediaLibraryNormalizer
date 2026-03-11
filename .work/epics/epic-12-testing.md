# Epic 12: Testing & Quality Assurance

**Goal:** Establish comprehensive test coverage across unit, integration, and performance tiers.

**Spec references:** v1.1 §20; Copilot Prompts §21

---

## Tasks

### 12.1 — Create test dataset generator
Build a utility that creates mock media libraries with:
- Duplicate series folders (with varying names)
- Multiple encoding variants
- `_UNPACK_` folders
- Empty folders
- Sample files
- Multi-episode files
- Year-disambiguated series (e.g., Doctor Who 1963 vs 2005)
- Configurable scale (small for unit tests, large for perf tests)

### 12.2 — Unit tests: NameNormalizer
See Epic 4 Task 4.7 for test cases.

### 12.3 — Unit tests: EpisodeParser
See Epic 5 Task 5.8 for test cases.

### 12.4 — Unit tests: SeriesMatcher
See Epic 6 Task 6.6 for test cases.

### 12.5 — Unit tests: DuplicateDetector
- Test resolution ranking
- Test codec ranking
- Test multi-episode beats individual
- Test identical episodes resolved by size → date

### 12.6 — Unit tests: MediaHasher
See Epic 8 Task 8.6 for test cases.

### 12.7 — Integration tests: full pipeline
See Epic 11 Task 11.5 for details.
- Dry-run correctness
- Merge correctness
- Undo correctness

### 12.8 — Performance tests
Simulated library:
- 100k folders, 500k files (empty stubs)
- Measure: scan time, normalization time, matching time
- Memory profiling: ensure bounded memory under large loads

### 12.9 — Edge case tests
- Folder names with only illegal characters
- Files with no parseable episode pattern
- Series with only sample files
- Deeply nested season structures
- Network path handling (UNC paths)
- Unicode characters in folder/file names

---

## Acceptance Criteria
- [ ] All unit test suites pass
- [ ] Integration tests verify end-to-end correctness
- [ ] Performance test completes 100k folder scan in reasonable time
- [ ] Edge cases are handled without exceptions
