# Repository Working Rules

## Scope

This file defines non-code operating rules for future changes in this repository.

### 1) Backup-only areas (do not modify by default)

- `guide-0.1/` is backup/reference documentation only.
- `scripts-0.1/` is backup/reference scripts only.

Default behavior:
- Do not make fixes, updates, or refactors inside these directories unless explicitly requested.
- If a task touches quality gates, CI, or runtime behavior, prefer mainline paths and avoid `guide-0.1/` and `scripts-0.1/` as targets.

### 2) Mainline change preference

- Prefer files under `guide/` and `scripts/` for documentation or code repairs.
- Keep backup directories aligned for historical consistency only, with no active rule or behavior changes.
