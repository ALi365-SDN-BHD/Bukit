---
name: bukit-routing
description: Use when using bukit to customize URL structures, bukit URLs are not generated as expected, configuring bukit permalink patterns, setting up bukit collection routes, or troubleshooting 404 errors on bukit-deployed sites
---

# Bukit URL Routing & Permalinks

## Overview

Bukit generates URLs and output paths for each content item through **permalink patterns** and **collection route rules**. Route priority: content metadata override > collection config > global permalinks > built-in defaults (post→`/blog/{slug}/`, page→`/pages/{slug}/`).

**REQUIRED BACKGROUND:** Routing config depends on `site.collections` and `site.permalinks` in site.yaml — you must understand the collection config model in bukit-config first.
**REQUIRED SUB-SKILL:** Verify route output with `bukit build`. CLI commands reference bukit-cli-reference.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "自定义 URL"、"permalink 模式"、"集合路由"、"URL 编码策略"、"slug" |
| English | "custom URL", "permalink pattern", "collection routes", "URL encoding", "bukit routing" |
| Bahasa Melayu | "URL tersuai", "corak permalink", "laluan koleksi", "pengekodan URL", "penghalaan bukit" |

## Route Priority

```
1. Content metadata route.url + route.outputPath + route.template  ← Highest
2. site.collections match (by collection field or type field)
3. site.permalinks global rules
4. Built-in defaults: post → /blog/{slug}/, page → /pages/{slug}/
```

## Permalink Patterns

| Placeholder | Replaced With | Example (slug=hello-world, date=2026-05-05) |
|--------|--------|------------------------------------------|
| `{slug}` | Content slug | `hello-world` |
| `{year}` | Publish year (4-digit) | `2026` |
| `{month}` | Publish month (2-digit) | `05` |
| `{day}` | Publish day (2-digit) | `05` |
| `{type}` | Content type (post/page/collection name) | `post` |
| `{title}` | Equivalent to `{slug}` | `hello-world` |

All URLs automatically get leading and trailing slashes (`/blog/hello-world/`), and output paths automatically get `index.html` appended (`blog/hello-world/index.html`).

## Collection Routes

Each collection can define its own permalink and template:

```yaml
site:
  collections:
    article:
      permalink: /articles/{year}/{month}/{slug}/
      template: pages/post.html
      listRoute: /articles/
      pagination:
        enabled: true
        pageSize: 20
      output:
        rss: true
        sitemap: true
    page:
      permalink: /{slug}/
      template: pages/page.html
```

### Collection Matching Rules

- Content `collection` metadata field → matches `site.collections.<key>`
- If collection is empty, fallback to `type` field → matches `site.collections.<type>`
- If neither matches → use global permalinks or built-in defaults when no rules exist

### List Route (listRoute)

When a collection defines `listRoute`, a list page is generated (using the collection's template + `pages` variable). Must start with `/`.

## URL Encoding Strategy

`site.outputPathEncoding` controls how output directory names are encoded:

| Mode | Behavior | Use Case |
|------|------|---------|
| `none` | No processing, keep as-is | English slugs, default |
| `slug` | Convert to lowercase ASCII slug | Multi-language slugs to ASCII |
| `urlencode` | `Uri.EscapeDataString` | Special character URL encoding |
| `sanitize` | Remove Windows-disallowed characters (`<>:"|?*`), spaces → `-` | Windows dev environments |

## Route Override

Set the `route` field in content metadata to fully customize routing. Markdown frontmatter:

```yaml
---
route:
  url: /custom/path/
  outputPath: custom/path/index.html
  template: pages/special.html
---

# My Page
```

Or separate fields:

```yaml
---
url: /custom/path/
outputPath: custom/path/index.html
template: pages/special.html
---
```

All three fields are required; if any is missing, routing falls back to collection routing.

## Output Path Rules

- URL `/{slug}/` → output path `{slug}/index.html`
- URL `/{year}/{month}/{slug}/` → `{year}/{month}/{slug}/index.html`
- Path separators auto-converted to `\` on Windows

## Common Errors

| Error | Cause | Fix |
|------|------|------|
| Route conflict (doctor error) | Multiple content items generate same URL | Check slug uniqueness or permalink pattern |
| Permalink generates unexpected URL | Placeholder typo | Use `{slug}` not `{Slug}` or `{SLUG}` |
| `listRoute must start with '/'` | listRoute doesn't start with `/` | Change to `/articles/` |
| `permalink must include {slug}` | Permalink missing {slug} placeholder | Add `{slug}` to the pattern |
| Chinese characters in URL truncated on Windows | Output path encoding not set | Set `site.outputPathEncoding: slug` or `sanitize` |
| Collection matched incorrectly | collection or type field name mismatch | Check content metadata and site.collections key names |
| Route override fields ignored | Not all three fields provided | Ensure route includes url, outputPath, and template |
