# Plugin System (derive-pages / after-build)

Plugins are Bukit's primary extension point for adding derived pages and post-build artifacts without modifying the engine main flow.

Implementation: `src/Bukit.Engine/Plugins/PluginRunner.cs`, `src/Bukit.Engine/Plugins/PluginRegistry.cs`

## Lifecycle

### DerivePages (`IDerivePagesPlugin`)
Derives additional pages from routed content. Returns `(ContentItem, RouteInfo, LastModified)`.
Conflict policy via `site.deriveConflictPolicy`: `fail|warn|last-wins`.

### AfterBuild (`IAfterBuildPlugin`)
Generates additional files after all pages are rendered.

## Failure Policy: `site.pluginFailMode`
- `strict`: Plugin errors abort build
- `warn`: Log errors and continue

## Plugin Sources (discovered by PluginRegistry)

1. **built-in**: Bundled with engine (taxonomy/sitemap/rss/search-index/pagination/archive)
2. **generated**: Compile-time source-generated plugins (AOT-compatible)
3. **external**: Runtime `plugins/*.dll` loading (Non-AOT only)
4. **external-protocol**: `stdin/stdout + JSON` protocol plugins (AOT-compatible)

## generated Discovery

Types implementing `IBukitPlugin`, namespace starts with `Bukit.Plugins.`, decorated with `[BukitPlugin]`.

## external Loading (Non-AOT)

Scans `<rootDir>/plugins/*.dll`, loads types implementing `IBukitPlugin`.
Trust governance: `site.externalAssemblyTrustMode` (`warn|strict`) + `site.externalAssemblyAllowlist`.

## Plugin Execution Order

Plugins implementing `IOrderedPlugin` follow `Order` from smallest to largest (default 0).

## Plugin Configuration (`site.plugins`)

```yaml
site:
  plugins:
    path-report:
      enabled: true
      options: {}
```

Each plugin reads custom parameters from `options` via `PluginContext`.

## Built-in Plugins Overview

| Plugin | Type | Output |
|---|---|---|
| taxonomy | DerivePages + AfterBuild | `/tags/`, `/categories/` pages |
| sitemap | AfterBuild | `sitemap.xml` |
| rss | AfterBuild | `rss.xml` |
| search-index | AfterBuild | `search.json` |
| pagination | DerivePages | pagination pages |
| archive | DerivePages | archive pages |
| pages-index | DerivePages | `site.data.pages_by_id` |
