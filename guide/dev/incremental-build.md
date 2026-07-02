# Incremental Build

Incremental rendering avoids rewriting unchanged pages while keeping route,
template, and content changes visible.

Source anchors:

- `src/Bukit-Core/Bukit.Engine/Incremental/`
- `src/Bukit-Core/Bukit.Engine/PageRenderDispatcher.cs`
- `src/Bukit-Core/Bukit.Engine/RenderDependencyHasher.cs`

## Switches

| Switch | Meaning |
|---|---|
| `--incremental` | Enable incremental mode |
| `--no-incremental` | Force full render |
| `--cache-dir <dir>` | Override the cache directory |
| `--jobs <n>` | Render concurrency |

The `dev` server uses incremental rebuilds after its initial clean build.

## Manifest Files

Default cache directory: `.cache/`

| File | Use |
|---|---|
| `.cache/build-manifest.json` | single-language manifest |
| `.cache/build-manifest.<lang>.json` | per-language manifest |

Each manifest entry tracks output path, URL, template, content hash, route
hash, template hash, and render dependency hash.

## Skip Conditions

A page can be skipped only when all of these are true:

1. incremental mode is enabled;
2. the manifest has the output entry;
3. the output file still exists;
4. content hash matches;
5. route hash matches;
6. template hash matches;
7. render dependency hash matches.

List pages and plugin-derived pages use the same dispatcher path with list or
derived-page-specific hashes.

## Template Hash Inputs

The template hash covers active layouts, theme metadata, relevant parent-theme
metadata when `theme.yaml` inheritance is used, and renderer version markers.

## Metrics

`--metrics <path>` writes render reasons such as:

- `new_page`
- `output_missing`
- `template_changed`
- `content_changed`
- `route_changed`
- `full_render`
- `unchanged`
- `list_render`
- `list_unchanged`

## Troubleshooting

| Symptom | Check |
|---|---|
| Template change ignored | active theme, `theme.layouts`, template hash input |
| Content change ignored | source filters, content hash, cache directory |
| Slow local rebuild | compare first build to later incremental rebuilds |
| Language output mixed | per-language manifest files and `site.defaultLanguage` |

