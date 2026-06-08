# Plugin System (derive-pages / after-build)

Plugins are Bukit's primary extension point for adding derived pages and custom post-build artifacts without modifying the engine main flow. Core machine-readable publish artifacts are owned by the publish projection pipeline, not by default after-build plugins.

Implementation: `src/Bukit.Engine/Plugins/PluginRunner.cs`, `src/Bukit.Engine/Plugins/PluginRegistry.cs`

## Lifecycle

### DerivePages (`IDerivePagesPlugin`)
Derives additional pages from routed content. Returns `(ContentDocument, RouteInfo, LastModified)`.
Conflict policy via `site.deriveConflictPolicy`: `fail|warn|last-wins`.

### AfterBuild (`IAfterBuildPlugin`)
Generates additional files after all pages are rendered.

### Publish Projections (`IPublishProjection`)
Generates canonical publish representations from the content graph and route inventory. The built-in aggregate outputs `sitemap.xml`, RSS/Atom/JSON Feed, `search.json`, `llms.txt`, `llms-full.txt`, `robots.txt`, and `agent-manifest.json` are registered in `PublishRepresentationRegistry` and audited in the publish report.

## Failure Policy: `site.pluginFailMode`
- `strict`: Plugin errors abort build
- `warn`: Log errors and continue

## Plugin Sources (discovered by PluginRegistry)

1. **built-in**: Bundled with engine (taxonomy/pagination/archive/menu/image; projection-owned artifacts are registered separately)
2. **generated**: Compile-time source-generated plugins (AOT-compatible)
3. **external**: Runtime `plugins/*.dll` loading (Non-AOT only)
4. **external-protocol**: `stdin/stdout + JSON` protocol plugins (AOT-compatible)

## external-protocol Security

External protocol plugins run with **environment isolation**: host environment variables are cleared, and only `BUKIT_PLUGIN_NAME`, `BUKIT_PLUGIN_HOOK`, `BUKIT_PROJECT_ROOT`, and `BUKIT_OUTPUT_DIR` are injected. Use `allowEnvironment` in `site.externalPlugins` to explicitly expose additional host variables.

Output limits (`maxStdoutBytes` / `maxStderrBytes`) cap plugin stdout/stderr; exceeding the limit kills the process. All plugin outputs are tracked in the build manifest with plugin/hook/path/hash metadata, and stale outputs from previous builds are automatically cleaned during incremental builds.

### Plugin Capability Enforcement

External plugins can declare a `capabilities` list that acts as a sandbox:

```yaml
site:
  externalPlugins:
    my-plugin:
      capabilities:
        - derive-pages   # Required for hooks: [derive-pages]
        - emit-outputs   # Required for hooks: [after-build]
```

Implementation: `src/Bukit.Engine/Plugins/PluginCapability.cs`, `src/Bukit.Engine/Plugins/PluginCapabilityEnforcer.cs`.

**Enforcement rules:**
- `capabilities` not declared → config validation fails (`ConfigException` / `BKT-0701`)
- `capabilities` declared → each hook execution checked against capability list at runtime
- Hook missing required capability → `ConfigException` with `BKT-0701`
- Invalid capability names → `ConfigException` during config validation

Capability check is integrated in `ExternalProtocolPlugin.DerivePagesAsync()` and `ExternalProtocolPlugin.AfterBuildAsync()` before invoking the protocol invoker.

See [External Plugin Protocol](./external-plugin-protocol.md) for the full request/response schema and protocol negotiation details.

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
| pagination | DerivePages | pagination pages |
| archive | DerivePages | archive pages |
| pages-index | DerivePages | `site.data.pages_by_id` |

## Built-in Publish Projections

| Projection | Output |
|---|---|
| sitemap | `sitemap.xml` |
| feed / atom / jsonfeed | `rss.xml`, `feed/atom.xml`, `feed/feed.json` |
| search | `search.json`, optional `bukit-search.html` |
| llms / llms-full | `llms.txt`, `llms-full.txt` |
| robots | `robots.txt` |
| agent-manifest | `agent-manifest.json` |
