---
name: bukit-plugins-debug
description: Use when using bukit and plugins do not take effect or behave unexpectedly, bukit build output does not meet expectations, bukit incremental builds behave incorrectly, when developing custom bukit plugins, or diagnosing bukit build performance issues
---

# Bukit Plugin System & Build Debugging

## Overview

Bukit has 13 core built-in plugins plus support for external process protocol plugins. Plugin lifecycle: `derivePages` (derive pages) → parallel rendering → `afterBuild` (post-processing). Build debugging requires understanding plugin ordering, incremental skip logic, and configuration conflicts.

**REQUIRED BACKGROUND:** Plugin config depends on `site.plugins` and `site.externalPlugins` in site.yaml — you must understand the plugin config section in bukit-config first.
**REQUIRED SUB-SKILL:** List registered plugins with `bukit plugin list`, diagnose performance with `bukit build --metrics`. CLI commands reference bukit-cli-reference.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "插件不生效"、"增量构建跳过了"、"构建排错"、"自定义插件"、"构建慢" |
| English | "plugin not working", "incremental build skipped", "build debugging", "custom plugin", "build slow" |
| Bahasa Melayu | "plugin tidak berfungsi", "binaan tambahan dilangkau", "nyahpepijat binaan", "plugin tersuai", "binaan perlahan" |

## Built-in Plugin Quick Reference

### derivePages Phase (v2.8+)

| Plugin | Hook | Function |
|------|------|------|
| **DataFilesPlugin** | derive-pages | Loads `data/` directory (YAML/JSON/TOML), injects into `context.Data["__data_files"]`. Supports multi-language data subdirectories (`data/{lang}/`) |
| **CollectionRouteIndex** | (internal) | In-memory index of routed content grouped by collection, used by Pagination, Archive, LlmsTxt, and Taxonomy plugins |
| **PagesIndexPlugin** | derive-pages | Page index data generation + Notion relation resolution |
| **TaxonomyPlugin** | derive-pages | Taxonomy page generation (tags/categories/custom taxonomy index and term pages) |
| **RelatedContentPlugin** | derive-pages | Related content recommendation based on tags/categories/keywords/date multi-dimensional weighted matching. Config: `site.related` |
| **AliasPlugin** | derive-pages | URL redirect generation from front matter `aliases`. Produces HTML redirect pages with `<meta http-equiv="refresh">` + `<link rel="canonical">` |
| **PaginationPlugin** | derive-pages | Multi-collection list/taxonomy pagination with customizable URL patterns (`urlPattern: "page/:num/"`) |
| **ArchivePlugin** | derive-pages | Year/month/day archive page generation. Config: `collection.output.archiveDetail.depth: yearly/monthly/daily` |

### afterBuild Phase (v2.8+)

| Plugin | Hook | Function |
|------|------|------|
| **SitemapPlugin** | after-build | sitemap.xml generation with `<priority>`/`<changefreq>`, Image/Video Sitemap extensions. Config: `site.sitemapDetail` |
| **FeedPlugin** | after-build | Multi-format feed generation (RSS 2.0, Atom, JSON Feed). Per-collection independent feeds. Config: `site.feed` |
| **SearchIndexPlugin** | after-build | Search index JSON generation + built-in search UI component (`bukit-search.html`). Supports `searchWeight` and `searchExclude` front matter |
| **LlmsTxtPlugin** | after-build | llms.txt + llms-full.txt generation + AI crawler robots.txt rules |
| **MenuPlugin** | after-build | Multi-menu navigation system with nesting support. Outputs `menus.json`. Config: `site.menus` |
| **ImageProcessingPlugin** | after-build | Image resizing via CLI tools (ImageMagick). Generates multi-size variants + srcset data. Config: `theme.images` |

## Plugin Registration Sources

| Source | Description | Config |
|------|------|------|
| **BuiltIn** | 13 framework built-in plugins, always loaded | None, toggle via `site.plugins` |
| **ExternalProtocol** | Standalone process plugins | `site.externalPlugins` config |
| **Policy** | Global external plugin safety control | `site.externalPluginPolicy`: `deny` (block all), `warn` (load with warning, default), `allow` (load silently). Invalid values throw `ConfigException` with `BKT-0002`. |

### External Protocol Plugins

Current AOT-focused builds support external protocol plugins through standalone processes. External assembly loading and WASM protocol plugins are disabled.

```yaml
site:
  externalPlugins:
    my-plugin:
      runtime: process
      entry: ./tools/my-plugin  # Executable path
      hooks: [derive-pages, after-build]
      enabled: true
      timeoutMs: 5000
      maxStdoutBytes: 1048576   # optional: cap stdout at 1 MB
      maxStderrBytes: 262144    # optional: cap stderr at 256 KB
      allowEnvironment:         # optional: host env vars to expose
        - PATH
      capabilities:             # optional: declared permissions (enforced at runtime)
        - derive-pages
        - emit-outputs
```

#### Plugin Capability System

Each external plugin can declare a `capabilities` list to limit what hooks it can execute. This is a **sandbox mechanism** — the engine enforces it at runtime.

| Capability | Required For | Description |
|---|---|---|
| `derive-pages` | `hooks: [derive-pages]` | Allows the plugin to generate new pages |
| `emit-outputs` | `hooks: [after-build]` | Allows the plugin to write files to the output directory |

**Enforcement rules:**
- When `capabilities` is **not declared** (`null` or absent): all hooks are allowed (backward compatible)
- When `capabilities` is **declared**: each hook execution is checked against the capability list
- If a hook requires a capability the plugin doesn't declare, build fails with `ConfigException` + `BKT-0701`

```yaml
# This will FAIL because "derive-pages" hook is declared but the plugin
# only has "emit-outputs" capability:
site:
  externalPlugins:
    bad-plugin:
      runtime: process
      entry: ./tools/bad-plugin
      hooks: [derive-pages, after-build]
      capabilities: [emit-outputs]             # missing: derive-pages
```

Error output:
```
[BKT-0701] Plugin './tools/bad-plugin' is missing required capability 'derive-pages'
            for hook 'derive-pages'. Declared capabilities: [emit-outputs].
            How to fix: add 'derive-pages' to the plugin's capabilities list in site.yaml.
```

#### Config Validation for Capabilities

`bukit config check` (and build-time validation) rejects invalid capability names:
- Valid: `emit-outputs`, `derive-pages`
- Invalid: any other string → `ConfigException: site.externalPlugins.<name>.capabilities[i] must be emit-outputs or derive-pages.`

#### Plugin Environment Isolation

When Bukit invokes a process plugin, the environment is **isolated** (host environment variables are cleared). Only the following variables are injected by default:

| Variable | Description |
|----------|-------------|
| `BUKIT_PLUGIN_NAME` | Plugin name (from `site.externalPlugins` key) |
| `BUKIT_PLUGIN_HOOK` | Current hook: `derive-pages` or `after-build` |
| `BUKIT_PROJECT_ROOT` | Absolute path to the site project root |
| `BUKIT_OUTPUT_DIR` | Absolute path to the build output directory |

To expose additional host environment variables, use `allowEnvironment` as shown above.

#### Output Limits

| Field | Type | Default | Description |
|------|------|--------|------|
| `maxStdoutBytes` | int | unlimited | Max bytes to read from plugin stdout; exceeding kills the process |
| `maxStderrBytes` | int | unlimited | Max bytes to read from plugin stderr; exceeding kills the process |

#### Plugin Output Manifest

Bukit tracks every file produced by external protocol plugins in the build manifest (`build-manifest.json`) under `pluginOutputs`. Each entry records `plugin`, `hook`, `path`, and `hash`. During incremental builds, files from a previous build that are no longer produced are automatically deleted from the output directory (stale output cleanup).

## External Plugin Environment Debugging

When an external protocol plugin fails to start or produces unexpected output:

### Check Environment Isolation

The `ProcessPluginInvoker` clears host environment and only preserves a minimal runtime allowlist:
- `PATH`, `HOME`, `USER`, `SHELL`, `TMPDIR` (POSIX)
- `USERPROFILE`, `SystemRoot`, `WINDIR`, `COMSPEC`, `PATHEXT` (Windows)
- `TEMP`, `TMP`, `DOTNET_ROOT`, `DOTNET_ROOT_X64`, `DOTNET_ROOT_X86`, `DOTNET_CLI_HOME`

If the plugin entry is a command like `dotnet`, `node`, or `python3` that requires `PATH`, ensure the host environment has the correct path.

To pass custom variables, list them in `site.externalPlugins.<name>.allowEnvironment`.

### Trace Plugin Execution

The build context records every plugin execution in `context.PluginExecutions`:

```
PluginName/Hook: success=True/False, error=<message>, ms=<duration>
```

Use this to identify which plugin failed and why:
- `success=False` → exception caught, check `error` for the exception message
- `success=True, ms=<N>` but output missing → plugin returned ok=true with empty results
- `Timeout` → plugin exceeded `timeoutMs`

### Use ProtocolEchoPlugin for Testing

For integration tests, `ProtocolEchoPlugin` (`tests/ProtocolEchoPlugin/Program.cs`) provides deterministic modes:
- `derive-success`, `derive-conflict`, `derive-lastwins` — derive-pages hook
- `derive-plugin-a`, `derive-plugin-b` — produce conflicting derived pages (URL `/plugin-conflict/page/`)
- `env-allowlist` — reports environment variables to `env-report.json`
- `env` — reports BUKIT_* context and sensitive vars to file
- `error` — returns ok=false for error path testing
- `traversal` — outputs path traversal attempt for security validation

## Plugin Execution Order

```
1. derivePages phase (in registration order):
   - DataFilesPlugin (loads data/ before rendering)
   - CollectionRouteIndex (builds route index, called lazily by other plugins)
   - PagesIndexPlugin
   - TaxonomyPlugin (generate taxonomy pages)
   - RelatedContentPlugin (compute related content)
   - AliasPlugin (generate redirect pages)
   - PaginationPlugin (pagination)
   - ArchivePlugin (archives)
   - Custom derivePages plugins

2. Parallel rendering phase:
   - All original + derived pages rendered concurrently via Scriban

3. afterBuild phase (in registration order):
   - SitemapPlugin
   - FeedPlugin (RSS + Atom + JSON Feed)
   - SearchIndexPlugin
   - LlmsTxtPlugin (llms.txt + llms-full.txt + AI crawler rules)
   - MenuPlugin (menus.json generation)
   - ImageProcessingPlugin (image resizing)
   - Custom afterBuild plugins
```

## New Plugin Configurations (v2.8+)

### FeedPlugin (replaces RssPlugin)

```yaml
site:
  feed:
    formats: ["rss", "atom", "json"]   # default: ["rss"]
    limit: 20                           # max items per feed
    path: "feed"                        # base path prefix for feed files

collection:
  output:
    rss: true                           # enable RSS for this collection
    feedPath: "custom-feed"             # per-collection feed path
    feedTitle: "My Blog"                # per-collection feed title
    feedDescription: "..."              # per-collection feed description
```

### SitemapPlugin Enhancements

```yaml
site:
  sitemapDetail:
    defaultPriority: 0.5
    defaultChangefreq: "weekly"
    imageEnabled: false                 # enable Image Sitemap extension
    videoEnabled: false                 # enable Video Sitemap extension

# front matter:
sitemap:
  priority: 0.8
  changefreq: "daily"
  images:
    - url: "..."
      caption: "..."
  videos:
    - url: "..."
      title: "..."
```

### SearchIndexPlugin Enhancements

```yaml
site:
  search:
    ui: "default"                       # "default" or false to disable built-in UI
    uiTheme: "light"                    # "light" | "dark" | "auto"
    placeholderText: "Search..."

# front matter:
searchWeight: 5                         # higher = ranked higher (default 1)
searchExclude: true                     # exclude from search index
```

### RelatedContentPlugin

```yaml
site:
  related:
    enabled: true
    threshold: 80                       # minimum score to include
    limit: 5                            # max related items per page
    indices:                            # matching dimensions and weights
      - name: tags
        weight: 100
      - name: categories
        weight: 60
      - name: keywords
        weight: 40
```

Data available in templates via `context.Data["__related_pages"]` (Dictionary<string, List<{title,url,score}>>).

### MenuPlugin

```yaml
site:
  menus:
    main:
      - identifier: home
        name: Home
        url: /
        weight: 1
      - identifier: blog
        name: Blog
        url: /blog/
        weight: 2
        children:
          - identifier: tech
            name: Tech
            url: /blog/tech/
            weight: 1
    footer:
      - identifier: about
        name: About
        url: /about/
        weight: 1
```

Data available in templates via `context.Data["menus"]` and as `menus.json` in output.

### DataFilesPlugin

Place data files in `data/` directory:
```
data/
  authors.yaml
  navigation.json
  zh-CN/
    strings.yaml
  en/
    strings.yaml
```

Data available in templates via `context.Data["__data_files"]`.

### AliasPlugin

```yaml
# front matter:
aliases:
  - /old-url/
  - /previous-url/
```

Generates HTML redirect pages for each alias.

### ArchivePlugin Enhancements

```yaml
collection:
  output:
    archive:
      enabled: true
      depth: "daily"                     # yearly | monthly | daily
      template: "pages/archive.html"     # custom template
      routePrefix: "archives"            # custom URL prefix
```

### PaginationPlugin Enhancements

```yaml
collection:
  pagination:
    enabled: true
    pageSize: 10
    urlPattern: "page/:num/"             # :num placeholder, e.g. "p/:num/"
    firstPageUsesListRoute: true

site:
  pagination:
    pageSize: 10                         # global default
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
    feed:               # Note: FeedPlugin uses "feed" key (replaced "rss" in v2.8)
      enabled: true
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

The **templateHash** is a composite fingerprint combining child theme layouts, parent theme layouts (if theme inheritance is in use), user layouts (if `theme.layouts` is overridden), each theme's `theme.yaml`, and a renderer version marker. This means parent theme layout changes or custom user layout overrides correctly trigger re-rendering.

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

### Minimal In-Process Derive Pages Plugin

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

The in-process interfaces are useful when developing Bukit itself or built-in plugins. Current AOT-focused builds do not auto-load external `.dll` assemblies from site projects.

### Minimal In-Process After-Build Plugin

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

### External Process Deployment

For site-level custom plugins, expose the plugin as a standalone process protocol plugin and configure it through `site.externalPlugins`:

```yaml
site:
  externalPlugins:
    my-plugin:
      runtime: process
      entry: ./tools/my-plugin
      hooks: [derive-pages, after-build]
      enabled: true
```

Use `bukit plugin list` to verify the plugin is registered.

## Common Error Quick Reference

| Error | Cause | Fix |
|------|------|------|
| Plugin list empty (`plugin list`) | Config not loaded or plugin directory doesn't exist | Check `--config` parameter and working directory |
| External plugin not loaded | Process entry missing, disabled, or invalid config | Verify `site.externalPlugins` and run `bukit config check` |
| WASM plugin errors | WASM not supported under AOT | Switch to a process protocol plugin |
| `deriveConflictPolicy` conflict | Derived page route duplicates existing route (stage 1: per-plugin; stage 2: final inventory validation) | Change policy to `warn` or `last-wins`, or adjust source routing |
| Taxonomy pages not generated | TaxonomyPlugin disabled or taxonomy config incomplete | Check plugin toggle and taxonomy config |
| Pagination not working | PaginationPlugin OK but collection pagination not enabled | Set `pagination.enabled: true` in collection config |
| Feed (RSS) not generated | `site.url` not set | Feed requires `site.url` for absolute link generation |
| Alias redirect page not generated | `aliases` front matter missing or malformed | Ensure `aliases` is a YAML list: `aliases: ["/old-url/"]` or string: `aliases: "/old-url/"` |
| Related content not shown | `site.related.enabled: false` or `threshold` too high | Enable `related.enabled` and lower `threshold` |
| Menu not appearing | `site.menus` not configured or empty | Configure `site.menus` with at least one menu key |
| Image processing skipped | No ImageMagick (`magick`/`convert`) installed | Install ImageMagick for image resize support |

## Plugin Security (P1-6)

### SSRF Protection for External Plugins

External plugin entries that access network resources are validated via `SsrfGuard.SsrfSafeConnectAsync`. The host rejects connections to:

- Loopback: 127.0.0.0/8, ::1
- RFC1918 private: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16
- Link-local: 169.254.0.0/16

This applies to:
- `CloneCommand` — theme asset downloads from remote URLs
- `SeoExternalAuditor` — SEO external link audits
- `ImageAssetLocalizer` — remote image downloads (governed by `content.media.blockPrivateNetworks: true` by default)

### Process Plugin Sandbox

External process plugins (`runtime: process`) run with the host's process permissions. They are **not sandboxed** — treat them as trusted local commands:
- `allowEnvironment` only exposes explicitly listed variables
- Plugin `entry` paths are validated against `SsrfGuard`
- Use `--allow-external-plugins` in CI to explicitly enable
