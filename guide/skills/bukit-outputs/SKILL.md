---
name: bukit-outputs
description: Use when configuring or checking files Bukit generates besides pages — sitemap.xml, RSS/Atom/JSON feeds, search.json and the search UI, robots.txt (including Content-Signal), 404.html, alias redirect pages, per-page JSON/Markdown projections, and agent-manifest.json.
---

# Bukit Built-in Outputs

These outputs are produced by every build. They are controlled by `site.*` settings,
not by `site.plugins` toggles: setting `site.plugins.sitemap`, `feed`, `search-index`,
or `llms-txt` to `false` has no effect.

`site.url` (absolute, e.g. `https://example.com`) is required for sitemap, feeds, and
robots.txt. Without it those files are skipped and the build logs `seo.site_url_missing`;
under `site.seo.diagnostics: strict` that fails the build.

## Output table

| Output | Condition | Path |
|---|---|---|
| Per-page projections | Every routed document | `content/<output path>.json` and `.md` (e.g. `content/about/index.html.json`) |
| `agent-manifest.json` | Always | root |
| `search.json` | Always | root (`search.index.json` in i18n index mode) |
| `bukit-search.html` | Unless `site.search.ui` is `false`/`off` | root |
| `sitemap.xml` | `site.url` set | root |
| `robots.txt` | `site.seo.robotsTxt.enabled: true` and `site.url`, no static `robots.txt` | root |
| `rss.xml` | `site.url` and `rss` in `site.feed.formats` (default `[rss]`) | root `/rss.xml` |
| `atom.xml`, `feed.json` | `atom` / `json` in formats and `site.url` | `<site.feed.path>/` (default `feed/`) |
| Collection feeds | On by default (`output.rss: false` disables); one per enabled format; needs `site.url` | `<output.feedPath or feed.path/<c>>/` |
| `llms.txt`, `llms-full.txt` | See `bukit-geo` | root |
| `404.html` | A `404.html` in the active static directory (see "404 page") | root |
| Alias redirects | Front matter `aliases` (currently breaks the build; see below) | `<alias>/index.html` |

## Feeds

```yaml
site:
  url: https://example.com
  feed:
    formats: [rss, atom, json]   # default [rss]; [] disables all feeds
    limit: 20                    # <= 0 falls back to 20
    path: feed                   # Atom, JSON, and collection feeds go here
    mode: split                  # split|merged (i18n root only)
```

RSS is always `/rss.xml` at the site root; only Atom and JSON Feed use `feed.path`.
Each collection also gets its own feeds unless `output.rss: false`; set
`output.feedTitle` / `output.feedDescription` to override `site.title` /
`site.description`. Multilingual `feed.mode: merged` skips collection feeds.
Exclude one document with front matter `feedexclude: true`.

## Sitemap

- Lists indexable routes with `loc` and `lastmod` (plus `xhtml:link` hreflang
  alternates in multilingual sites).
- Excluded: noindex pages, front matter `sitemapexclude: true`, collection or list
  route sitemap exclusions, canonicals out of scope or duplicated (logged as
  `event=sitemap.canonical_out_of_scope` / `sitemap.canonical_duplicate`).
- `site.sitemapMode: split|merged|index` controls the i18n root.
- `site.sitemapDetail` (`defaultPriority`, `defaultChangefreq`, `imageEnabled`,
  `videoEnabled`) is accepted by config validation but currently has no effect on output. Do not promise those fields.

## Search

```yaml
site:
  searchIncludeDerived: false   # include taxonomy/list pages
  search:
    ui: default                 # false/off: no bukit-search.html
    uiTheme: light              # light|dark|auto
    placeholderText: "Search..."   # plain text, HTML-encoded
    maxContentLength: 8000      # caps only each record's `content` field
    # route: /search/           # only with a real /search/ page and site.url; enables SearchAction JSON-LD
    mode: split                 # split|merged|index (i18n)
```

`bukit-search.html` is an embeddable fragment, not a route. If `search.route` is set
while `site.seo.schema.searchAction` is on (default), that route must exist and
`site.url` must be set, or the build fails (exit 2). Exclude a document with `searchexclude: true`.

## robots.txt

Off by default. When enabled it writes `User-agent: *` / `Allow: /`, an optional
`Content-Signal` line, `Sitemap: <site.url><baseUrl>sitemap.xml` when any page is
indexable, then AI crawler groups from `site.seo.geo.aiBotMode` (see `bukit-geo`).
A `robots.txt` in the active static directory (see "404 page") wins and is copied
unchanged.

```yaml
site:
  url: https://example.com
  seo:
    robotsTxt:
      enabled: true
      contentSignal:      # each key optional; only set keys are written
        search: true
        aiInput: true
        aiTrain: false
```

Output: `Content-Signal: search=yes, ai-input=yes, ai-train=no` in the `User-agent: *`
group. Values must be booleans. When robots.txt is not generated, the build warns
`event=robots.content_signal_inactive reason=robots_txt_disabled|site_url_missing|static_robots_txt`.

## Alias redirects

> Known Core defect (2.0.10/2.0.11): any page with `aliases` makes `bukit build` fail
> with exit 3 `Template path is invalid:` because alias routes have no template. Until
> Core is fixed, do not add `aliases`; redirect old URLs at the host or CDN instead.

Intended behavior once fixed:

```yaml
---
title: New page
aliases: [/old-path/, /legacy/page/]
---
```

Each alias gets an HTML page with `<meta http-equiv="refresh">` and a canonical link,
both pointing to `<baseUrl><page url>` (root-relative). Every alias is written as a
directory page `<alias>/index.html`, even when it ends in `.html`, so a legacy file URL
such as `/old.html` is not redirected; handle that at the host or CDN. Alias pages are
excluded from the sitemap. Percent-encoded aliases are decoded (`event=route.alias.decoded`); fix
the source when you see that event.

## 404 page

Only one static directory is copied, plus a parent theme's `static/`: the project
`static/` for an unnamed theme, or `themes/<name>/static/` when `theme.name` is set.
With a named theme the project-root `static/` is ignored. Put `404.html` there. It is copied to the output
root for the host to use. `bukit preview` and `bukit dev` do not serve it for missing
paths; they return a plain 404.

## Machine-readable projections

Every routed page also gets `content/<output path>.json` and `.md`, and
`agent-manifest.json` lists indexable documents with their representations. In SEO
inject mode, indexable content pages get `<link rel="alternate">` tags to their JSON
and Markdown. `bukit publish audit` checks that these exist (`bukit-publish-deploy`).

## Verify

```bash
bukit build
ls dist/sitemap.xml dist/rss.xml dist/robots.txt dist/search.json
bukit publish audit --dir dist
```
