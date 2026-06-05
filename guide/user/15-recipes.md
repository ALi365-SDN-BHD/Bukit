# 15 Recipes: Step-by-Step Guides Organized by "What I Want to Achieve"

This page organizes common needs as "Goal → Config → Data → Command," suitable for following directly.

Think of it as a "cookbook": first replicate one fully to get it working, then modify it according to your needs.

## Recipe 1: Minimal Blog (Markdown)

### Goal

- Blog using only local Markdown
- Generate blog list and article pages (depending on the theme)

### Config (site.yaml)

```yaml
site:
  name: my-blog
  title: My Blog
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
      listTemplate: pages/list.html
content:
  provider: markdown
  markdown:
    dir: content
build:
  output: dist
  clean: true
theme:
  name: alt
logging:
  level: info
```

### Sample Data (content/)

`content/2026-01-first.md`

```markdown
---
collection: post
title: First Article
slug: first
publishAt: 2026-01-01T10:00:00+08:00
tags: [demo]
categories: updates
summary: This is the first article
---

# First Article

Hello Bukit.
```

### Build & Preview

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config site.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c Release -- preview --dir dist --port auto
```

## Recipe 2: Multilingual Site (Markdown Bilingual)

### Goal

- zh-CN + en-US bilingual output
- Each piece of content tagged with language

### Config

Refer directly to the runnable example: `examples/starter/site.i18n.yaml`.

Minimal version:

```yaml
site:
  name: my-i18n
  title: My i18n Site
  baseUrl: /
  language: zh-CN
  languages: [zh-CN, en-US]
  defaultLanguage: zh-CN
  timezone: Asia/Shanghai
  collections:
    page:
      permalink: /{slug}/
      template: pages/page.html
content:
  provider: markdown
  markdown:
    dir: content
build:
  output: dist
  clean: true
theme:
  name: alt
```

### Sample Data

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

## Recipe 3: Corporate Homepage (Modules Data + Theme)

### Goal

- Homepage assembled from banner/features/faq/pricing/footer modules
- Module content managed from `data/`, templates read `site.modules.*`

### Config

Refer directly to the runnable example: `examples/starter/site.modules.yaml`.

### Sample Data (Replicating Three Blocks)

`data/banner-1.md`

```markdown
---
type: banner
title: Banner 1
order: 1
locale: zh-CN
image: https://example.com/banner.png
link: https://example.com/
---
```

`data/features-main.md`

```markdown
---
type: features
title: Core Capabilities
order: 10
locale: zh-CN
f1_title: Fast
f1_desc: Get started in 10 minutes
f2_title: Controllable
f2_desc: Config-driven, templates extensible
---
```

`data/footer-main.md`

```markdown
---
type: footer
title: Footer
order: 100
locale: zh-CN
copyright: "© 2026 My Site"
---
```

### What the Template Should Do

In the theme template, read:

- `site.modules.banner`
- `site.modules.features`
- `site.modules.footer`

Example see: [09 Modules Structured Data](./09-modules-data.md).

## Recipe 4: Notion as CMS (Render Only Published)

### Goal

- Content maintained by operations in a Notion database
- Render only content where Published=✅

### Config (site.yaml)

```yaml
site:
  name: notion-site
  title: Notion Site
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
content:
  provider: notion
  notion:
    databaseId: "your-database-id"
    filterProperty: Published
    filterType: checkbox_true
    sortProperty: PublishAt
    sortDirection: descending
    fieldPolicy:
      mode: whitelist
      allowed: [seo_title, seo_desc, cover, reading_time]
build:
  output: dist
  clean: true
theme:
  name: alt
```

> **Recommended: declare site.collections and coordinate with Notion Collection field.** It is recommended to add the `site.collections` node in site.yaml and create a `Collection` field (select type) in the Notion database, so the engine prioritizes collection-driven routing.

### Key Run Points

- Set the `NOTION_TOKEN` environment variable locally first
- Then run `doctor` and `build`

Details see: [06 Content Notion](./06-notion-content.md).

## Recipe 5: Multi-Site (Maintain main + blog in the Same Repo)

### Goal

- Repo root is the main site
- `sites/blog.yaml` is the blog site

### Steps

1. Write the blog config in `sites/blog.yaml` (can reference `examples/starter/sites/blog.yaml`)
2. Build the blog:

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --site blog --clean --site-url https://example.com
```

## Recipe 6: GitHub Pages Project Repo Deployment (Fixing Resource 404)

### Goal

- Deploy to `https://<owner>.github.io/<repo>/`
- Pages and resources all load correctly

### Key Point

Must pass during build:

```bash
--base-url /<repo> --site-url https://<owner>.github.io/<repo>
```

Full instructions see: [13 Deploy GitHub Pages](./13-deploy-github-pages.md).
