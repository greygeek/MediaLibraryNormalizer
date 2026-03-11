# Epic 7: AI-Assisted Title Resolution

**Goal:** Integrate optional AI verification for fuzzy matches in the 85–92 threshold range.

**Spec references:** Advanced Spec §13; v1.1 §6, §7; Copilot Prompts §13

---

## Tasks

### 7.1 — Define `IAiResolver` interface
```csharp
Task<AiVerdict> ResolveAsync(string titleA, string titleB, AiContext context);
```
Where `AiVerdict` = `SameSeries | DifferentSeries | Uncertain`

### 7.2 — Implement `AiContext` model
Context passed to the AI:
```csharp
string TitleA
string TitleB
int FolderAFileCount
int FolderBFileCount
int? DetectedYear
```

### 7.3 — Implement OpenAI/Azure OpenAI provider
- Use the improved prompt from v1.1 §7
- Send context (file counts, detected year)
- Parse response: `SAME_SERIES`, `DIFFERENT_SERIES`, `UNCERTAIN`
- Handle API errors gracefully (timeout, rate limit)

### 7.4 — Implement `UNCERTAIN` response handling
When AI responds `UNCERTAIN`:
- Do **not** merge
- Log the pair for manual review in the report
- Include in report under "Uncertain Matches — Requires Manual Review"

### 7.5 — Make AI optional via `--ai` flag
- AI is only invoked when `--ai` is passed
- Without `--ai`, matches in the 85–92 range are logged but skipped

### 7.6 — Store AI API key configuration
- Support via environment variable: `NORMALIZER_AI_KEY`
- Support via config file field: `aiApiKey`, `aiEndpoint`, `aiModel`
- Never log API keys

### 7.7 — Write unit tests with mocked AI provider
- Test that AI is called only for 85–92 range scores
- Test `SAME_SERIES` → merge
- Test `DIFFERENT_SERIES` → skip
- Test `UNCERTAIN` → skip and log
- Test AI disabled (no `--ai` flag) → skip 85–92 range

---

## Acceptance Criteria
- [ ] AI is only invoked for borderline matches (85–92)
- [ ] AI is never invoked without `--ai` flag
- [ ] `UNCERTAIN` responses do not merge; they are logged for review
- [ ] API errors are handled gracefully without crashing the scan
