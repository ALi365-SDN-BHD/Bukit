# 01 Quick Start

Bukit Core does not currently expose a stable scaffold command. A site is a
small directory with `site.yaml`, content files, and a local theme.

## Minimal Layout

```text
my-site/
  site.yaml
  content/
    hello.md
  themes/
    site/
      theme.yaml
      layouts/
        layouts/base.html
        pages/page.html
        pages/list.html
      assets/style.css
      static/
```

## Minimal Config

```yaml
site:
  name: my-site
  title: My Site
  baseUrl: /
  collections:
    page:
      permalink: /{slug}/
      template: pages/page.html
      listRoute: /
      listTemplate: pages/list.html

content:
  sources:
    - type: markdown
      mode: content
      collection: page
      markdown:
        dir: content

theme:
  name: site

build:
  output: dist
  clean: true
```

## Minimal Theme Manifest

`themes/site/theme.yaml` must be a valid Core theme manifest. `doctor` and
`build` treat a missing or invalid manifest as a hard error.

```yaml
name: site
version: 1.0.0
engine: bukit
assets:
  css:
    - assets/style.css
```

## Minimal Markdown

```markdown
---
title: Hello
slug: hello
collection: page
---

# Hello

This page is rendered through `themes/site/layouts/pages/page.html`.
```

## Command Loop

```bash
bukit config check
bukit doctor
bukit build --clean
bukit preview --dir dist
```

Use `bukit dev` for watch-and-reload development. It runs the same build path
as `build`, then serves the output with LiveReload.
