# 05 Content (Markdown): Front Matter Fields, Structure and Examples

> Bahasa Melayu: pada masa ini hanya tersedia dalam bahasa Inggeris dan Cina.

If you want your content to follow repository version control and be authored in a local editor, the Markdown mode is the simplest and most reliable choice.

See running example: `examples/starter/content/`.

## What You Will Get

- A recommended set of Front Matter fields (page/post/i18n/tags/categories/SEO)
- 2 copy-paste-ready "page" and "post" examples
- FAQ: where do title/slug/date come from, why does content not appear on the site

## Enabling the Markdown Content Source

In `site.yaml`:

```yaml
site:
  collections:
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
content:
  sources:
    - type: markdown
      name: content
      mode: content
      collection: page
      markdown:
        dir: content
```

The engine will recursively read all `*.md` files under `content/`. Routed content should declare `collection`, or the source should set `collection`, and the site must declare matching routing config.

## Limits and Scoped Reading (Large Repositories / Single-Page Debugging)

When your repository contains many Markdown files, or you only want to debug a few specific pages, you can limit the reading scope in the configuration:

```yaml
content:
  sources:
    - type: markdown
      name: content
      mode: content
      collection: page
      markdown:
        dir: content
        maxItems: 5000
        includePaths:
          - hello-world.md
          - blog/2026-01-update.md
        includeGlobs:
          - blog/*.md
          - "**/pages/*.md"
```

For fully explicit routing, prefer `collection` in front matter over `content.sources[].markdown.defaultType`.

Notes:

- `maxItems`: Maximum number of items to read (truncated after sorting by path).
- `includePaths`: Only read the specified paths (relative to `content.sources[].markdown.dir`; `.md` can be omitted).
- `includeGlobs`: Only read files matching the given glob patterns (matches relative paths, separator is `/`; `**` means cross-directory).

## Markdown Rendering Features

Bukit uses Markdig for Markdown rendering and supports common GFM features:

- tables, task lists, strikethrough, and autolinks
- fenced code block language classes such as `language-csharp`; the starter theme includes Prism/Highlight.js-compatible styles
- automatic heading IDs and table-of-contents data derived from body headings

Templates can read TOC entries from `page.table_of_contents` or `page.tableOfContents`. Each entry has `level`, `text`, `id`, and `url`.

## Front Matter (YAML) Basic Structure

Each Markdown file may optionally include a YAML Front Matter block:

```yaml
---
collection: page
title: About Us
slug: about
publishAt: 2026-01-01T00:00:00Z
language: zh-CN
tags: [bukit, starter]
categories: docs
summary: A one-line summary (used in list pages or meta)
seo_title: Custom SEO title (available to templates)
seo_desc: Custom SEO description (available to templates)
---
```

Note: Front Matter field names are case-insensitive (e.g. `Title` and `title` are equivalent). If you write two fields with the same name but different casing, the later one will override the earlier one.

### Common Fields Reference (User Perspective)

| Field | Common Values | Purpose |
|---|---|---|
| `collection` | string | Corresponds to a collection key in site.collections, determines routing and template (recommended to use first) |
| `type` | string | Optional content metadata for templates or integrations; it is not the 1.0 starter routing field. Use `collection` for routing. |
| `title` | text | Page title (default can be extracted from the first `#` heading in the body) |
| `slug` | `hello-world` | Core URL segment (defaults to filename) |
| `publishAt` | ISO time string | Publish date (default may use file modification time) |
| `language` | `zh-CN`/`en-US` | Multilingual filtering and output (recommended to set for every item when i18n is enabled) |
| `tags` | array or comma-separated | Used for tag-derived pages and article organization |
| `categories` | array or string | Used for category-derived pages and article organization |
| `summary` | text | List page / card summary (available to themes) |

You can define additional custom fields (e.g. `cover`, `reading_time`, `seo_*`) — they will be available as `page.fields.*` for templates to read.

## Example 1: Page — Collection-Driven Routing

File: `content/about.md`

```markdown
---
collection: page
title: About Us
slug: about
language: zh-CN
seo_title: About Us - My Site
seo_desc: This is a sample site built with Bukit
---

# About Us

This is some sample content. You can replace it with your product/team introduction.
```

Common use cases:

- About, Contact, Help Center, Product Introduction, Privacy Policy

## Example 2: Post — Collection-Driven Routing

File: `content/2026-01-hello.md`

```markdown
---
collection: post
title: January 2026 Update
slug: 2026-01-update
publishAt: 2026-01-15T10:00:00+08:00
language: zh-CN
tags: [release, roadmap]
categories: updates
summary: This month's main updates: multilingual, search and module data sources
cover: /assets/covers/2026-01.png
reading_time: 6
---

# January 2026 Update

Write your article body here...
```

Common use cases:

- Blog, News, Changelog

## Multilingual Authoring (Markdown)

When the site has multilingual support enabled:

```yaml
site:
  languages: [zh-CN, en-US]
  defaultLanguage: zh-CN
```

It is recommended that every piece of content has `language` set, and that slugs remain unique within the same language.

Example (bilingual pages for the same topic):

`content/greeting-zh.md`

```markdown
---
collection: page
title: Hello
slug: greeting
language: zh-CN
---

# Hello
```

`content/greeting-en.md`

```markdown
---
collection: page
title: Hello
slug: greeting
language: en-US
---

# Hello
```

See running examples: `examples/starter/content/greeting-zh.md` and `examples/starter/content/greeting-en.md`.

## FAQ

### 1) What happens if I don't write a title?

- The engine will try to extract it from the first `# ` heading in the body
- If there is no level-1 heading in the body either, it may fall back to the slug or filename

Recommendation: for user-facing pages, always explicitly write `title` to reduce ambiguity.

### 2) What happens if I don't write a slug?

- The filename (without `.md`) will usually be used as the slug

Recommendation: keep slugs stable; if you change the title in the future, try not to change the slug (to avoid broken links from URL changes).

### 3) Why aren't tag/category pages generated?

- This requires theme support + built-in derivation logic to be active (see: [10 Built-in Features & Output](./10-built-in-features.md))
- Verify that your content actually has `tags`/`categories` set

### 4) I want to keep a piece of content "unpublished" for now

Two recommended approaches:

- Don't add the file to the repository (finish writing locally before committing)
- Use a draft mechanism during build (if your theme/convention uses a field to mark drafts): use `--draft` during build to enable draft rendering

If you use Notion mode, it's recommended to filter by the `Published` field (see: [06 Content Notion](./06-notion-content.md)).
