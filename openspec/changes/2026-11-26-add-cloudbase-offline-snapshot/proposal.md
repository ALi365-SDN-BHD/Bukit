# CloudBase offline snapshot (proposal, not implemented)

## Why

A CloudBase build always reads the table, so a site cannot rebuild without the network or a
Publishable Key, and a CI rerun can read different rows. Roadmap item F7 is the P2 of the
CloudBase content source design (section 13): caching, an offline snapshot, and `BuildTransaction`
registration (`docs/plans/2026-11-25-generic-data-derivation-roadmap.zh-CN.md`). The roadmap
leaves the design open ("工作量待设计"), so this change records a design to confirm before code.

## What Changes (proposed)

- CLI surface (open, design decision 1): add provider-neutral `--content-snapshot <dir>` /
  `--content-offline` that record and read CloudBase and Notion sources, keeping the Notion flags
  as aliases (recommended); or reuse `--notion-snapshot` / `--notion-offline` for every provider.
- Generalize `NotionSnapshotScope` into a content snapshot scope (`IsOffline` for every provider).
- Record, per CloudBase source, the projected public rows only (after field policy, before
  signing) plus the object names of file fields, under `cloudbase/<source>/rows.json` in the same
  staging directory, committed only after a successful build.
- Offline file fields resolve to the media files the recording build localized, looked up by
  object name (local names already follow the object name), never by signed link.
- Bump the snapshot `FormatVersion` (new top-level entry).

## Impact

- CLI build, config check, doctor; `NotionSnapshotSession` (or a split per provider);
  `CloudBaseContentSource`; `ImageAssetLocalizer` offline lookup; `BuildTransaction`.
- Estimated 800 to 1,500 lines with tests.
- Not in scope: a readonly/readwrite row cache with expiry, classic-mode environments.
