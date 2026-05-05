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
