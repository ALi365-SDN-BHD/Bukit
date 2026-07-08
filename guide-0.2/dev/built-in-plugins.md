# Built-in Plugin Runtime

Bukit Core 1.0 loads built-in plugins only. The source of truth is
`src/Bukit-Core/Bukit.Engine/Plugins/PluginRegistry.cs`.

## Registry Source

`PluginRegistry` builds one source list:

```text
BuiltInPluginSource -> built-in plugins
```

It does not load plugin sources from project-local binaries or process hosts in
Core.

## Built-in Plugins

| Plugin | Hook type | Main purpose |
|---|---|---|
| `data-files` | derive pages | Load data files into build data |
| `pages-index` | derive pages | Build page lookup data and Notion page index helpers |
| `taxonomy` | derive pages, after build | Build taxonomy pages, taxonomy data, feeds, and redirects |
| `pagination` | derive pages | Build paginated collection list pages |
| `archive` | derive pages | Build date archive pages for configured collections |
| `related-content` | derive pages | Add related-content data to page models |
| `alias` | derive pages | Generate alias redirect pages from content fields |
| `menu` | after build | Expose configured menus and write `menus.json` |
| `image-processing` | after build | Generate resized image variants when enabled and tooling exists |

Additional publish projections such as sitemap, search, feeds, robots, llms,
and agent manifest are engine output projections. They are part of Core output,
but are not loaded through `PluginRegistry` in the same way as the built-in
plugin source above.

## Hooks

| Hook | Contract |
|---|---|
| `derive-pages` | Runs before rendering to add routed documents |
| `after-build` | Runs after render/projection stages to write or enrich artifacts |

Built-in plugin failures are governed by `site.pluginFailMode` where applicable.
Derived route conflicts are governed by `site.deriveConflictPolicy`.

## Configuration Surface

Use `site.plugins` for built-in plugin toggles and options:

```yaml
site:
  plugins:
    taxonomy:
      enabled: true
    image-processing:
      enabled: false
```

Plugin-specific behavior also comes from first-class config sections such as
`taxonomy`, `site.collections.*.pagination`, `site.collections.*.output`,
`site.menus`, and `theme.images`.

## Verification

```bash
bukit doctor
bukit build --metrics .cache/build-metrics.json
```

When output looks unexpected, first identify whether it came from routed
content, a built-in derive-pages plugin, a publish projection, or an after-build
plugin.

