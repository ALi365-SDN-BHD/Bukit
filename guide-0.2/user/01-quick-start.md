# 01 Quick Start: Build a Minimal Core Site

This quick start uses only Bukit Core 1.0 features. You create the files
manually, validate the config, run diagnostics, build static output, and open a
local preview.

## Prerequisites

- A `bukit` executable on `PATH`, or a local executable you can call as
  `./bukit`.
- Basic command-line, YAML, Markdown, and HTML familiarity.

Check the CLI:

```bash
bukit version
```

## 1. Create the Project Directories

```text
my-site/
  site.yaml
  content/
    hello.md
  themes/
    starter/
      theme.yaml
      layouts/
        layouts/
          base.html
        pages/
          index.html
          page.html
          post.html
          list.html
      assets/
        style.css
      static/
```

## 2. Add `site.yaml`

```yaml
site:
  name: my-site
  title: My Site
  description: A small Bukit Core site.
  url: https://example.com
  baseUrl: /
  language: en
  collections:
    page:
      permalink: /{slug}/
      template: pages/page.html
      listRoute: /
      listTemplate: pages/index.html
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
      listTemplate: pages/list.html
content:
  sources:
    - type: markdown
      name: pages
      mode: content
      collection: page
      markdown:
        dir: content
build:
  output: dist
  clean: true
theme:
  name: starter
logging:
  level: info
```

## 3. Add a Minimal Theme Manifest

File: `themes/starter/theme.yaml`

```yaml
name: starter
version: 1.0.0
engine: bukit
description: Minimal starter theme.
capabilities:
  seo: true
  geo: true
  i18n: true
templates:
  home:
    template: pages/index.html
    required: true
  page:
    template: pages/page.html
    accepts:
      collection: page
  post:
    template: pages/post.html
    accepts:
      collection: post
  list:
    template: pages/list.html
    accepts:
      kind: list
assets:
  css:
    - assets/style.css
```

## 4. Add Minimal Templates

File: `themes/starter/layouts/layouts/base.html`

```html
{{ base_url = site.base_url }}
{{ if base_url == "/" }}{{ base_url = "" }}{{ end }}
<!doctype html>
<html lang="{{ page.language | default site.language }}">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{ if page.seo }}{{ page.seo.title }}{{ else }}{{ page.title }}{{ end }}</title>
  <link rel="stylesheet" href="{{ base_url }}/assets/style.css">
</head>
<body>
  <main>
    {{ content }}
  </main>
</body>
</html>
```

File: `themes/starter/layouts/pages/page.html`

```html
{% layout "layouts/base.html" %}
<article>
  <h1>{{ page.title }}</h1>
  {{ page.content }}
</article>
```

File: `themes/starter/layouts/pages/post.html`

```html
{% layout "layouts/base.html" %}
<article>
  <h1>{{ page.title }}</h1>
  {{ if page.publish_date }}<time>{{ page.publish_date }}</time>{{ end }}
  {{ page.content }}
</article>
```

File: `themes/starter/layouts/pages/index.html`

```html
{% layout "layouts/base.html" %}
<h1>{{ site.title }}</h1>
{{ for item in page.items }}
  <article>
    <h2><a href="{{ item.url }}">{{ item.title }}</a></h2>
    {{ if item.summary }}<p>{{ item.summary }}</p>{{ end }}
  </article>
{{ end }}
```

File: `themes/starter/layouts/pages/list.html`

```html
{% layout "layouts/base.html" %}
<h1>{{ page.title }}</h1>
{{ for item in page.items }}
  <article>
    <h2><a href="{{ item.url }}">{{ item.title }}</a></h2>
  </article>
{{ end }}
```

File: `themes/starter/assets/style.css`

```css
body {
  font-family: system-ui, sans-serif;
  line-height: 1.6;
  max-width: 72rem;
  margin: 0 auto;
  padding: 2rem;
}
```

## 5. Add Content

File: `content/hello.md`

```markdown
---
collection: page
title: Hello Bukit
slug: hello
summary: My first Core page.
language: en
---

# Hello Bukit

This page is rendered from Markdown.
```

## 6. Validate, Build, and Preview

Run from the `my-site` directory:

```bash
bukit config check
bukit doctor
bukit build
bukit preview --dir dist --port auto
```

During active editing, use the LiveReload development server:

```bash
bukit dev
```

`dev` performs an initial build, watches content and active theme inputs,
rebuilds incrementally, and reloads connected browsers.

## Next

- Add more Markdown: [05 Markdown Content](./05-markdown-content.md)
- Use Notion: [06 Notion Content](./06-notion-content.md)
- Configure deployment: [13 Deploy GitHub Pages](./13-deploy-github-pages.md)

