---
name: bukit-debug
description: Use for focused Bukit troubleshooting, metrics, reports, and small validation loops.
---

# Bukit Debug

Start with the smallest failing command: `config check`, `doctor`, `build`,
then audit commands. Use `--metrics` for build timing and `.bukit` reports for
route, SEO, publish, and security evidence.

Common reliability diagnostics:

| Symptom | Check |
|---|---|
| Clean is refused | Use a dedicated project output; do not manufacture a marker for unrelated files. |
| `BuildAssetOutputCollision` | Find duplicate or file/descendant destinations across routes, static, assets, media, and generated tokens; case-only differences collide on case-insensitive volumes. |
| Symlinked files are absent | Default publication scanners skip directory links; `followSymlinks` is limited to supported copy paths. |
| Template decision looks stale | Confirm the file is under resolved layouts and run the next build; manifest/root/include/layout decisions are content-sensitive. |
| Search content is truncated | Inspect `site.search.maxContentLength`; only record `content` is capped. |
| Media host rate-limits | Lower `content.media.maxConcurrency`; it limits active localization downloads, not render jobs. |
| Build report differs from audit reports | Build counts are build log events; SEO/publish/security issue totals are separate. `generatedFiles` contains public output only. |
