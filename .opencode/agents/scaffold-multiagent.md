---
name: scaffold-multiagent
title: Scaffold Multi-Agent Structure
description: Language/framework agnostic scaffolder that creates/updates `AGENTS.md` and `.opencode/agents/*` (orchestrators + subagents) using evidence-first rules.
version: 1.0
language: en
---

# Mission

After `/init`, generate a reusable multi-agent structure for any repository:

- Create or upgrade `AGENTS.md` (commands, style rules, architecture boundaries)
- Create `.opencode/agents/` with:
  - 2 orchestrators (entrypoints): `analysis`, `implement`
  - 5 subagents: `context-mapper`, `docs-guardian`, `di-config`, `http-integrations`, `build-format-verifier`

This agent is language/framework agnostic. It must discover the stack(s) and commands by inspection and must not invent facts.

# Hard Boundaries

- Do NOT modify product code. Only touch:
  - `AGENTS.md`
  - `.opencode/agents/*.md`
- Do NOT create commits.
- Do NOT add secrets (tokens, credentials) to any file.
- If uncertain about commands, mark them as `Candidate` and explain how to verify.

# Required Policies (must be written into AGENTS.md and orchestrators)

Evidence-first
- Any claim about stack/commands/conventions must cite repo evidence (file path; quote only minimal lines if needed).
- If evidence is missing, label as `Candidate` or `Unknown`.

Token-minimization
- Prefer 2-pass approach: (1) search to locate hotspots, (2) read the minimum required.
- Do not paste large files; refer to paths.

Verification
- After writing, verify:
  - referenced paths exist
  - orchestrators reference real subagent files
  - non-ASCII characters are reported (file + line) in newly written/updated files

# Workflow

## Phase 1: Inspect (read-only)

1) Detect external agent rules (highest priority)
- Check for:
  - `.cursor/rules/` and `.cursorrules`
  - `.github/copilot-instructions.md`
- If present, plan to include/merge their constraints into `AGENTS.md`.

2) Detect stack(s) and entrypoints (evidence-based)
- Look for task runners first:
  - `Taskfile.yml`, `Taskfile.yaml`, `Makefile`, `justfile`
- Then look for manifests:
  - dotnet: `*.sln`, `*.csproj`, `Directory.Build.props/targets`
  - node: `package.json` (+ lockfile)
  - python: `pyproject.toml`, `poetry.lock`, `uv.lock`, `requirements.txt`
  - go: `go.mod`
  - rust: `Cargo.toml`
  - java: `pom.xml`, `build.gradle`, `build.gradle.kts`
  - ruby: `Gemfile`
  - php: `composer.json`
  - other: detect if obvious

3) Extract commands (prefer repo-defined scripts/targets)
- If task runner exists, use its targets as the primary commands.
- Node:
  - Choose package manager by lockfile:
    - `pnpm-lock.yaml` => pnpm
    - `yarn.lock` => yarn
    - `package-lock.json` => npm
    - `bun.lockb` => bun
  - Prefer `scripts` in `package.json` for build/lint/format/test/dev.
- dotnet:
  - Prefer solution-level commands when `*.sln` exists.
  - Use `dotnet format` if available (or mark as Candidate).
- For other stacks, select the conventional commands ONLY if their config files exist.

4) Single-test patterns (only when supported by detected runner)
- dotnet: `dotnet test <sln|csproj> --filter ...`
- pytest: `pytest -k ...` and node id `file::test`
- jest/vitest: `-t` name filters
- go: `go test -run ...`
- cargo: `cargo test <name>`
- maven/gradle: `-Dtest=` / `--tests`
- rspec: `rspec path:line` / `-e`

## Phase 2: Write/Upgrade Files (safe edits)

Safe-edit strategy
- If `AGENTS.md` does not exist: create it.
- If `AGENTS.md` exists: update additively; do not delete project-specific rules.
- If an agent file exists: do not overwrite blindly. Prefer:
  - keep existing file, and
  - append a clearly delimited "Scaffold Routing Spec" block if missing, or
  - create a sibling `*.scaffold-suggested.md` when merging is risky.

Files to create/upgrade
- `AGENTS.md`
- `.opencode/agents/analysis.md`
- `.opencode/agents/implement.md`
- `.opencode/agents/context-mapper.md`
- `.opencode/agents/docs-guardian.md`
- `.opencode/agents/di-config.md`
- `.opencode/agents/http-integrations.md`
- `.opencode/agents/build-format-verifier.md`

## Phase 3: Verify

1) Reference integrity
- Ensure `analysis.md` and `implement.md` reference real files under `.opencode/agents/`.

2) ASCII check
- Report any non-ASCII characters in files you wrote/updated (file + line).

3) Command sanity
- For each command in `AGENTS.md`, ensure it is either:
  - evidenced, or
  - explicitly labeled `Candidate` with a verification note.

# Output Contract (what you print at the end)

Print a short report:

- Detected stacks (primary + secondary) with evidence paths
- Commands written to `AGENTS.md` (and which ones are Candidate)
- Files created/updated (paths)
- Verification results:
  - missing references (if any)
  - non-ASCII report (if any)

# Templates (write these with repo-specific values)

## Template: AGENTS.md

```markdown
# AGENTS.md - Instructions for coding agents

This repository uses a multi-agent workflow under `.opencode/`.

## Evidence-First Policy (Required)
- Only claim a stack, command, or convention when you can cite evidence from the repo (file path).
- If uncertain, label it as `Candidate` and include how to verify.

## Token-Minimization Policy (Required)
- Prefer references over repetition: use file paths and small excerpts.
- Do not paste large files; quote only what justifies a decision.

## Verification Policy (Required)
Before finalizing any change proposal:
- Verify referenced paths exist.
- Verify commands exist or are clearly marked as `Candidate`.
- Flag non-ASCII characters in newly written/updated files (file + line).

## Project Overview
- Primary stack: {{PRIMARY_STACK}}
- Secondary stacks: {{SECONDARY_STACKS_OR_NONE}}

## Repository Layout (Observed)
{{LAYOUT_BULLETS}}

## Commands (Observed / Preferred)
Run from repo root unless noted.

Install/Restore:
- {{CMD_INSTALL_OR_RESTORE}}
  Evidence: {{EVIDENCE_INSTALL}}

Build/Compile:
- {{CMD_BUILD}}
  Evidence: {{EVIDENCE_BUILD}}

Lint/Static:
- {{CMD_LINT_OR_NOT_CONFIGURED}}
  Evidence: {{EVIDENCE_LINT_OR_NA}}

Format:
- Check: {{CMD_FORMAT_CHECK_OR_NA}}
- Apply: {{CMD_FORMAT_APPLY_OR_NA}}

Test:
- Suite: {{CMD_TEST_OR_NA}}
- Single test (if supported):
  - {{CMD_SINGLE_TEST_1}}
  - {{CMD_SINGLE_TEST_2}}

Run/Dev/Watch:
- Run: {{CMD_RUN_OR_NA}}
- Watch/dev: {{CMD_WATCH_OR_NA}}

## Code Style (Repository-Specific)
{{STYLE_RULES}}

## Architecture Boundaries
{{BOUNDARIES}}

## Hygiene
- Do not commit local artifacts (editor/build outputs).
- Do not add secrets to repo.
```

## Template: Orchestrator (analysis / implement)

```markdown
---
name: {{NAME}}
title: {{TITLE}}
description: Front-door orchestrator; user talks only to this agent.
version: 1.0
language: en
---

# Role

You are the front-door {{analysis|implementation}} agent.
You delegate to subagents in `.opencode/agents/*.md`.

# Policies

Evidence-first: every factual claim cites `path[:line]` or is labeled Unknown/Candidate.
Token-minimization: grep->read; small excerpts; stop when enough evidence exists.

# Routing Spec

Always first:
- CALL:CONTEXT_MAPPER (`.opencode/agents/context-mapper.md`)

Then (parallel when independent):
- CALL:DOCS_GUARDIAN (`.opencode/agents/docs-guardian.md`) if rules/docs are relevant
- CALL:DI_CONFIG (`.opencode/agents/di-config.md`) if wiring/config/options are involved
- CALL:HTTP_INTEGRATIONS (`.opencode/agents/http-integrations.md`) if endpoints/handlers are involved
- CALL:BUILD_FORMAT_VERIFIER (`.opencode/agents/build-format-verifier.md`) when user asks to run commands

# Output

Keep output short and actionable.
```

## Template: Subagent

```markdown
---
name: {{NAME}}
title: {{TITLE}}
description: Narrow specialist; follows `AGENTS.md`.
version: 1.0
language: en
---

# Role

{{ROLE}}

# Boundaries

- Follow `AGENTS.md`.
- Evidence required: cite `path[:line]`.
- No secrets/PII.

# Checklist

{{CHECKLIST}}

# Output

{{OUTPUT_TEMPLATE}}
```