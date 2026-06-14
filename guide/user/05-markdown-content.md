# 05 Markdown Content: Front Matter and Authoring

Markdown is the simplest Core content source. It works well when content should
live in Git and be reviewed with code.

## Enable a Markdown Source

```yaml
site:
  collections:
    page:
      permalink: /{slug}/
      template: pages/page.html
content:
  sources:
    - type: markdown
      name: pages
      mode: content
      collection: page
      markdown:
        dir: content
```

Bukit recursively reads Markdown files under `content`.

## Limit the Read Scope

```yaml
content:
  sources:
    - type: markdown
      name: posts
      mode: content
      collection: post
      markdown:
        dir: content/posts
        maxItems: 500
        includePaths:
          - launch.md
        includeGlobs:
          - "2026/*.md"
```

Use this when a repository is large or you are debugging a small set of pages.

## Front Matter Basics

```markdown
---
collection: post
title: Product Launch
slug: product-launch
publishAt: 2026-06-01T09:00:00+08:00
language: en
i18nKey: product-launch
tags: [release, product]
categories: updates
summary: Launch notes for the new product.
seoTitle: Product Launch - My Site
seoDescription: Read the launch notes.
canonical: https://example.com/blog/product-launch/
---

# Product Launch

Write the article body here.
```

Recommended behavior fields:

| Field | Purpose |
|---|---|
| `collection` | Matches `site.collections` |
| `title` | User-facing page title |
| `slug` | Stable URL segment |
| `publishAt` | Publish date for sorting, feeds, and SEO |
| `language` | Multilingual filtering |
| `i18nKey` | Translation grouping |
| `draft` | Draft state for builds with or without `--draft` |
| `summary` | Cards, feeds, search, and SEO fallback |
| `tags`, `categories` | Taxonomy and related content |

## Custom Fields

Any additional front matter can be read from templates through `page.fields`:

```markdown
---
collection: page
title: Pricing
slug: pricing
heroText: Plans for every team
ctaUrl: /contact/
---
```

```html
{{ if page.fields.heroText }}
  <p>{{ page.fields.heroText.value }}</p>
{{ end }}
```

## Markdown Body

Bukit renders Markdown body content to `page.content`. Common Markdown features
include headings, fenced code blocks, tables, links, and lists.

```html
<article>
  <h1>{{ page.title }}</h1>
  {{ page.content }}
</article>
```

## Drafts

Use a draft field when your content model has one:

```markdown
---
collection: post
title: Work in Progress
slug: wip
draft: true
---
```

Build drafts explicitly:

```bash
bukit build --draft
```

## Checks

```bash
bukit config check
bukit doctor
bukit build
```

If a Markdown page does not appear, check its `collection`, `slug`, language,
draft state, source filters, and collection route.

