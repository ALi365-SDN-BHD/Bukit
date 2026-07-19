# Incremental Build

Incremental state is stored under `.cache`. `VariantBuildPipeline` loads a build
manifest during setup when incremental mode is enabled.

## Inputs

Incremental rendering considers:

- Content hash.
- Metadata and route hash.
- Composite template hash.
- Render dependency hash from config and template model inputs.
- Static, asset, media, and plugin output manifests.

## CLI Controls

`build` supports `--incremental`, `--no-incremental`, and `--cache-dir`.
First builds and missing manifests render normally. Changing templates,
theme manifests, config-dependent render inputs, or routes invalidates affected
entries.

Asset/render ownership uses the same output-filesystem destination comparer as
`BuildManifestTracker`. Stale owner cleanup must preserve a current owner that
differs only by case on a case-insensitive volume, and must remove stale
file/directory structural blockers before the new owner writes.

Template capability decisions are not keyed only by timestamp or file length.
`bukit.templates.yaml` uses a content fingerprint, while root/include/layout
static analysis is scoped to one analysis call. A same-process rebuild observes
changed, created, deleted, or corrected inputs on the next decision.
