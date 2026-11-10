---
name: bukit-i18n
description: Use for Bukit language settings — single-language filtering with site.language, multilingual builds with site.languages, per-page language and i18nKey, output folders per language, merged sitemap/feed/search modes, hreflang, and translated UI strings.
---

# Bukit i18n

## Single language

```yaml
site:
  language: en     # default zh-CN
```

Documents whose front matter `language` differs from `site.language` are dropped
silently; untagged documents are kept. Always set `site.language` for non-Chinese
sites.

## Multiple languages

```yaml
site:
  name: example
  title: Example
  url: https://example.com      # required for sitemaps, feeds, hreflang
  languages: [en, zh-CN]        # codes like en, zh-CN; zh_CN is rejected
  defaultLanguage: en           # must be in languages; default first entry
  sitemapMode: merged           # split | merged | index
  feed: { mode: split }         # split | merged
  search: { mode: index }       # split | merged | index
```

```markdown
---
title: About
language: en
i18nKey: about
collection: page
---
```

- Page language comes only from front matter `language` (Notion:
  `propertyMap.Language`). No `lang` alias, no `about.en.md` naming, no folder
  inference. Untagged pages go to the default language only.
- `i18nKey` groups translations for hreflang; pages with the same URL in different
  languages are grouped automatically.
- There is no fallback: a page missing in one language does not exist there.
- Data items are filtered by a `locale` field (missing = all languages).
- There are no per-language titles, base URLs, or sources.

## Output layout

Any non-empty `languages`, even one entry, builds every language into
`dist/<lang>/` with base `<baseUrl>/<lang>`, including the default language. Nothing is
rendered at `/`: add a root redirect at the host or CDN. The root holds only aggregate
files (sitemap, feeds, search index, `llms.txt`, `robots.txt`, `agent-manifest.json`).

| Setting | `split` | `merged` | `index` |
|---|---|---|---|
| `sitemapMode` | per-language sitemaps | one root sitemap with `xhtml:link` alternates; per-language ones suppressed | per-language sitemaps plus a root sitemap index |
| `feed.mode` | per-language feeds | root feeds only | — |
| `search.mode` | `/<lang>/search.json` only | adds root `search.json` | adds root `search.index.json` |

## hreflang

Built when `site.url` is set and at least two languages share a group. `x-default`
points to the default language. In SEO `inject` mode Core writes the links and removes
theme-written ones; templates can read `page.seo.alternates`.

## UI strings

There is no `t()` helper or `i18n/` folder. Branch on `site.language` or load strings
from `data/<lang>/*.yaml`, exposed as `site.data_files.<lang>.<file>`:

```scriban
{{ strings = site.data_files[site.language].ui }}
<a href="...">{{ strings.read_more }}</a>
```

## Building

With two or more languages, `bukit build` builds each language in a Git worktree
worker: the project must be a Git repository with committed inputs
(`i18n-worktree-git-required`, `-dirty`, `-input-untracked` errors otherwise). Keep
`dist/` and `.cache/` ignored. `build.languageJobs` sets parallel workers (default 1).
`bukit dev` and `bukit deploy` build in-process without that requirement.
