---
name: bukit-i18n
description: Use when configuring Bukit multilingual sites, language lists, default language, per-language output, hreflang, merged feed/search/sitemap behavior, or i18n validation errors.
status: stable
since: "v4.0.0-core1"
verified_by:
  - "tests/Bukit.Engine.Tests/I18nOutputMergerTests.cs"
source_anchors:
  - "src/Bukit.Config/I18nValidator.cs"
  - "src/Bukit.Engine/I18nOutputMerger.cs"
guide_chapters:
  - "guide/skills/README.md"
---

# Bukit i18n

Multilingual behavior is configured under `site`.

```yaml
site:
  language: en
  languages:
    - en
    - zh-CN
    - ms
  defaultLanguage: en
```

## Content Fields

Use content front matter or Notion property mapping to set:

```yaml
language: en
i18nKey: article-001
```

`i18nKey` groups translations. Missing or inconsistent language values can produce duplicated routes or incomplete alternates.

## Output Behavior

Bukit builds per-language routes and then merges language-aware outputs such as feeds, search indexes, sitemaps, and SEO alternate links where supported.

## Verification

```bash
bukit doctor
bukit build
bukit seo audit --dir dist
```

Check generated URLs, hreflang alternates, language-specific list pages, and merged feed/search output.
