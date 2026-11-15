# 01 Quick Start

Run `bukit new my-site` to create a buildable starter. It writes `site.yaml`,
Markdown content, and a local `themes/starter/`; it does not run git, build, or
make network requests. A site is a small directory with `site.yaml`, content
files, and a local theme.

## Create a Starter

```bash
bukit new my-site
cd my-site
```

Use `--title` and `--language` to set the display title and primary language:

```bash
bukit new my-site --title "My Site" --language zh-CN
```

The starter theme reads its interface text (menu, pagination, search) from
`theme.params.labels` in `site.yaml`. `bukit new` writes Simplified Chinese
labels for `zh` tags (`zh`, `zh-CN`, `zh-Hans-HK`) unless they name
Traditional Chinese (`zh-Hant`, `zh-TW`, `zh-HK`, `zh-MO`), and English labels
otherwise. Edit them there to change the wording.

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
bukit new my-site
cd my-site
bukit config check
bukit doctor
bukit build --clean
bukit preview --dir dist
```

Use `bukit dev` for watch-and-reload development. It runs the same build path
as `build`, then serves the output with LiveReload.
