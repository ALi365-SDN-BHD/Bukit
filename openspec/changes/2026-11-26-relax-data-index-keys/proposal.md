# Relaxed dataIndex keys

## Why

All 247 travelWeb copy keys are dotted camelCase (`custom.daysHint`, `custom.days.4-5.label`,
`custom.days.10+.label`) and fail `^[a-z][a-z0-9_]*$`. Roadmap item F10, option (a)
(`docs/plans/2026-11-25-generic-data-derivation-roadmap.zh-CN.md`).

## What Changes

- `dataIndex` keys accept `^[A-Za-z0-9][A-Za-z0-9._+-]*$`. Source names, scopes and field names
  keep `^[a-z][a-z0-9_]*$`.
- `requiredKeys` split at the first dot: scope, then key.
- Rows still carry their own scope, so there is no scope-splitting rule for flat keys; a site picks
  one scope (for example `copy`) for them.

## Impact

- Config: `DataIndexKey`, `ConfigValidator` requiredKeys check. Engine: `DataModuleBuilder`.
- Route metadata and mini program datasets keep their own rules (unchanged).
- No public API change. Docs: `04`, `07`, the `bukit-content` skill, CHANGELOG.
