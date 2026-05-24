# 10 Built-in Features and Outputs: sitemap/rss/search, Tags, Categories, and Derived Pages

In addition to generating page HTML, Bukit also generates a set of "site-level artifacts" based on your content and configuration. These are used for SEO, subscriptions, search, and content aggregation.

This page focuses on "what users can control and what files are generated." If you need more detailed plugin contracts and boundaries, see the developer docs: [guide/dev/built-in-plugins](../dev/built-in-plugins.md).

## What You Will Get

- What extra files are generated, and where
- How these files are output for multilingual sites (`split`/`merged`/`index`)
- What "derived pages" such as tags/categories/archives/pagination are
- Common issues: why sitemap links are wrong, and why `search.json` is empty

## Site-Level Artifact List (Common)

In the build output directory (`build.output`, default `dist/`), you will usually see:

- `sitemap.xml`
- `rss.xml`
- `search.json` (browser-facing search data)
- `search.index.json` (optional: aggregated index)
- `tags/`, `categories/` (derived list pages; the exact output depends on the theme and derivation logic)

Runnable examples for comparison:

- `examples/starter/dist/`
- `examples/starter/.bukit_test/dist/` (complete output used for testing)

## sitemap.xml: Search Engine Index Entry Point

### What You Can Configure

- `site.url`: Absolute site domain (the basis for generating absolute links)
- `site.baseUrl`: Sub-path (common for GitHub Pages)
- `site.sitemapMode`: Multilingual output mode (see the next section)
- `site.sitemapDetail.defaultPriority`: Default `<priority>` value (0.0-1.0, v3.0+)
- `site.sitemapDetail.defaultChangefreq`: Default `<changefreq>` value (v3.0+)
- `site.sitemapDetail.imageEnabled`: Whether to enable the image Sitemap extension (v3.0+)
- `site.sitemapDetail.videoEnabled`: Whether to enable the video Sitemap extension (v3.0+)

### Per-Page Overrides (v3.0+)

```yaml
---
sitemap:
  priority: 0.8
  changefreq: "daily"
  images:
    - url: "/images/hero.jpg"
      caption: "Hero image"
---
```

### Common Pitfalls

- `site.url` is not set: the sitemap may generate relative links or incorrect absolute links
- `baseUrl` is misconfigured: URLs in the sitemap have the wrong prefix, causing search engine crawling to fail

For deployment details, see: [13 Deploying to GitHub Pages](./13-deploy-github-pages.md).

## rss.xml → Multi-Format Feeds (v3.0 Upgrade)

Previously, Bukit only generated `rss.xml`. Starting in v3.0, it can generate RSS 2.0 + Atom 1.0 + JSON Feed 1.1 at the same time.

Configuration (added in v3.0):

```yaml
site:
  feed:
    formats: ["rss", "atom", "json"]
    limit: 20
    path: feed
```

Generated files:
- `rss.xml` (RSS 2.0, existing format)
- `feed/atom.xml` (Atom 1.0, new)
- `feed/feed.json` (JSON Feed 1.1, new)

⚠️ The plugin switch key changed from `rss` to `feed`:
```yaml
site:
  plugins:
    feed:
      enabled: false   # Disable all feed generation
```

> Per-collection independent feeds: see `collection.output.feedPath`.

Subscription feeds usually depend on:
- Site URL (`site.url`)
- Content title/publish time/type (especially for posts)

If feed content is incomplete, check first:
- Whether your content has `publishAt`
- Whether it was excluded by draft/filter conditions (Notion Published, `build.draft`, etc.)

## search.json: Site Search Data

`search.json` is usually a list of "title/summary/URL for each page" for frontend JS search implementations.

### Search Weight and Exclusion (v3.0+)

Control search behavior in front matter:

```yaml
---
searchWeight: 5        # Higher weight ranks earlier (default 1)
searchExclude: true    # Do not add to the search index
---
```

### Built-in Search UI (v3.0+)

```yaml
site:
  search:
    ui: "default"      # Enable the built-in search UI (false disables it)
    uiTheme: "dark"    # light / dark / auto
    placeholderText: "Search..."
```

Generates `bukit-search.html`, which can be included in templates:

```html
{{ include "bukit-search.html" }}
```

The search UI includes an input box, keyword matching, keyboard navigation, and highlighted results, with no additional JS library required.

You usually need:
- A search UI implemented in the theme (reads and filters `search.json`)
- Or directly use the built-in `bukit-search.html`

If `search.json` is empty:
- The site may have no content items (content loading failed / content was filtered out)
- Or the theme/configuration has not enabled the corresponding output (depending on version and mode)

## Tags and Categories (tags / categories)

When your content contains `tags` or `categories`:

- The engine/plugin aggregates this information
- The theme usually renders list pages and detail pages for tags/categories

Optional: enable pinned sorting for content under a specific category/tag:

- Mark content with `pinned: true` (optional numeric `pinOrder`; smaller numbers appear earlier)
- Configuration keys: `taxonomy.pinField` / `taxonomy.pinOrderField` (for multiple data sources, use `pinFieldBySource` / `pinOrderFieldBySource` to map field names)

### Term Metadata (v3.0.0+)

You can set extra information for each tag/category in either of two ways:

**Approach 1: data file** (`content/data/tags.yaml`):
```yaml
- title: Machine Learning
  slug: ml
  description: Everything about ML and AI
  image: /assets/images/ml-cover.png
  weight: 10          # Sorting weight; higher appears earlier
  parent: tech        # Parent category (hierarchical)
```

**Approach 2: directory convention** (`content/_taxonomy/tags/ml/_index.md`), Hugo-style:
```yaml
---
description: Everything about ML and AI
image: /assets/images/ml-cover.png
---
```

### Hierarchical Taxonomies

Enable with `taxonomy.kinds[].hierarchical: true`. Terms establish parent-child relationships through the `parent` field, and `children` and `ancestors` (for breadcrumb navigation) are computed automatically.

### RSS Feeds

Each term automatically generates an independent RSS 2.0 feed: `/tags/python/feed.xml`, which can be subscribed to separately.

### Alias Redirects

Terms can be configured with aliases (`aliases` field), automatically generating redirect pages so old URLs do not 404.

For Markdown examples (tags/categories), see: [05 Markdown Content](./05-markdown-content.md).

## Derived Pages: What tags/categories/pagination/archives Are

Derived pages are not pages you directly write in the content source. Instead, they are pages "derived" by the engine from your content, for example:

- `/tags/<tag>/`: Article list under a specific tag
- `/categories/<category>/`: Article list under a specific category
- `/blog/page/2/`: Paginated list page
- `/archive/2026/`: Archive by year

What users need to care about:

- Whether derived pages are rendered depends on: whether the engine enables the corresponding derivation capability + whether the theme provides the corresponding templates
- Derived pages participate in sitemap/search (so accurate `baseUrl` and `url` values are even more important)

## pluginFailMode: Whether to Interrupt the Build When Derivation/Output Fails

```yaml
site:
  pluginFailMode: strict  # strict (default) | warn
```

- `strict`: Plugin errors interrupt the build (suitable for production)
- `warn`: Log errors but continue output (suitable for migration/debugging)

## Multilingual Output Modes (sitemap/rss/search)

For multilingual sites, these artifacts have three common modes (with the same meaning for each artifact type):

- `split`: One file per language (for example, `zh-CN/sitemap.xml` and `en-US/sitemap.xml`)
- `merged`: Aggregate into one file (usually output one file at the root)
- `index`: Output an index file at the root that points to each language-specific file

For how to choose, see: [11 Multilingual and SEO](./11-i18n-seo.md).

## Automatic Image Optimization (WebP / AVIF)

During build, PNG/JPG images in the `assets/` directory are automatically converted to WebP/AVIF formats.

**Dependencies**: Install the `cwebp` (libwebp) or `magick` (ImageMagick) CLI:

```bash
# macOS
brew install webp imagemagick
# Linux
sudo apt install webp imagemagick
```

**Configuration**:

```yaml
theme:
  images:
    enabled: true
    formats: [webp]          # avif is also supported
    sizes: [480, 768, 1200]  # Responsive sizes for srcset
    quality: 85
```

If the conversion tools are not installed, the build skips image optimization and prints a warning instead of failing.

## Automatic SCSS Compilation

During build, `.scss` files in the `assets/` directory are automatically compiled to `.css`.

**Dependencies**: Install the `sass` or `dart-sass` CLI:

```bash
npm install -g sass
```

**Configuration**:

```yaml
theme:
  scss:
    enabled: true
```

After successful compilation, the original `.scss` files are deleted automatically. If the CLI is not installed, compilation is skipped and a warning is printed.

## Related Content Recommendations (v3.0+)

Automatically match related content based on multiple dimensions such as tags, categories, and keywords.

```yaml
site:
  related:
    enabled: true
    threshold: 80
    limit: 5
    indices:
      - name: tags
        weight: 100
      - name: categories
        weight: 60
```

## Menu System (v3.0+)

Multi-menu navigation with nested submenu support.

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
            name: Tech
            url: /blog/tags/tech/
            weight: 1
```

## Data Files (v3.0+)

Place YAML/JSON/TOML files in the `data/` directory, and they are automatically loaded into templates during build.

```
data/
  authors.yaml
  navigation.json
```

## URL Alias Redirects (v3.0+)

Declare old URLs in front matter to automatically generate HTML redirect pages:

```yaml
---
aliases:
  - /old-url/
  - /previous-permalink/
---
```

## Multi-Size Image Processing (v3.0+)

Automatically generate multiple size variants for images under `assets/` (depends on ImageMagick).

```yaml
theme:
  images:
    enabled: true
    sizes: [480, 768, 1200]
    quality: 80
```

📖 For detailed usage and full configuration, see: [19 New Features in v3.0](./19-new-features-v3.md).
