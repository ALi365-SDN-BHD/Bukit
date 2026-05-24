# 19 v3.0 New Features: Multi-Format Feeds, Enhanced Sitemap, Search UI, Taxonomy Refactor, Related Content, Menus, Data Files, Aliases, Image Processing

Bukit v3.0 adds 5 new plugins on top of the original 9 built-in plugins, and significantly upgrades 6 existing plugins. This page summarizes all changes.

## Quick Overview

| Feature | Status | Configuration | Output |
|------|------|---------|------|
| Multi-format Feed (RSS + Atom + JSON) | 🆕 Upgraded | `site.feed` | `rss.xml` / `feed/atom.xml` / `feed/feed.json` |
| Sitemap priority / changefreq | 🆕 Upgraded | `site.sitemapDetail` + front matter | `sitemap.xml` (with `<priority>` `<changefreq>`) |
| Sitemap image/video extensions | 🆕 Upgraded | `site.sitemapDetail` + front matter | `sitemap.xml` (with `<image:image>` `<video:video>`) |
| Search UI | 🆕 Upgraded | `site.search` | `search.json` + `bukit-search.html` |
| searchWeight / searchExclude | 🆕 Upgraded | front matter | `search.json` (with `weight` field) |
| Multi-collection pagination + urlPattern | 🆕 Upgraded | `collection.pagination` | Pagination pages |
| Daily archive depth + custom templates | 🆕 Upgraded | `collection.output.archiveDetail` | Archive pages |
| **Taxonomy v3.0.0 full upgrade** | 🆕 Refactored | `taxonomy.kinds` + `_index.md` | Hierarchical taxonomy / term RSS / redirects |
| Related content recommendations | 🆕 New | `site.related` | Data injection `__related_pages` |
| Menu system | 🆕 New | `site.menus` | `menus.json` + data injection |
| Data files | 🆕 New | `data/` directory | Data injection `__data_files` |
| URL aliases/redirects | 🆕 New | front matter `aliases` | HTML redirect pages |
| Multi-size image processing | 🆕 New | `theme.images` | Multi-size variants + srcset |

---

## 1. Multi-Format Feed (RSS + Atom + JSON Feed)

Previously, only RSS 2.0 was supported. Now three formats can be generated at the same time.

```yaml
site:
  feed:
    formats: ["rss", "atom", "json"]   # Default is rss only
    limit: 20                           # Maximum entries per feed
    path: feed                          # Output path prefix
```

**Per-collection independent feed:**

```yaml
collections:
  post:
    output:
      rss: true
      feedPath: blog-feed          # Independent directory, such as /blog-feed/atom.xml
      feedTitle: "My Blog Posts"
      feedDescription: "Latest blog updates"
```

**Exclude a page / podcast enclosure:**

```yaml
---
feed:
  exclude: true                    # Do not include in feed
  enclosure:                       # Podcast enclosure
    url: "https://example.com/ep1.mp3"
    length: 12345678
    type: "audio/mpeg"
---
```

> ⚠️ The plugin switch key changed from `rss` to `feed`: `site.plugins.feed.enabled: false`

---

## 2. Enhanced Sitemap

### priority / changefreq

```yaml
site:
  sitemapDetail:
    defaultPriority: 0.5
    defaultChangefreq: "weekly"
```

**Per-page override:**

```yaml
---
sitemap:
  priority: 0.8
  changefreq: "daily"
---
```

### Image Sitemap Extension

```yaml
site:
  sitemapDetail:
    imageEnabled: true
```

Declare in front matter:
```yaml
---
sitemap:
  images:
    - url: "/images/hero.jpg"
      caption: "Main image"
      title: "Hero"
---
```

### Video Sitemap Extension

```yaml
site:
  sitemapDetail:
    videoEnabled: true
```

```yaml
---
sitemap:
  videos:
    - url: "https://youtube.com/watch?v=xxx"
      title: "Tutorial video"
      thumbnail: "/images/thumb.jpg"
---
```

---

## 3. Search Enhancements

### Search Weight and Exclusion

```yaml
---
searchWeight: 5        # Higher weight ranks earlier (default 1)
searchExclude: true    # Do not include in the search index
---
```

### Built-in Search UI

```yaml
site:
  search:
    ui: "default"      # Enable built-in search UI (false disables it)
    uiTheme: "dark"    # light / dark / auto
    placeholderText: "Search articles..."
```

The generated `bukit-search.html` can be included from templates:

```html
{{ include "bukit-search.html" }}
```

Search UI features:
- ~5KB plain JS, zero dependencies
- Search-as-you-type, weighted title + content matching
- Supports `searchWeight` weighting
- Keyboard navigation (↑ ↓ Enter Escape)
- Highlighted search results
- Light/dark theme switching

---

## 4. Related Content Recommendations

Automatically matches related content across multiple dimensions such as tags/categories/keywords.

```yaml
site:
  related:
    enabled: true
    threshold: 80      # Minimum score
    limit: 5           # Up to 5 items per page
    indices:
      - name: tags
        weight: 100
      - name: categories
        weight: 60
      - name: keywords
        weight: 40
```

Supported matching dimensions: `tags`, `categories`, `keywords`, `collection` (bonus for the same type), and `date` (bonus within 90 days).

**Usage in templates:**

Data can be accessed via `context.Data["__related_pages"]`, indexed by content ID. Each entry contains `{title, url, score}`.

---

## 5. Menu System

```yaml
site:
  menus:
    main:
      - identifier: home
        name: Home
        url: /
        weight: 1
      - identifier: blog
        name: Blog
        url: /blog/
        weight: 2
        children:
          - identifier: tech
            name: Technology
            url: /blog/tags/tech/
            weight: 1
    footer:
      - identifier: about
        name: About
        url: /about/
        weight: 1
```

**Render in templates:**

```html
<nav>
  <ul>
    {{ for item in site.menus.main }}
      <li>
        <a href="{{ item.url }}">{{ item.name }}</a>
        {{ if item.children }}
          <ul>
            {{ for child in item.children }}
              <li><a href="{{ child.url }}">{{ child.name }}</a></li>
            {{ end }}
          </ul>
        {{ end }}
      </li>
    {{ end }}
  </ul>
</nav>
```

A `menus.json` file is also generated.

---

## 6. Data Files (`data/` Directory)

Create a `data/` folder in the project root and place YAML/JSON/TOML files in it:

```
data/
  authors.yaml
  navigation.json
  zh-CN/
    strings.yaml
  en/
    strings.yaml
```

Data is automatically loaded into `context.Data["__data_files"]`.

**Multilingual support**: Data in `data/{lang}/` subdirectories is loaded per language, while shared root-level files are available to all languages.

---

## 7. URL Aliases (Redirects)

Declare aliases in front matter to automatically generate HTML redirect pages:

```yaml
---
title: "New Article"
aliases:
  - /old-permalink/
  - /another-old-url/
---
```

The generated HTML contains:

```html
<meta http-equiv="refresh" content="0; url=/new-url/">
<link rel="canonical" href="/new-url/">
```

Alias pages are marked as `type: redirect` and automatically excluded from the sitemap.

---

## 8. Multi-Size Image Processing

Automatically generate multi-size variants for JPG/PNG images under `assets/`:

```yaml
theme:
  images:
    enabled: true
    formats: ["webp", "avif"]
    sizes: [480, 768, 1200]
    quality: 80
```

Generated variant files (such as `hero-480w.jpg` and `hero-768w.jpg`) and srcset data are injected into `__image_srcsets`.

**Dependency**: Requires ImageMagick (`magick` or `convert` command). If not installed, processing is skipped and a warning is output.

---

## 9. Pagination Enhancements

### Independent Pagination for Multiple Collections

```yaml
collections:
  post:
    pagination:
      enabled: true
      pageSize: 10
      urlPattern: "p/:num/"           # Optional: custom URL pattern
      firstPageUsesListRoute: true    # Page 1 uses listRoute
  docs:
    pagination:
      enabled: true
      pageSize: 20
```

### Global Pagination Defaults

```yaml
site:
  pagination:
    pageSize: 10
```

---

## 10. Archive Enhancements

```yaml
collections:
  post:
    output:
      archive:
        enabled: true
        depth: "daily"              # yearly | monthly | daily
        template: "pages/archive.html"
        routePrefix: "archives"     # Custom URL prefix
```

---

## 11. Taxonomy v3.0.0 Full Upgrade

The taxonomy system has been comprehensively refactored from architecture to functionality. `TaxonomyPlugin` was split from 1194 lines into 7 responsibility-focused modules, and 7 new features were added.

### Hierarchical Taxonomy

Enable it with `taxonomy.kinds[].hierarchical: true`. Terms establish parent-child relationships through the `parent` field, and `children` and `ancestors` (breadcrumb navigation) are calculated automatically.

```yaml
taxonomy:
  kinds:
    - key: categories
      kind: categories
      hierarchical: true
```

**Access in templates:**

```html
{{ if taxonomy.ancestors }}
  <nav class="breadcrumb">
  {{ for ancestor in taxonomy.ancestors }}
    <a href="{{ site.base_url }}/{{ taxonomy.kind }}/{{ ancestor }}/">{{ ancestor }}</a>
  {{ end }}
  </nav>
{{ end }}
```

### Term Metadata (`_index.md` Convention)

Following Hugo-style conventions, define term metadata through YAML front matter in `content/_taxonomy/<kind>/<slug>/_index.md`:

```yaml
---
title: "Machine Learning"
description: "Articles about machine learning algorithms, frameworks, and practices"
image: "/images/ml-cover.jpg"
weight: 10
parent: "ai"
aliases:
  - machine-learning
  - ml
---
```

Supported fields: `title`, `description`, `image`, `weight`, `parent`, and `aliases`.

### Term RSS Feed

Each term that has articles automatically generates an RSS 2.0 feed:

| Product | Path | Description |
|------|------|------|
| RSS feed | `<output>/<kind>/<slug>/feed.xml` | Latest 20 articles, with `<atom:link>` autodiscovery |

### Slug Transliteration

`SlugHelper` supports Unicode NFD decomposition and automatically converts Latin characters with diacritics to ASCII:

| Input | Output | Description |
|------|------|------|
| `café` | `cafe` | Accent removed |
| `naïve` | `naive` | Diaeresis removed |
| `über` | `uber` | Umlaut removed |
| `Straße` | `strasse` | Ligature `ß` → `ss` |
| `Æsop` | `aesop` | Ligature `Æ` → `ae` |
| `日本語` | `日本語` | CJK characters preserved |

### Alias Redirects

A term's `Aliases` field automatically generates HTML redirect pages:

```
content/_taxonomy/tags/dl/_index.md:
  aliases: [deep-learning, deep_learning]

→ generated:
  /tags/deep-learning/index.html  → redirect to /tags/dl/
  /tags/deep_learning/index.html  → redirect to /tags/dl/
```

### Term Sorting and Visibility

- `weight`: Larger numbers sort earlier (on index pages)
- `isVisible: false`: The term does not generate a page (but remains in JSON data)

### taxonomy.json Schema v2

Adds `children` and `ancestors` array fields:

```json
{
  "tags": {
    "ml": {
      "title": "Machine Learning",
      "slug": "ml",
      "count": 15,
      "description": "...",
      "children": ["deep-learning", "nlp"],
      "ancestors": ["ai"]
    }
  }
}
```

---

## Migration Guide

| Old configuration | New configuration |
|--------|--------|
| `site.plugins.rss.enabled: false` | `site.plugins.feed.enabled: false` |
| `RssPlugin` (source class name) | `FeedPlugin` (source class name) |
| Only generates `rss.xml` | Can generate RSS + Atom + JSON Feed at the same time |
| Search only has `search.json` | + `searchWeight` / `searchExclude` + built-in UI |
| `taxonomy.json` schema v1 | schema v2 (adds `children` / `ancestors` arrays) |
| Term only has `title` + `slug` | Adds `description`, `image`, `weight`, `parent`, `children`, `ancestors`, `aliases` |
| No hierarchical taxonomy | Enable with `taxonomy.kinds[].hierarchical: true` |
| No term metadata | `content/_taxonomy/<kind>/<slug>/_index.md` (Hugo style) |
| No term RSS | Automatically generates `<kind>/<slug>/feed.xml` for each term |
