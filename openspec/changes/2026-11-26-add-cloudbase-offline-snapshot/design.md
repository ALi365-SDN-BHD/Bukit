# Design: CloudBase offline snapshot

Decisions confirmed by the user on 2026-11-26 (all recommended options):

1. **CLI surface.** Add `--content-snapshot` / `--content-offline` and keep the Notion names as
   aliases. Giving both `--content-snapshot` and `--notion-snapshot` is an argument error.
2. **What is stored.** Projected public rows only: the mapped columns, `fileFields` and the
   columns the field policy publishes, with personal and operator columns and nested keys removed.
   The offline build runs the same projection, so a narrower policy still drops columns and a
   wider one cannot reveal what was never stored.
3. **Media offline.** Each signed object is recorded with its media identity (scheme, host and
   path without the token). Offline, the resolver answers signing from these identities, and the
   media localizer, already offline under the snapshot scope, finds the cached file by the same
   key it stored while recording. An unrecorded object fails like an unavailable one (placeholder,
   reason `not-in-snapshot`), and a recorded object missing from the media cache gets the
   placeholder too: the recorded URL has no token and names the environment, so it is never kept.
4. **Release builds.** The entry path hashes the resolved filter value under a directory per
   source shape (mapped fields, file fields, field policy, sort, maxItems, bucket), under a
   directory per (envId, table, filter field). Two sources on one table therefore never overwrite
   each other. A missing table directory: `BKT-0B11`. A missing shape directory (changed source
   settings) or a missing value file (another filter value): `BKT-0B13`.
5. **Cache modes.** `cacheMode: off` only.

Entries are recorded after the rows built successfully and committed with the snapshot, so a
failed build keeps the previous snapshot.
