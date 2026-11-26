# CloudBase offline snapshot

## Why

A CloudBase build always reads the table, so a site cannot rebuild without the network or a
Publishable Key, and a CI rerun can read different rows. Roadmap item F7 is the P2 of the
CloudBase content source design (section 13): an offline snapshot recorded by a formal build
(`docs/plans/2026-11-25-generic-data-derivation-roadmap.zh-CN.md`).

## What Changes

- Provider-neutral `--content-snapshot <dir>` / `--content-offline` on `build`, `config check`
  and `doctor`; `--notion-snapshot` / `--notion-offline` stay as aliases (decision 1).
- The Notion snapshot directory also holds CloudBase entries at
  `cloudbase/<table key>/<shape key>/<filter key>.json`: the projected public rows only and, per signed file
  object, its media identity (the signed link without its token) (decisions 2 and 3).
- Offline, CloudBase sources replay the projection from the recorded rows and resolve file objects
  to the recorded identities, so media localization reuses the local media cache; no key and no
  network.
- The shape key covers the source settings that decide the recorded rows and columns, so two
  sources on one table keep separate entries; the filter key covers the resolved filter value, so
  an offline build for another release or a changed source fails with `BKT-0B13` (decision 4). A table the snapshot lacks fails with `BKT-0B11`, also in
  `config check`.
- `cacheMode` stays `off` (decision 5). `doctor` skips CloudBase network checks offline.
- The snapshot `FormatVersion` is unchanged: the CloudBase entries are additive, and the session's
  rule is that a new entry older snapshots lack fails with a clear missing-entry error, so
  existing Notion snapshots stay valid.

## Impact

- CLI options and help, `NotionSnapshotSession` (generic entry read/write), new
  `CloudBaseSnapshot`, `CloudBaseContentSource`, `CloudBaseFileResolver` (signing delegate),
  `ContentProviderFactory`, `DoctorCommand`.
- Not in scope: a readonly/readwrite row cache with expiry, classic-mode environments.
