# 03 Project Structure

Bukit resolves paths from the directory containing the selected config file.
For `--config site.yaml`, the root is the current site directory. For
`--site name`, `ConfigPathResolver` resolves `sites/name.yaml`.

## Recommended Tree

```text
site-root/
  site.yaml
  content/
  data/
  themes/
    site/
      theme.yaml
      layouts/
      assets/
      static/
  dist/
  .cache/
  .bukit/
```

## Runtime Directories

| Path | Owner | Notes |
|---|---|---|
| `content/` | Markdown provider | Default location for Markdown documents. |
| `themes/<name>/layouts/` | Theme runtime | Scriban files used by route templates. |
| `themes/<name>/assets/` | Asset pipeline | Copied to output assets and optionally processed. |
| `themes/<name>/static/` | Static file service | Copied to output root; static HTML needs `theme.staticTemplate`. |
| `dist/` | Build output | Must be a safe dedicated output directory. |
| `.cache/` | Incremental build and media cache | Safe to remove with `clean`. |
| `.bukit/` | Reports and generated metadata | Contains build, SEO, publish, routes, and security artifacts. |

## Output Safety

Core refuses unsafe clean operations. If `build.clean: true`, non-empty output
directories must contain `.bukit-output-marker`; this prevents accidental
deletion of unrelated directories.
