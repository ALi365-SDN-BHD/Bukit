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

**TemplateHash** is a composite fingerprint combining: child theme `layouts/` directory content, parent theme `layouts/` (if `theme.extends` is in use), user `layouts/` directory (if `theme.layouts` is overridden), each theme's `theme.yaml` manifest, and a renderer version marker. This means changes to parent theme templates or `theme.yaml` correctly trigger re-rendering.

**ContentHash** covers the full ContentItem: Id, Title, Slug, PublishAt, meta.type, meta.summary, fields, and ContentHtml. Body content is served via the `BodyCacheDecorator` — a build-scoped LRU cache that avoids repeated reads of the same body across multiple render passes.

### BodyCacheDecorator LRU Eviction (P3-8)

The body cache uses a real LRU (Least Recently Used) eviction strategy backed by `LinkedList<T>` + `ConcurrentDictionary<K, LinkedListNode<T>>` + `lock`:

- **Default capacity**: 256 entries
- **Hit**: Node is moved to the tail of the linked list (marking it as most recently used)
- **Miss**: New entry is added to the tail; if over capacity, the head (least recently used) entry is evicted
- **Inline bypass**: ContentHtml that is already inline (no deferred load needed) is counted as `inlineBypasses`, NOT as cache hits — maintaining the identity `totalRequests = cacheHits + cacheMisses + inlineBypasses`
- LRU behavior is verified by `BodyCacheDecoratorTests`

### DirectoryHashCache Limits (P3-9)

TemplateHash computation uses `DirectoryHashCache` which now enforces safety limits:

- **maxFiles**: 10,000 files per directory (prevents OOM on directories with very large file counts)
- **maxTotalSize**: 100 MB total scan size (prevents excessive I/O on directories with large files)
- Directories exceeding either limit trigger a warning and fall back to a simpler hash strategy

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