# Built-In Plugins

Built-in plugins are registered by `BuiltInPluginSource`.

| Plugin | Hook | Purpose |
|---|---|---|
| `data-files` | derive pages | Writes data-backed generated pages. |
| `pages-index` | derive pages | Builds page index data and optional Notion relation resolution. |
| `taxonomy` | derive pages, after build | Builds taxonomy pages, data, redirects, and feeds. |
| `pagination` | derive pages | Adds paginated list routes. |
| `archive` | derive pages | Adds archive list routes. |
| `related-content` | derive pages | Builds related-content data. |
| `alias` | derive pages | Adds redirect pages from alias metadata. |
| `menu` | after build | Writes menu data from `site.menus`. |
| `image-processing` | after build | Writes image processing output metadata. |
| `analytics` | html transform | Injects validated provider fragments into content, list, and static HTML. |

Additional aggregate writers such as sitemap, feed, search, and llms output are
implemented as built-in plugin classes and projection writers, but the current
registry determines which plugins run through `PluginRunner`.

## Data Files

The `data-files` plugin loads only `.json`, `.yaml`, and `.yml` files below the
site's `data/` directory. TOML data files are not supported and stop the build
with a relative-path configuration error. Malformed JSON or YAML also stops the
build instead of being silently skipped.

Files and subdirectories are loaded in ordinal name order so generated data is
deterministic across filesystems. Two entries that produce the same
case-insensitive logical key, including files with different supported
extensions or a file/directory collision, are rejected rather than overwritten.

## Search Output Safety

The default search UI treats indexed title/snippet values as untrusted text.
Dynamic results use text nodes, `textContent`, and explicit `<mark>` elements;
configured placeholder text is HTML-encoded. Do not reintroduce an HTML parsing
sink for content-derived values.

`site.search.maxContentLength` is passed to document/list records, the built-in
search plugin, publish projections, and multilingual merged output. It caps only
the `content` value and does not cap title, summary, or generated snippet.

## Control

`site.plugins.<name>.enabled: false` disables a built-in plugin by name.
`site.pluginFailMode` chooses strict failure or warning behavior.
`site.deriveConflictPolicy` handles conflicts among derived pages.

Analytics has a second, feature-level switch at `site.analytics.enabled`.
`site.plugins.analytics.enabled` controls plugin lifecycle participation;
`site.analytics.enabled` controls output after the plugin is active. Both must
be true, providers must be non-empty, and the production/development policy
must permit injection.

## Analytics Internal Boundary

`AnalyticsPlugin` is registered exactly once by `BuiltInPluginSource`, resolved
through `PluginRegistry`, and collected by `PluginRunner` as an Engine-internal
HTML transform. Its registration name is `analytics`, version is `1.0.0`, and
order is `1000`. Core transforms run before contributed plugin transforms, so
the Analytics transform follows the SEO transform without depending on SEO
being enabled.

The `html-transform` hook, transform contexts, provider interfaces, and HTML
fragments remain internal to `Bukit.Engine`. They do not extend
`Bukit.Engine.Abstractions`, `Bukit.Plugin.Abstractions`, `Bukit.PluginHost`, or
the `bukit-plugin-v1` protocol. Providers are statically registered for Native
AOT; there is no assembly scan, reflection discovery, runtime DLL loading, or
external process access to page HTML.
