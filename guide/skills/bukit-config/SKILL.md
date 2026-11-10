---
name: bukit-config
description: Use when creating or editing Bukit `site.yaml` — top-level structure, required fields, strict unknown-key validation, defaults, environment overrides and secrets, built-in plugin toggles, and `bukit config check` / `bukit config schema`.
---

# Bukit site.yaml

`site.yaml` is the single project config. Keys are camelCase and case-sensitive;
unknown keys fail with `Unknown config field '<path>.<key>'.`

## Minimal valid config

```yaml
site:
  name: example            # required
  title: Example           # required
  language: en             # default is zh-CN; see "Language" below
  url: https://example.com # needed for sitemap, feeds, robots, absolute canonicals
  collections:
    page:
      permalink: /{slug}/
      template: pages/page.html
content:
  sources:                 # required, at least one
    - type: markdown
      name: pages
      collection: page
      markdown:
        dir: content
build:
  output: dist
theme:
  layouts: layouts
```

## Top-level sections

| Section | Contents | Skill |
|---|---|---|
| `site` | Identity, URLs, language, collections, permalinks, SEO, analytics, feeds, search, plugins | below, `bukit-routing`, `bukit-seo`, `bukit-outputs`, `bukit-analytics`, `bukit-i18n` |
| `content` | `sources`, `media`, `modelSchema`, `routeMetadata`, `ordering` | `bukit-content`, `bukit-content-model` |
| `build` | Output and build behavior | below, `bukit-build` |
| `theme` | Theme name, directories, params, SCSS, images, components | `bukit-theme` |
| `taxonomy` | Tag/category pages | `bukit-routing` |
| `logging` | `level: debug\|info\|warn\|error` (default `info`) | — |
| `deploy` | GitHub Pages settings | `bukit-publish-deploy` |

## `site` keys

| Key | Default | Notes |
|---|---|---|
| `name`, `title` | required | |
| `url` | none | Must start with `http://` or `https://`. |
| `description` | none | Used by SEO, llms.txt. |
| `baseUrl` | `/` | Must start with `/`; use `/repo/` for a GitHub project page. CLI `--base-url` overrides. |
| `language` | `zh-CN` | Single-language content filter. |
| `languages`, `defaultLanguage` | none | Multilingual builds (`bukit-i18n`). |
| `timezone` | `Asia/Shanghai` | Must be a valid time zone identifier. |
| `autoSummary`, `autoSummaryMaxLength` | `false`, `200` | Generate missing summaries (1–5000). |
| `collections`, `permalinks` | none | Routing (`bukit-routing`). |
| `outputPathEncoding` | `none` | `none\|slug\|urlencode\|sanitize`; changes file paths only, not URLs. Keep `none` unless required. |
| `pluginFailMode` | `strict` | `strict\|warn`. |
| `deriveConflictPolicy` | `fail` | `fail\|warn\|last-wins` for derived-page route conflicts. |
| `seo`, `analytics`, `feed`, `search`, `searchIncludeDerived`, `sitemapMode`, `related` | | See related skills. |
| `plugins` | none | Built-in plugin toggles (below). |

Accepted but inert today: `site.menus` (written to `menus.json` after the build; not
available to templates), `site.pagination` (use `site.collections.<c>.pagination`),
and `site.sitemapDetail` (`defaultPriority`, `defaultChangefreq`, `imageEnabled`,
`videoEnabled`). For navigation use `theme.params` or a `data/` file.

## Language

Without `site.languages`, the build keeps only documents whose front matter
`language` matches `site.language` (untagged documents are kept). Because the default
is `zh-CN`, an English site that tags pages `language: en` must set `site.language:
en`, or those pages silently disappear.

## `build` keys

| Key | Default | Notes |
|---|---|---|
| `output` | `dist` | Relative, no `..`; must be a dedicated directory. |
| `clean` | `true` | |
| `draft` | `false` | Same as `--draft`; also makes `bukit dev` show drafts. |
| `schemaFailMode` | `warn` | `off\|warn\|strict` for content model validation (`bukit-content-model`). Not range-checked: a typo acts like `warn`. |
| `listPageContentMode` | `auto` | `auto\|always\|never`: whether list items get `page.content`. |
| `fingerprintMode` | `size-time` | `size-time\|sha256` change detection (not URL fingerprinting). |
| `publishDotFiles` | `false` | |
| `followSymlinks` | `false` | Supported copy paths only; forced off by `--ci`. |
| `languageJobs` | `1` | Multilingual worker count. |
| `report.enabled` | `true` | Optional `.bukit/` reports. |
| `report.securityFailMode` | `auto` | `auto\|off\|warn\|strict`. |

## Built-in plugin toggles

```yaml
site:
  plugins:
    related-content: false
    image-processing: { enabled: true }
```

Only these names have an effect: `analytics`, `data-files`, `pages-index`, `taxonomy`,
`pagination`, `archive`, `related-content`, `alias`, `menu`, `image-processing`.
Unlisted plugins are enabled. Other names have no effect on the build (`bukit doctor`
warns about them); `sitemap`, `feed`,
`search-index`, and `llms-txt` cannot be disabled here (`bukit-outputs`). The
`options:` map is accepted but no built-in plugin reads it.

## Booleans and values

Booleans must be `true`/`false` (`yes`, `no`, `1` are rejected). Enum and range errors
are reported by `bukit config check` and at build time, not while parsing.

## Environment overrides and secrets

Any variable `BUKIT_<SEG>__<SEG>...` sets a scalar before validation; segments map
snake_case to camelCase: `BUKIT_SITE__URL=https://example.com`,
`BUKIT_SITE__BASE_URL=/docs/`, `BUKIT_DEPLOY__REPO=owner/name`. Lists cannot be set
this way, misspelled segments fail strict validation, and an empty value still creates
the key.

Secrets never go in `site.yaml`: Notion reads `NOTION_TOKEN`, deploy reads
`GITHUB_TOKEN` (or `--token-env`). Anything in `theme.params` or data sources can end up
in public HTML.

## Validate

```bash
bukit config check            # exit 0 ok, 1 on any config error
bukit config schema --output site.schema.json
bukit doctor                  # adds theme, template, content, route checks
```

On a multilingual site, commit or git-ignore the generated schema file (or write it
outside the site root): an untracked file there fails the next `bukit build` with
`i18n-worktree-dirty` (`bukit-build`).

`config check` runs all `site.yaml` validators, including collection rules (`{slug}`
in permalinks, pagination and filteredLists patterns, source `collection`
assignment) and provider secrets (for example `NOTION_TOKEN`). On failure it prints
`✖ Config error` and the message to stdout and exits 1. It does not load theme.yaml,
templates, or content files, so route conflicts and template errors appear only in
`doctor` or `build`.

## Common mistakes

- Using snake_case in `site.yaml` (it is camelCase) or camelCase in `theme.yaml` (it is
  snake_case).
- Forgetting `site.language` on a non-Chinese single-language site.
- Defining `site.collections` but not setting `collection:` on each content source
  (see `bukit-content`).
- Keeping removed keys such as `googleAnalyticsId` or `disableInPreview`.
