# Design: CloudBase offline snapshot (open decisions)

Decisions the user needs to confirm before implementation:

1. **CLI surface.** (a) Reuse `--notion-snapshot` / `--notion-offline` for all providers
   (smallest change, misleading names); (b) add `--content-snapshot` / `--content-offline` and
   keep the Notion names as aliases (recommended).
2. **What is stored.** Projected public rows only (recommended: matches the CloudBase design's
   rule that snapshots hold no private columns), or raw PostgREST pages (closer to the Notion
   snapshot, but stores columns the field policy drops).
3. **Media offline.** Look up localized files by object name (recommended), or record signed
   links and their local files in the media index.
4. **Release builds (F6).** A snapshot records resolved filter values; an offline build with a
   different `${env:...}` value fails instead of silently serving the recorded release
   (recommended), or ignores the filter offline.
5. **Cache modes.** Keep `cacheMode: off` only and rely on the snapshot (recommended for now), or
   add readonly/readwrite caching (P2 line item, about 400 lines).
