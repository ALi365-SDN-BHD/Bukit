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
with an `i18n.document.language_unknown` warning; untagged documents are kept.
Always set `site.language` for non-Chinese sites. A site that intentionally picks
one language from a mixed-language source will warn about the other languages on
every build; warnings are capped at five per kind plus one summary line.

## Multiple languages

```yaml
site:
  name: example
  title: Example
  url: https://example.com      # required for sitemaps, feeds, hreflang
  languages: [en, zh-CN]        # codes like en, zh-CN; zh_CN is rejected
  defaultLanguage: en           # must be in languages; default first entry
  defaultLanguageInSubdir: true # false puts the default language at the output root
  defaultLanguageRedirect: true # legacy home redirect page
  defaultLanguageLegacyRedirects: false # true: redirect every old /<default>/... page
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
  `content.sources[].notion.propertyMap.Language`). No `lang` alias, no `about.en.md` naming, no folder
  inference. Untagged pages go to the default language only.
- `i18nKey` groups translations for hreflang; pages with the same URL in different
  languages are grouped automatically.
- There is no fallback: a page missing in one language does not exist there.
- A page `language` outside `site.languages` is dropped with `i18n.document.language_unknown`.
  Duplicate `i18nKey` values inside one language warn with `i18n.document.i18n_key_duplicate`.
- Data items are filtered by a `locale` field (missing = all languages).
- There are no per-language titles, base URLs, or sources.

## Output layout

With `defaultLanguageInSubdir: true` (the default), every language including the
default builds into `dist/<lang>/` with base `<baseUrl>/<lang>`. With
`defaultLanguageInSubdir: false` the default language builds into `dist/` with the
site root base URL, and the other languages keep their codes. In both layouts the
root holds the aggregate files (sitemap, feeds, search index, `llms.txt`,
`robots.txt`, `agent-manifest.json`).

`defaultLanguageRedirect: true` (the default) writes a noindex redirect page so the
home URL never 404s on static hosting: `dist/index.html` → `/<defaultLang>/` for the
prefixed layout, and `dist/<defaultLang>/index.html` → `/` for the root layout. The
redirect page is not listed in sitemaps, search, feeds, llms, or the agent manifest.
Set it to `false` when the host or CDN already redirects and would otherwise be
overridden.

When migrating a live site to the root layout, prefer a host 301 rule
`/<defaultLang>/*` → `/*`. On static hosts without redirects (GitHub Pages), set
`defaultLanguageLegacyRedirects: true`: every default-language HTML page (content,
lists, pagination, taxonomy pages, static HTML routes, aliases) also gets a noindex
redirect at its old `/<defaultLang>/...` path. The home page stays with
`defaultLanguageRedirect`; your own outputs at those paths win; non-HTML files (feeds,
sitemaps) and taxonomy aliases are not redirected. It has no effect in the prefixed
layout (`i18n.config.legacy_redirects_ignored`).

With the root layout, the default language's first path segment must not equal
another language code (case-insensitive). The build fails with
`i18n-route-reserved-language-prefix`; adding a language can therefore reject
existing content. Default-language `static/robots.txt` overrides generated robots;
other static files colliding with root-owned outputs fail with
`i18n-root-output-conflict`. Default-language reports move to
`.bukit/variants/<lang>/`; the merged reports stay in `.bukit/`. With `search.mode:
merged` the root `search.json` is the merged index and the default language's own
index is `/<defaultLang>/search.json`.

| Setting | `split` | `merged` | `index` |
|---|---|---|---|
| `sitemapMode` | per-language sitemaps | one root sitemap with `xhtml:link` alternates; per-language ones suppressed | per-language sitemaps plus a root sitemap index |
| `site.feed.mode` | per-language feeds | root feeds only | — |
| `site.search.mode` | `/<lang>/search.json` only | adds root `search.json` | adds root `search.index.json` |

With the root layout, `split` writes the default language sitemap to the root
`sitemap.xml`; `index` writes it to `/<lang>/sitemap.xml` with unprefixed page URLs
and lists it from the root sitemap index.

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
(`i18n-worktree-git-required`, `-dirty`, `-input-untracked` errors otherwise). Any
untracked file under the site root counts, so keep `dist/`, `.cache/`, and the project
`.bukit/` (`--metrics` output) git-ignored. `build.languageJobs` sets parallel workers (default 1).
`bukit dev` and `bukit deploy` build in-process without that requirement.
