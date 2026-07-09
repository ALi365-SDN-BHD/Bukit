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
