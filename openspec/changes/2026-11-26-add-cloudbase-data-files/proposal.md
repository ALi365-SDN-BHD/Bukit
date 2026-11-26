# Localize CloudBase files in data mode

## Why

Media localization handles CloudBase `fileFields` only in content mode, and `mode: data` drops every
field holding `cloud://`. Gallery photos, advisor portraits and review photos are data rows without
pages, so their images are empty links. Roadmap item F11
(`docs/plans/2026-11-25-generic-data-derivation-roadmap.zh-CN.md`).

## What Changes

- `mode: data` CloudBase sources accept `fileFields`, with the same D8 preconditions.
- Their file values are signed like content mode; the existing media localization stage downloads
  them, naming each file after the object (signature parameters removed), so names are stable.
- Other `cloud://` fields are still dropped with `BKT-0C0B`.

## Impact

- Config: the data-mode rejection in `ProviderValidators` is removed.
- Content: `CloudBaseContentSource` resolves file fields in data mode.
- Docs: `27-cloudbase-content.md`, the `bukit-content-model` skill, CHANGELOG in three languages.
- Out of scope: arrays of file values (a file field still holds one object name), and generating
  responsive sizes for data fields (sites use `theme.images`).
