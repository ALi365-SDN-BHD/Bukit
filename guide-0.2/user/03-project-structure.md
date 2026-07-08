# 03 Project Structure: Files, Paths, and Conventions

Bukit resolves project paths from the directory containing the selected config
file. If you pass `--config sites/blog.yaml`, paths in that config are relative
to `sites/` unless the command's `--site` mode resolves a site config from the
repository root.

## Recommended Single-Site Layout

```text
my-site/
  site.yaml
  content/
    pages/
    posts/
  data/
    modules/
  themes/
    starter/
      theme.yaml
      layouts/
        layouts/
        pages/
        partials/
      assets/
      static/
  dist/
```

## Relative Path Base

Given this config:

```yaml
content:
  sources:
    - type: markdown
      markdown:
        dir: content
build:
  output: dist
theme:
  name: starter
```

Bukit resolves:

- `content` as `<config directory>/content`
- `dist` as `<config directory>/dist`
- `starter` as `<config directory>/themes/starter`

Path fields must be relative and must not contain `..` traversal segments.

## Multi-Site Layout

For more than one site in a repository:

```text
repo/
  site.yaml
  sites/
    docs.yaml
    blog.yaml
  content/
  themes/
```

Build a named site with:

```bash
bukit build --site blog
```

Keep shared content and themes at stable paths, then point each site config to
the sources and theme it owns.

## Theme Directory

When `theme.name` is set, Bukit looks under `themes/<name>/`:

```text
themes/starter/
  theme.yaml
  layouts/
  assets/
  static/
```

When you do not use `theme.name`, you may set direct directories:

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

For reusable projects, prefer `themes/<name>/` because it keeps templates and
assets grouped.

## Content Naming

Use stable slugs:

```markdown
---
collection: post
title: Product Update
slug: product-update
language: en
---
```

Changing a slug changes URLs, feeds, sitemap entries, search entries, and links
from other pages. Use redirects or alias output only when the site has already
published old URLs.

## Route Overrides

Prefer collections for routing. Use route overrides only for exceptional pages:

```yaml
---
collection: page
route:
  url: /legal/privacy/
  template: pages/page.html
---
```

Bukit derives the output path from the final URL. Do not try to write a manual
output path in content front matter.

## Output Directory

Use a dedicated output directory such as `dist`. Bukit protects cleaning with an
output marker to avoid deleting unrelated directories.

```bash
bukit clean --dir dist
```

