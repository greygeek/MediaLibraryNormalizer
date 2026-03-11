# Epic 8: File Hashing Engine

**Goal:** Implement fast partial-file hashing for duplicate file detection on multi-TB libraries.

**Spec references:** Advanced Spec §7; v1.1 §8; Copilot Prompts §11, §12

---

## Tasks

### 8.1 — Implement `IMediaHasher` interface and `MediaHasher` class
Core method:
```csharp
Task<string> ComputeHashAsync(string filePath);
```

### 8.2 — Implement partial hashing strategy
Per v1.1 §8:
- Read and hash the **first 1 MB** of the file
- Read and hash the **last 1 MB** of the file
- Combine both partial hashes with the **file size**
- Use **xxHash64** algorithm (`System.IO.Hashing`)
- For files smaller than 2 MB, hash the entire file

### 8.3 — Implement composite hash key
Format: `{fileSize}:{firstMBHash}:{lastMBHash}`
- Used as the dedup key alongside parsed episode metadata

### 8.4 — Add parallel hash calculation
- Hashing is safe to parallelize
- Use `Parallel.ForEachAsync` with `MaxDegreeOfParallelism` from config
- Thread-safe result collection

### 8.5 — Gate hashing behind `--hash` flag
- Hashing is opt-in (can be expensive on network storage)
- Without `--hash`, duplicate detection relies on metadata + file size only

### 8.6 — Write unit tests for hashing
- Test partial hash on known files
- Test small file (< 2 MB) full hash
- Test that identical files produce identical hashes
- Test that different files produce different hashes

---

## Acceptance Criteria
- [ ] Partial hashing reads only 2 MB regardless of file size
- [ ] xxHash64 is used (not SHA1 or MD5)
- [ ] Hashing is only performed when `--hash` is specified
- [ ] Parallel hashing respects concurrency limits
