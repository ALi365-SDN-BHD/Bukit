# 11 I18n And SEO

Bukit handles language variants and SEO metadata inside the build pipeline.

## i18n Config

```yaml
site:
  language: en
  languages: [en, zh-CN, ms]
  defaultLanguage: en
  sitemapMode: index
  feed:
    mode: split
  search:
    mode: index
build:
  languageJobs: 2
```

Each language builds into `dist/<language>/`. Content without a language value
belongs to `defaultLanguage`.

## SEO Config

```yaml
site:
  url: https://example.com
  seo:
    enabled: true
    renderMode: inject
    diagnostics: warn
    defaultImage: /assets/og.png
    twitterSite: "@example"
    schema:
      webPage: true
      collectionPage: true
      searchAction: true
```

`SeoPipeline` builds page-level metadata, alternate links, Open Graph, Twitter,
article metadata, JSON-LD, and report issues.

## Audits

Run `seo audit` after `build`:

```bash
bukit build --clean
bukit seo audit --dir dist --strict
```

Use `seo diff` to compare reports in CI when you want to limit new warnings or
route removals.
