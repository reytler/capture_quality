# AGENTS.md - Agentic Coding Guidelines for capture_quality

> **Multi-Agent Architecture**: This repository uses a multi-agent workflow under `.opencode/agents/`.
> See sections below for routing specs and policies.

## Evidence-First Policy (Required)
- Only claim a stack, command, or convention when you can cite evidence from the repo (file path).
- If evidence is missing, label as `Candidate` and include how to verify.

## Token-Minimization Policy (Required)
- Prefer references over repetition: use file paths and small excerpts.
- Do not paste large files; quote only what justifies a decision.

## Verification Policy (Required)
Before finalizing any change proposal:
- Verify referenced paths exist (`src/CaptureQuality/`).
- Verify commands exist or are clearly marked as `Candidate`.
- Flag non-ASCII characters in newly written/updated files (file + line).

## Project Overview

- **Primary stack**: .NET 8.0 Blazor WebAssembly (detected via `src/CaptureQuality/CaptureQuality.csproj:1-4`)
- **Secondary stacks**: None

## Repository Layout (Observed)

```
capture_quality/
├── src/CaptureQuality/
│   ├── Services/           # Blur detection pipeline
│   ├── Components/         # Razor UI components
│   ├── Pages/              # Route pages
│   ├── Layouts/            # Layouts
│   ├── Program.cs          # DI registration
│   └── App.razor           # Root component
├── docs/                   # Methodology docs
├── Directory.Build.props   # Shared MSBuild properties
└── CaptureQuality.slnx      # Solution file
```

## Services Architecture (Observed)

Evidence: `src/CaptureQuality/Services/*.cs` and `src/CaptureQuality/Program.cs:11-14`

```
ConfigurationService         # Defaults: K=1, PatchSize=27, PatchThreshold=0.64, BlurRatioThreshold=0.35
    │
    ├── ImageProcessorService   # Grayscale, MedianFilter, K-means, Features
    │
    ├── SvdAnalyzerService      # SVD, Bk calculation, Global blur ratio
    │
    └── BlurDetectorService     # Orchestrator (load -> resize -> grayscale -> features -> segment -> patches -> result)
```

## Commands (Observed / Preferred)

Run from repo root unless noted.

### Install/Restore
- `dotnet restore` or implicit (build triggers restore)
  Evidence: `CaptureQuality.slnx` exists (solution file)

### Build/Compile
- `dotnet build src/CaptureQuality/CaptureQuality.csproj`
  Evidence: `src/CaptureQuality/CaptureQuality.csproj:1`

### Lint/Static Analysis
- **Not configured** - Candidate: `dotnet format --verify-no-changes` (requires dotnet format global tool)
  Evidence: No `.editorconfig` or style rule files detected

### Format
- **Check**: `dotnet format --verify-no-changes src/CaptureQuality/CaptureQuality.csproj` (Candidate)
- **Apply**: `dotnet format src/CaptureQuality/CaptureQuality.csproj` (Candidate)

### Test
- **Suite**: No test project exists (Candidate: create with `dotnet new xunit -o tests`)
- **Single test** (if supported): `dotnet test --filter "FullyQualifiedName~MethodName"`

### Run/Dev/Watch
- `dotnet run --project src/CaptureQuality/CaptureQuality.csproj`
  Evidence: Blazor WASM uses `dotnet run` for dev server

### Publish
- `dotnet publish src/CaptureQuality/CaptureQuality.csproj -c Release`
  Evidence: `AGENTS.md:31-34`

## Code Style (Repository-Specific)

Evidence: `src/CaptureQuality/Services/*.cs`, `Directory.Build.props`

### File Organization
- **File-scoped namespaces**: `namespace CaptureQuality.Services;`
- One public class per file, named same as file

### Naming
| Element | Convention | Example |
|---------|------------|---------|
| Classes | PascalCase | `BlurDetectorService` |
| Public Methods | PascalCase | `DetectBlurAsync` |
| Private Fields | `_camelCase` | `_config` |
| Parameters | camelCase | `imageStream` |

### Async
- Use `Async` suffix for async methods
- Use `await` rather than blocking
- Configure `ConfigureAwait(false)` for library code (optional)

### Nullable
- Nullable reference types enabled (csproj:5)
- Use `?` for nullable types

## Blur Detection Pipeline (Observed)

Evidence: `src/CaptureQuality/Services/BlurDetectorService.cs:23-127`

1. Load image (ImageSharp) - line 25
2. Resize if needed (Config.MaxImageDimension) - lines 27-42
3. Convert to grayscale - line 44
4. Apply median filter - line 46
5. Extract features (intensity, local median, gradient) - line 48
6. Segment with K-means (foreground/background) - line 50
7. Filter foreground patches - lines 52-106
8. Analyze patches with SVD - line 78
9. Calculate global blur ratio - lines 110-111

## SVD Algorithm Parameters (Observed)

Evidence: `src/CaptureQuality/Services/ConfigurationService.cs` and `docs/regras.md:71-89`

| Parameter | Value | Description |
|-----------|-------|-------------|
| K | 1 | Number of singular values to consider |
| PatchSize | 27 | Patch grid size |
| PatchThreshold | 0.64 | Bk threshold for blurred patch |
| BlurRatioThreshold | 0.35 | Global threshold for image acceptance |
| MedianFilterSize | 31 | Lighting bias correction window |

Key formula: `Bk = sum of first k singular values^2 / sum of all singular values^2`

## Architecture Boundaries

- **Services** (`Services/`): Pure logic, no UI
- **Components** (`Components/`): UI only, inject services via `[Inject]`
- **Program.cs**: DI registration only, no business logic

### What belongs where

| Task | Location |
|------|----------|
| Add new blur detection method | `SvdAnalyzerService.cs` |
| Add new image preprocessing | `ImageProcessorService.cs` |
| Add new UI component | `Components/` |
| Change default parameters | `ConfigurationService.cs` |
| Register new service | `Program.cs:11-14` |

## Hygiene
- Do not commit local artifacts (editor/build outputs, `obj/`, `bin/`).
- Do not add secrets to repo.
- Do not modify product code. Only touch:
  - `AGENTS.md`
  - `.opencode/agents/*.md`

## Multi-Agent Routing

When using the multi-agent system, follow the routing specs in orchestrators:

- **Entry**: `.opencode/agents/analysis.md` - for understanding/exploring code
- **Entry**: `.opencode/agents/implement.md` - for implementing features/fixes

Orchestrators delegate to subagents based on task type.