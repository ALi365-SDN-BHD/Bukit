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

Additional aggregate writers such as sitemap, feed, search, and llms output are
implemented as built-in plugin classes and projection writers, but the current
registry determines which plugins run through `PluginRunner`.

## Control

`site.plugins.<name>.enabled: false` disables a built-in plugin by name.
`site.pluginFailMode` chooses strict failure or warning behavior.
`site.deriveConflictPolicy` handles conflicts among derived pages.
