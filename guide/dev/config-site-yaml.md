# `site.yaml` Contract

`site.yaml` is strict in Core 1.0. The source contract is:

- `src/Bukit.Config/AppConfig.cs`
- `src/Bukit.Config/ConfigStrictFieldValidator.cs`
- `src/Bukit.Config/ConfigJsonSchemaGenerator.cs`

Unknown fields fail during config loading. Do not keep legacy examples in the
Core guide to preserve old behavior.

## Required Top-Level Shape

```yaml
site:
  name: my-site
  title: My Site
  url: https://example.com
  baseUrl: /
content:
  sources:
    - type: markdown
      name: pages
      collection: page
      markdown:
        dir: content
build:
  output: dist
theme:
  name: starter
```

`site` and `content` are required. `content.sources` is required by the schema.

## Top-Level Nodes

| Node | Allowed purpose |
|---|---|
| `site` | identity, URLs, language, collections, menus, SEO, search, feed, related content, plugin toggles |
| `content` | Markdown/Notion sources, media localization, content model schema |
| `build` | output, cleaning, drafts, reports, fingerprinting, language jobs |
| `theme` | local theme name and theme runtime settings |
| `taxonomy` | taxonomy kinds, output mode, pinned fields |
| `logging` | log level |
| `deploy` | GitHub Pages deploy config |

## Important Strict Fields

| Path | Notes |
|---|---|
| `site.name`, `site.title` | Required site identity |
| `site.url`, `site.baseUrl` | Absolute site URL and mounted base path |
| `site.outputPathEncoding` | `none`, `slug`, `urlencode`, or `sanitize` |
| `site.sitemapMode` | `split`, `merged`, or `index` |
| `site.pluginFailMode` | `strict` or `warn` for built-in plugin failures |
| `site.deriveConflictPolicy` | `fail`, `warn`, or `last-wins` for derived-page conflicts |
| `site.collections.*.permalink` | Required per collection |
| `site.collections.*.template` | Page template for that collection |
| `site.collections.*.listRoute` | List route for collection output |
| `site.collections.*.pagination` | Collection pagination settings |
| `site.collections.*.output` | RSS, sitemap, archive, feed, and archive detail settings |
| `site.plugins.*` | Built-in plugin toggle and options bag |
| `content.sources[].type` | `markdown` or `notion` |
| `content.sources[].mode` | `content` or `data` |
| `content.sources[].markdown.dir` | Markdown root |
| `content.sources[].notion.databaseId` | Notion database ID |
| `content.media` | media download and safety settings |
| `content.modelSchema` | canonical model validation settings |
| `build.output` | output directory, default `dist` |
| `build.clean` | build-time clean behavior |
| `build.listPageContentMode` | `auto`, `always`, or `never` |
| `build.schemaFailMode` | `off`, `warn`, or `strict` |
| `build.fingerprintMode` | `size-time` or `sha256` |
| `build.report.securityFailMode` | `auto`, `off`, `warn`, or `strict` |
| `theme.name` | local theme under `themes/<name>` |
| `theme.layouts`, `theme.assets`, `theme.static` | local theme paths |
| `theme.staticTemplate` | optional static HTML rendering template |
| `theme.params` | site-owned params exposed to templates |
| `theme.components` | site-owned component definitions |
| `theme.scss`, `theme.images` | SCSS and image processing settings |
| `theme.componentValidation` | `off`, `warn`, or `strict` |
| `deploy.provider` | Required when `deploy` exists; must be `github-pages` |

## Removed From Core Config

The strict validator rejects historical project-local plugin configuration,
site-level remote theme source fields, and site-level theme inheritance fields.

Theme inheritance, when used, belongs in `theme.yaml`, not in `site.yaml`.

## Schema and Validation

```bash
bukit config schema --output site.schema.json
bukit config check
bukit doctor
```

Use `config schema` for generated contract checks and `config check` for
runtime validation, including provider-secret validation such as Notion tokens.
