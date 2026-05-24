# Incremental Builds (manifest / cache-dir / render-skip reasons)

Implementation: `src/Bukit.Engine/Incremental/*`, `src/Bukit.Engine/PageRenderDispatcher.cs`

## Switches and Directories

- Enabled by default
- CLI: `--incremental`/`--no-incremental`, `--cache-dir <dir>` (default `<rootDir>/.cache`)
- `--jobs <n>`: controls rendering parallelism (independent of incremental)

## Manifest Files

Default paths: `<cacheDir>/build-manifest.json` (single language), `<cacheDir>/build-manifest.<lang>.json` (multilingual).

```json
{
  "version": 1,
  "templateHash": "<sha256>",
  "entries": {
    "<normalizedOutputPath>": {
      "outputPath": "blog/hello/index.html",
      "url": "/blog/hello/",
      "template": "pages/post.html",
      "contentHash": "<sha256>",
      "routeHash": "<sha256>",
      "templateHash": "<sha256>"
    }
  }
}
```

## Render Skip Conditions

To skip rendering, all must match:
1. Incremental enabled
2. Manifest has entry for the page
3. Output file exists
4. TemplateHash, ContentHash (covers Id/Title/Slug/PublishAt/meta.type/meta.summary/fields/ContentHtml), RouteHash (url/outputPath/template) all match

Homepage/list pages use dedicated `ListContentHash`. Plugin-derived pages use the same logic.

## renderReasons (in `--metrics` output)

- `new_page`, `output_missing`, `template_changed`, `content_changed`, `route_changed`, `full_render`
- `unchanged`, `list_render`, `list_unchanged`

## Troubleshooting

1. "Template change didn't take effect": Confirm `theme.layouts` points to expected directory
2. "Local rendering very slow": Check `--no-incremental` is not in use, cache-dir is writable
3. "Multilingual caches cross-contaminate": Manifests are separated by language suffix

```

## Build Recovery

When a build is interrupted (e.g., process crash, system shutdown), Bukit detects the incomplete state on the next run and automatically cleans the output directory for a fresh start.

### How It Works

1. **Start marker**: At the beginning of each build, Bukit writes `.bukit-build-state.json` with `status: started` to the output directory.
2. **Completion marker**: When the build finishes successfully, the status is updated to `completed`.
3. **Recovery detection**: On the next build (non-Clean mode), if the status file shows `started`, the engine automatically deletes the output directory and rebuilds from scratch.

### Manual Clean Build

To explicitly force a clean rebuild (ignoring any previous state):

```bash
bukit build --clean
```

### Recovery Behavior Summary

| Scenario | Behavior |
|---|---|
| Previous build completed | Normal incremental build |
| Previous build interrupted (`--clean` not set) | Auto-clean output directory, then full rebuild with warning log |
| `--clean` explicitly set | Always clean output directory before build |

This ensures the output directory stays consistent even after unexpected build interruptions.

## Comparison with Full Build

```