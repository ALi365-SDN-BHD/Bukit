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
1. Content metadata full override (url + outputPath + template)  ← Highest
2. Content metadata partial override (url only or url + template)
3. site.collections match (by collection field or type field)
4. site.permalinks global rules
5. Built-in defaults: post → /blog/{slug}/, page → /pages/{slug}/
```

When only `url` is provided in metadata, Bukit enters **partial override**: `outputPath` is auto-derived from the URL, and `template` falls back to the collection/permalink/default rule. See Route Override below for details.

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

## URL Aliases & Redirects (AliasPlugin)

The AliasPlugin generates HTML redirect pages for content items with `aliases` defined in front matter. Each alias creates an HTML file with `<meta http-equiv="refresh">` and `<link rel="canonical">` pointing to the original URL.

### Front Matter Configuration

```yaml
---
title: My Post
aliases:
  - /old-url/
  - /previous-permalink/
---
```

Multiple aliases per content item are supported. The alias URL path is auto-normalized (leading/trailing slashes added if missing).

### Generated Redirect

```html
<!DOCTYPE html>
<html>
<head>
  <meta http-equiv="refresh" content="0; url=/new-url/">
  <link rel="canonical" href="/new-url/">
</head>
<body>
  <p>Redirecting to <a href="/new-url/">/new-url/</a></p>
</body>
</html>
```

Aliases can be specified as a single string or a list. The generated pages are marked with `type: redirect` and excluded from sitemap.

## URL Encoding Strategy

`site.outputPathEncoding` controls how output directory names are encoded. This applies to both content pages and derived pages (pagination, archive, taxonomy pages).

| Mode | Behavior | Use Case |
|------|------|---------|
| `none` | No processing, keep as-is | English slugs, default |
| `slug` | Convert to lowercase ASCII slug | Multi-language slugs to ASCII |
| `urlencode` | `Uri.EscapeDataString` | Special character URL encoding |
| `sanitize` | Remove Windows-disallowed characters (`<>:"|?*`), spaces → `-` | Windows dev environments |

## Route Override

Content metadata can override routing at two levels.

### Full Override (backward compatible)

Set all three fields — `url`, `outputPath`, and `template` — to take full control. Markdown frontmatter:

```yaml
---
route:
  url: /custom/path/
  outputPath: custom/path/index.html
  template: pages/special.html
---

# My Page
```

Or separate top-level fields:

```yaml
---
url: /custom/path/
outputPath: custom/path/index.html
template: pages/special.html
---
```

All three fields must be present for full override; otherwise partial override applies.

### Partial Override (url-only)

When only `url` is provided (directly or via `route.url`), Bukit auto-derives `outputPath` from the URL and keeps the collection/permalink/default template:

```yaml
---
url: /my-slug/
---

# outputPath auto-derived: my-slug/index.html
# template:       follows collection rule
```

```yaml
---
route:
  url: /my-slug/
  template: pages/special.html  # optional: override template
---

# outputPath auto-derived: my-slug/index.html
```

**Rules for partial override:**
- `url` → must be present; normalized with leading/trailing slashes
- `outputPath` → auto-derived via `NormalizeUrl(url)` → `BuildOutputPathFromUrl(url, encoding)`; any manually-supplied value is ignored
- `template` → if omitted, inherits from collection/permalinks/default; if provided, uses it
- `outputPath`-only override is **not supported** (to avoid URL/disk-path semantic split)

## Output Path Rules

- URL `/{slug}/` → output path `{slug}/index.html`
- URL `/{year}/{month}/{slug}/` → `{year}/{month}/{slug}/index.html`
- Path separators auto-converted to `\` on Windows
- Encoding applied per `site.outputPathEncoding` before writing to disk

## Route Conflict Detection

Bukit validates route uniqueness at two points during the build, and via `bukit doctor`.

### Content Page Conflicts

Two content pages that generate the same **URL** or **outputPath** cause the build to fail immediately with a `ConfigException`. The error message includes the conflicting item ids, titles, slugs, URLs, and output paths.

```yaml
# site.yaml — example: two posts with slug=same
# Error: Route conflict on url: /blog/same/. Conflicting routes: id=post1, ...; id=post2, ...
```

### Derived Page vs Content Page Conflicts

Derived pages (pagination, archive, taxonomy) may produce URLs or output paths that collide with content pages or with each other. Controlled by `site.deriveConflictPolicy`:

| Policy | Behavior |
|--------|----------|
| `fail` (default) | Throw `InvalidOperationException` and abort |
| `warn` | Skip the conflicting derived page, log a warning, continue |
| `last-wins` | Accept the derived page, overwrite the earlier route |

Detection runs in two stages:
1. **Per-plugin** — `PluginRunner.ApplyDeriveConflictPolicy` checks each derived page against existing routes
2. **Final validation** — `RouteInventoryValidator.ValidateFinalRoutes` checks the complete route inventory before rendering

Content-page-vs-content-page conflicts are **always fail** — `deriveConflictPolicy` only governs derived-page conflicts.

### Doctor Validation

`bukit doctor` runs the same content route validation without a full build, reporting conflicts early:

```
✖ Route inventory error
Route conflict on url: /blog/same/. Conflicting routes: ...
```

## Route Path Utilities

All routing logic shares a common set of utilities in `RoutePathBuilder`:

| Method | Purpose | Example |
|--------|---------|---------|
| `NormalizeUrl(url)` | Ensure leading/trailing slashes | `"blog"` → `"/blog/"` |
| `NormalizeListRoute(url)` | List route normalization (defaults to `/`) | `""` → `"/"` |
| `BuildOutputPathFromUrl(url, encoding)` | URL → output path with `index.html` | `"/blog/"` → `"blog/index.html"` |
| `NormalizeOutputPath(path, encoding)` | Apply encoding to segments | `"my page"` → `"my-page"` (slug) |

These utilities are used by the route generator, all built-in plugins (pagination, archive, taxonomy), the list route builder, and the i18n output merger — ensuring consistent output path generation everywhere.

### SlugHelper (Shared)

`Bukit.Shared.SlugHelper.Slugify()` provides consistent slug generation across the codebase:

| Feature | Description |
|---------|-------------|
| Basic slug | Alphanumeric preserved, separators compressed to `-` |
| Accented Latin | Unicode NFD decomposition: `é`→`e`, `ñ`→`n` |
| Ligatures | `ß`→`ss`, `æ`→`ae`, `œ`→`oe`, `ø`→`o` |
| CJK | Chinese/Japanese/Korean characters retained as-is |

Used by: taxonomy terms, SEO URLs, file system output paths.

## Common Errors

| Error | Cause | Fix |
|------|------|------|
| `Route conflict on url` (build fail) | Multiple content items generate same URL | Ensure unique slugs or disambiguate permalink patterns |
| `Route conflict on outputPath` (build fail) | Multiple content items write to same output path | Ensure unique `route.outputPath` values |
| `route conflict` from plugin (build fail) | Derived page URL/outputPath collides with existing route | Change `deriveConflictPolicy` to `warn` or `last-wins`, or adjust routing |
| Route inventory error (doctor) | Route conflicts detected by `bukit doctor` | Fix conflicting slugs/URLs/permalinks before building |
| Permalink generates unexpected URL | Placeholder typo | Use `{slug}` not `{Slug}` or `{SLUG}` |
| `listRoute must start with '/'` | listRoute doesn't start with `/` | Change to `/articles/` |
| `permalink must include {slug}` | Permalink missing {slug} placeholder | Add `{slug}` to the pattern |
| Chinese characters in URL truncated on Windows | Output path encoding not set | Set `site.outputPathEncoding: slug` or `sanitize` |
| Collection matched incorrectly | collection or type field name mismatch | Check content metadata and site.collections key names |
| URL from partial override differs from expected | OutputPathEncoding applied differently than expected | Verify `site.outputPathEncoding`; outputPath is auto-derived from url |
