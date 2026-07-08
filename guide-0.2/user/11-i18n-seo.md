# i18n and SEO

Bukit handles multilingual routing and SEO from config, content fields, and generated reports.

## i18n Config

```yaml
site:
  language: en
  languages:
    - en
    - zh-CN
    - ms
  defaultLanguage: en
```

Content can set:

```yaml
language: en
i18nKey: article-001
```

`i18nKey` groups translations. Keep it stable across language versions.

## SEO Config

```yaml
site:
  url: https://example.com
  baseUrl: /
  seo:
    enabled: true
    renderMode: inject
    diagnostics: warn
    defaultImage: /assets/og-default.png
    robotsTxt:
      enabled: true
    schema:
      webPage: true
      collectionPage: true
      searchAction: true
```

Use `renderMode: inject` unless the theme intentionally owns every head tag.

## SEO Content Fields

```yaml
seoTitle: Custom title
seoDescription: Custom description
seoImage: /assets/cover.jpg
canonical: https://example.com/canonical-url/
robots: index,follow
```

## Quality Gates

```bash
bukit build
bukit seo audit --dir dist
bukit seo audit --dir dist --strict
bukit seo diff --baseline old.json --current new.json --max-new-errors 0
```
