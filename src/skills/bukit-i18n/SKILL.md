---
name: bukit-i18n
description: Use when using bukit to create a multilingual site, bukit language switching does not work, bukit multilingual content is not correctly separated, or encountering bukit sitemap/RSS/search index merging issues
---

# Bukit Multilingual Sites

## Overview

Bukit implements multilingual sites through a three-step process: **language detection → independent variant build → output merging**. Each language builds a complete set of static pages independently, then sitemaps, RSS, and search indexes are merged at the root level.

**REQUIRED BACKGROUND:** Multilingual config depends on `site.languages`, `site.sitemapMode` and other fields in site.yaml — you must understand bukit-config first.
**REQUIRED SUB-SKILL:** Build multilingual sites with `bukit build`. CLI commands reference bukit-cli-reference.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "多语言站点"、"语言切换"、"i18n"、"sitemap 合并"、"languages 配置" |
| English | "multilingual site", "language switch", "i18n", "sitemap merge", "bukit languages" |
| Bahasa Melayu | "laman berbilang bahasa", "tukar bahasa", "i18n", "gabung sitemap", "bahasa bukit" |

## Configuration Model

```yaml
site:
  language: zh-CN              # Default language for single-language mode
  languages: [zh-CN, en]       # Language list
  defaultLanguage: zh-CN       # Default language (unmarked content belongs here)
  sitemapMode: merged          # merged | split | index
  rssMode: merged              # merged | split
  searchMode: merged           # merged | split | index
```

| Field | Description |
|------|------|
| `languages` | Languages to build, at least 1, no duplicates |
| `defaultLanguage` | Default language, must be in languages. Content without `language` metadata belongs here |
| `sitemapMode` | `merged`=merged sitemap (with hreflang); `split`=one per language; `index`=generate index sitemap |
| `rssMode` | `merged`=merged RSS; `split`=one per language |
| `searchMode` | `merged`=merged search index; `split`=one per language; `index`=generate index |

## Content Organization

### Marking Language in Markdown

```markdown
---
title: About Us
language: zh-CN
---

# About Us
```

```markdown
---
title: About Us
language: en
---

# About Us
```

Content without `language` metadata is automatically assigned to `defaultLanguage`.

### Marking Language in Notion

Add a `language` property (type: select) to the Notion database, with values like `zh-CN`, `en`. Pages without a value are assigned to `defaultLanguage`.

### i18n Association Key

Use `i18n_key` metadata to link different language versions of the same content. During sitemap merging, pages with the same `i18n_key` generate `hreflang` alternate links:

```markdown
---
title: About Us
language: zh-CN
i18n_key: about
---

---
title: About Us
language: en
i18n_key: about
---
```

## Build Process

```
1. Load content → parse items
2. Get language list → languages = [zh-CN, en]
3. For each language:
   a. FilterItemsByLanguage: filter content for this language
      - Has language metadata → match
      - No language metadata → assign to defaultLanguage
   b. baseUrl combination: / becomes /zh-CN/ or /en/
   c. BuildVariantAsync: full build of this language's static site
      → output to dist/zh-CN/ and dist/en/
4. Root-level merge:
   - Sitemap: per sitemapMode strategy
   - RSS: per rssMode strategy
   - Search index: per searchMode strategy
```

## Output Structure

```
dist/
  zh-CN/
    index.html
    blog/
      hello-world/
        index.html
    assets/
      style.css
    sitemap.xml
  en/
    index.html
    blog/
      hello-world/
        index.html
    assets/
      style.css
    sitemap.xml
  sitemap.xml       ← generated only in merged mode
  rss.xml           ← generated only in merged mode
  search.json       ← generated only in merged mode
```

## Merge Mechanisms

### Sitemap Merge

- `merged`: Generate merged sitemap at `dist/sitemap.xml`, with automatic `<xhtml:link rel="alternate" hreflang="..."/>` for each pair sharing the same `i18n_key`
- `split`: One per language at `dist/<lang>/sitemap.xml`
- `index`: Generate `dist/sitemap.xml` as an index pointing to per-language sitemaps

### RSS Merge

- `merged`: Generate merged RSS at `dist/rss.xml`
- `split`: One per language (independent output not currently implemented)

### Search Index Merge

- `merged`: Generate unified `dist/search.json`
- `split`: One per language at `dist/<lang>/search.json`
- `index`: Generate index pointing to per-language indexes

## Template Adaptation

### Language Switcher

```html
<nav>
  {{ if site.language == "zh-CN" }}
    <a href="{{ site.base_url }}/../en/{{ page.url }}">English</a>
  {{ else }}
    <a href="{{ site.base_url }}/../zh-CN/{{ page.url }}">中文</a>
  {{ end }}
</nav>
```

### Conditional Rendering

```html
{{ if site.language == "zh-CN" }}
  <time>{{ page.publish_date | date.to_string "%Y年%m月%d日" }}</time>
{{ else }}
  <time>{{ page.publish_date | date.to_string "%B %d, %Y" }}</time>
{{ end }}
```

### Root Page Language Redirect

Not needed for single-language. For multilingual, create a root `index.html` for language detection redirect (manually add to `static/` or via custom template).

## Common Issues

| Issue | Cause | Solution |
|------|------|------|
| Language switching not working | Content missing language metadata | Add `language` to content frontmatter |
| Language has no content | No matching content for that language | Verify content with that language tag exists |
| Multilingual content mixed in same page | language metadata value doesn't exactly match languages list | Ensure metadata matches site.yaml (e.g., `zh-CN` not `zh_CN`) |
| Sitemap hreflang not appearing | i18n_key not set | Set same `i18n_key` for corresponding cross-language content |
| `defaultLanguage must be included in site.languages` | Config error | Add defaultLanguage to languages |
| Search index only contains one language | searchMode is split | Change to `merged` or `index` |
| Merged RSS content duplicated | Language versions share same i18n_key but have different content | Normal behavior; RSS includes articles from all languages |

## Multi-Language Data Files (DataFilesPlugin)

The DataFilesPlugin supports language-specific data through the `data/` directory structure. Place language-specific files in `data/{lang}/` subdirectories:

```
data/
  authors.yaml                # Shared across all languages
  navigation.json             # Shared across all languages
  zh-CN/
    strings.yaml              # Chinese strings
    testimonials.yaml         # Chinese testimonials
  en/
    strings.yaml              # English strings
    testimonials.yaml         # English testimonials
```

Language-specific data is loaded with the language code as the key (e.g., `context.Data["__data_files"]["zh-CN"]`). Shared files at the root `data/` level are available to all languages.

Each language variant gets merged data: shared root-level files + language-specific overrides. In templates, access via the `__data_files` context data.
