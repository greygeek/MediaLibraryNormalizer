# Epic 1: Project Setup & Foundation

**Goal:** Scaffold the .NET 10 console application with folder structure, DI, CLI framework, configuration, and build pipeline.

**Spec references:** Advanced Spec §15, §18; v1.1 §16, §17, §19; Copilot Prompts §1

---

## Tasks

### 1.1 — Initialize .NET 10 console project
- `dotnet new console -n MediaLibraryNormalizer`
- Target `net10.0` in the `.csproj`
- Verify build and run

### 1.2 — Create project folder structure
Create the following folders with placeholder classes:
```
MediaLibraryNormalizer/
├─ Config/
├─ Scanner/
├─ Parser/
├─ Normalization/
├─ Matching/
├─ Merging/
├─ Hashing/
├─ AI/
├─ Reporting/
└─ Models/
```

### 1.3 — Add NuGet dependencies
Install required packages:
- `System.CommandLine` (CLI parsing)
- `Spectre.Console` (rich console output)
- `FuzzySharp` (fuzzy string matching)
- `System.IO.Hashing` (xxHash64)
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Logging`
- `System.Text.Json` (built-in, for config/reporting)

### 1.4 — Configure dependency injection
- Create `ServiceCollectionExtensions` to register all services
- Wire up in `Program.cs` using `Microsoft.Extensions.DependencyInjection`
- All modules should depend on interfaces, not concrete types

### 1.5 — Implement CLI argument parsing
Using `System.CommandLine`, create:
- Root command accepting a path argument (required)
- Options: `--dry-run` (default), `--merge`, `--ai`, `--hash`, `--verbose`, `--delete-samples`, `--undo <file>`
- Default behavior with no flags = `--dry-run`

### 1.6 — Implement configuration file loading
- Config file: `normalizer.config.json`
- Model: `NormalizerConfig` with properties:
  - `FuzzyThreshold` (default: 92)
  - `AiThreshold` (default: 85)
  - `HashSizeMB` (default: 1)
  - `MaxConcurrency` (default: min(8, CPU cores))
  - `DeleteSamples` (default: false)
- CLI args override config file values

### 1.7 — Set up logging infrastructure
- Console logger (always)
- JSON structured log writer (for transaction log/report)
- `--verbose` flag increases console log detail level

### 1.8 — Create solution and test project
- `dotnet new xunit -n MediaLibraryNormalizer.Tests`
- Add project reference
- Verify test discovery works

---

## Acceptance Criteria
- [ ] `dotnet build` succeeds with zero warnings
- [ ] `dotnet run -- --help` shows CLI usage
- [ ] Running without flags defaults to dry-run
- [ ] Config file is loaded when present, defaults used when absent
- [ ] DI container resolves all registered services
