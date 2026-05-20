---
name: bukit-plugins-debug
description: Use when using bukit and plugins do not take effect or behave unexpectedly, bukit build output does not meet expectations, bukit incremental builds behave incorrectly, when developing custom bukit plugins, or diagnosing bukit build performance issues
---

# Bukit Plugin System & Build Debugging

## Overview

Bukit has 9 core built-in plugins plus support for external assembly and protocol plugins. Plugin lifecycle: `derivePages` (derive pages) → parallel rendering → `afterBuild` (post-processing). Build debugging requires understanding plugin ordering, incremental skip logic, and configuration conflicts.

**REQUIRED BACKGROUND:** Plugin config depends on `site.plugins`, `site.externalPlugins`, `site.externalAssemblyAllowlist` in site.yaml — you must understand the plugin config section in bukit-config first.
**REQUIRED SUB-SKILL:** List registered plugins with `bukit plugin list`, diagnose performance with `bukit build --metrics`. CLI commands reference bukit-cli-reference.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "插件不生效"、"增量构建跳过了"、"构建排错"、"自定义插件"、"构建慢" |
| English | "plugin not working", "incremental build skipped", "build debugging", "custom plugin", "build slow" |
| Bahasa Melayu | "plugin tidak berfungsi", "binaan tambahan dilangkau", "nyahpepijat binaan", "plugin tersuai", "binaan perlahan" |

## Built-in Plugin Quick Reference

| Plugin | Hook | Function |
|------|------|------|
| **CollectionRouteIndex** | (internal) | In-memory index of routed content grouped by collection, used by Pagination, Archive, LlmsTxt, and Taxonomy plugins |
| **TaxonomyPlugin** | derive-pages | Taxonomy page generation (tags/categories/custom taxonomy index and term pages) |
| **PaginationPlugin** | derive-pages | List/taxonomy pagination (split large lists into multiple pages) |
| **PagesIndexPlugin** | derive-pages | Page index data generation |
| **ArchivePlugin** | derive-pages | Yearly archive page generation |
| **SitemapPlugin** | after-build | sitemap.xml generation |
| **RssPlugin** | after-build | RSS Feed generation |
| **SearchIndexPlugin** | after-build | Search index JSON generation |
| **LlmsTxtPlugin** | after-build | llms.txt + llms-full.txt generation + AI crawler robots.txt rules |

## Plugin Registration Sources

| Source | Description | Config |
|------|------|------|
| **BuiltIn** | 9 framework built-in plugins, always loaded | None, toggle via `site.plugins` |
| **Generated** | AOT pre-generated plugins | None |
| **ExternalAssembly** | `.dll` assemblies in `plugins/` directory | `site.externalAssemblyTrustMode` + `site.externalAssemblyAllowlist` |
| **ExternalProtocol** | WASM or standalone process plugins | `site.externalPlugins` config |

### External Assembly Plugins

Place `.dll` files in the project `plugins/` directory for auto-discovery. SHA256 hash verification:

```yaml
site:
  externalAssemblyTrustMode: strict   # strict=require allowlist; warn=warn but allow
  externalAssemblyAllowlist:
    MyPlugin.dll: abc123...64-char SHA256...
```

### Protocol Plugins (WASM/Process)

```yaml
site:
  externalPlugins:
    my-plugin:
      runtime: process           # process or wasm
      entry: ./tools/my-plugin  # Executable path
      hooks: [derive-pages, after-build]
      enabled: true
      timeoutMs: 5000
```

## Plugin Execution Order

```
1. derivePages phase (in registration order):
   - CollectionRouteIndex (builds route index, called lazily by other plugins)
   - PagesIndexPlugin
   - TaxonomyPlugin (generate taxonomy pages)
   - PaginationPlugin (pagination)
   - ArchivePlugin (archives)
   - Custom derivePages plugins

2. Parallel rendering phase:
   - All original + derived pages rendered concurrently via Scriban

3. afterBuild phase (in registration order):
   - SitemapPlugin
   - RssPlugin
   - SearchIndexPlugin
   - LlmsTxtPlugin (llms.txt + llms-full.txt + AI crawler rules)
   - Custom afterBuild plugins
```

## Route Conflict Policy

Derived pages may conflict with existing page routes. Conflicts are detected in two stages:
1. **Per-plugin** — `PluginRunner.ApplyDeriveConflictPolicy` checks each derived page against content pages and previously-accepted derived pages
2. **Final validation** — `RouteInventoryValidator.ValidateFinalRoutes` checks the complete route inventory (content + derived + list routes) before rendering

Content-page-vs-content-page conflicts are always a build error — `deriveConflictPolicy` does not apply to them.

```yaml
site:
  deriveConflictPolicy: fail   # fail=error & abort; warn=skip with warning; last-wins=overwrite existing
```

## Plugin Toggles

```yaml
site:
  plugins:
    TaxonomyPlugin:
      enabled: false    # Disable built-in taxonomy plugin
    SitemapPlugin:
      enabled: false    # Disable sitemap generation
```

List page content mode:

```yaml
build:
  listPageContentMode: auto    # auto=static analysis; always=always include content; never=exclude content
```

When `auto` mode cannot confirm via static analysis, declare via `layouts/bukit.templates.yaml`:

```yaml
pages/index.html:
  needs_page_content: false
pages/list.html:
  needs_page_content: true
```

## Incremental Build

Incremental builds use SHA256 hashes to determine whether a page needs re-rendering. Skip condition: contentHash, metadataHash, routeHash, and templateHash are all unchanged.

- `--incremental` enables it; `--no-incremental` disables it
- Build manifest (`build-manifest-v2.json`) stored in `.cache/` directory
- First build with no manifest is always a full build
- `--clean` or `build.clean: true` does not affect incremental decisions

### Incremental Build Common Issues

| Issue | Cause | Fix |
|------|------|------|
| Modified content but page not updated | Incremental manifest not expired | `bukit clean` then rebuild |
| Page re-renders every time | Template or content changes frequently | Normal behavior; check for content that changes every time (e.g., date formulas) |
| `.cache/` corrupted | Build interrupted | Delete `.cache/` directory and rebuild |

## Build Debugging

### Page Not Output

1. Check if content is filtered: `filterProperty` + `filterType` config
2. Check if it's a draft (`draft: true`): need `--draft` parameter when building
3. Check collection route matching: do `collection` or `type` metadata match `site.collections` key names
4. Check `includeSlugs` whitelist restriction
5. Check `content.sources[].mode: data` — data mode does not generate pages

### Template Not Found

- Check if template path in site.yaml starts with `pages/`
- Check theme config and layouts directory existence
- Run `bukit doctor` to see missing template list

### Concurrent Write Conflicts

Bukit uses `ConcurrentDictionary<string, SemaphoreSlim>` to prevent concurrent writes to the same file. If you encounter write failures, check if an external process is locking the output directory.

### Build Performance

| Diagnostic | Method |
|------|------|
| Overall duration | `--metrics <path>` outputs JSON metrics file |
| Parallelism | `--jobs <n>` sets concurrent rendering thread count |
| Incremental speedup | `--incremental` skips unchanged pages |
| CI mode | `--ci` auto-sets log level to warn, reducing output |

## Custom Plugin Development

### Minimal Derive Pages Plugin

```csharp
using Bukit.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Routing;

public class HelloPlugin : IDerivePagesPlugin
{
    public string Name => "HelloPlugin";
    public string Version => "1.0.0";

    public IEnumerable<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)> DerivePages(BuildContext context)
    {
        var item = new ContentItem(
            Id: "hello",
            Title: "Hello from Plugin",
            Slug: "hello-plugin",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>Generated by plugin</p>",
            Meta: new Dictionary<string, object> { ["type"] = "page" },
            Fields: new Dictionary<string, ContentField>(),
            BodyKey: null);

        var route = RouteGenerator.Generate(item, context.Config.Site.OutputPathEncoding);

        yield return (item, route, DateTimeOffset.UtcNow);
    }
}
```

### Minimal After-Build Plugin

```csharp
public class AfterPlugin : IAfterBuildPlugin
{
    public string Name => "AfterPlugin";
    public string Version => "1.0.0";

    public void AfterBuild(BuildContext context)
    {
        var indexPath = Path.Combine(context.OutputDir, "hello.txt");
        File.WriteAllText(indexPath, "Hello from after-build plugin");
    }
}
```

### Deployment

Place the compiled `.dll` in the project's `plugins/` directory and configure the allowlist:

```yaml
site:
  externalAssemblyAllowlist:
    MyPlugin.dll: <SHA256>
```

Use `bukit plugin list` to verify the plugin is discovered.

## Common Error Quick Reference

| Error | Cause | Fix |
|------|------|------|
| Plugin list empty (`plugin list`) | Config not loaded or plugin directory doesn't exist | Check `--config` parameter and working directory |
| External plugin not loaded | Not in allowlist or hash mismatch | Verify `externalAssemblyAllowlist` config |
| WASM plugin errors | WASM not supported under AOT | Switch to process protocol plugin |
| `deriveConflictPolicy` conflict | Derived page route duplicates existing route (stage 1: per-plugin; stage 2: final inventory validation) | Change policy to `warn` or `last-wins`, or adjust source routing |
| Taxonomy pages not generated | TaxonomyPlugin disabled or taxonomy config incomplete | Check plugin toggle and taxonomy config |
| Pagination not working | PaginationPlugin OK but collection pagination not enabled | Set `pagination.enabled: true` in collection config |
| RSS not generated | `site.url` not set | RSS requires `site.url` for absolute link generation |
