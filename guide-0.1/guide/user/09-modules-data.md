# 09 Modules (Structured Data): Driving Company Sites / Landing Pages with Data Modules

The purpose of Modules is: **to extract "structured content blocks within a page" from templates and turn them into configurable data**.

A typical company website is often not "many independent pages" but "one homepage + several section pages", where each page is assembled from modules such as banner, navigation, features, faq, pricing, footer. Modules are designed for this need.

See runnable examples:

- Config: `examples/starter/site.modules.yaml`
- Sample data: `examples/starter/data/*.md`

## What You Will Get

- How to configure `mode: data` to inject modules into the template variable `site.modules`
- Recommended module fields and modeling approaches (compatible with Markdown and Notion)
- Multilingual modules authoring (locale)
- 3 copy-ready module examples (banner/nav/faq)

## Step 1: Enable mode=data in sources

```yaml
content:
  sources:
    - type: markdown
      name: content
      mode: content
      collection: page
      markdown:
        dir: content
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
```

This brings a key behavior:

- Content items with `mode: data` **do not generate routes** (no `/pages/...`)
- They are grouped by `type` and injected into `site.modules.<type>[]`

For example, modules with `type: banner` will appear in `site.modules.banner`.

## Step 2: Write Module Data (Markdown Mode)

Module data is also Markdown files, except its `type` represents "module type" rather than "page/post".

### Example 1: banner

File: `data/banner-1.md`

```markdown
---
type: banner
title: Banner 1
order: 1
locale: zh-CN
image: https://example.com/banner-1.png
link: https://example.com/
---

Banner 1 body
```

See runnable example: `examples/starter/data/banner-1.md`.

### Example 2: Navigation (nav)

File: `data/nav-home.md`

```markdown
---
type: nav
title: Home Nav
order: 10
locale: zh-CN
items:
  - text: Home
    href: /
  - text: Blog
    href: /blog/
  - text: About
    href: /pages/about/
---
```

Notes:

- `items` as a "list structure" will enter `page.fields.items.value` (how the theme consumes it depends on field type mapping; fault tolerance in the theme is recommended)
- If you want templates to be more stable, it is recommended to keep "enumerable structures" flat (e.g., multiple fields: `nav_1_text/nav_1_href`), or express them using database structures in Notion

### Example 3: FAQ

File: `data/faq-main.md`

```markdown
---
type: faq
title: FAQ
order: 30
locale: zh-CN
q1: What is Bukit?
a1: A static site engine supporting Markdown/Notion.
q2: Do I need to write code?
a2: No, but you can deeply customize via theme templates.
---
```

## Recommended "Module Field" Conventions (strongly recommended to standardize)

Modules have no enforced schema (it's determined by your theme), but for maintainability, it is recommended that all modules include the following common fields:

| Field | Purpose | Notes |
|---|---|---|
| `type` | Module type (grouping key) | Required; determines which `site.modules.<type>` it gets injected into |
| `title` | Module title | Optional but recommended |
| `order` | Sort order | Recommended numeric, smaller = earlier |
| `locale` | Language (multilingual sites) | e.g., `zh-CN`/`en-US` |
| `enabled` | Toggle (optional) | Used to quickly take down a block of content |

It is recommended that your theme sort by `order`, filter by `locale`, and ignore modules where `enabled=false`.

## Using Modules in Templates (Scriban Examples)

### 1) Render a banner list

```scriban
{{ for b in site.modules.banner }}
  <section class="banner">
    {{ if b.fields.image }}<img src="{{ b.fields.image.value }}" />{{ end }}
    <h2>{{ b.title }}</h2>
    {{ if b.fields.link }}<a href="{{ b.fields.link.value }}">View Now</a>{{ end }}
  </section>
{{ end }}
```

### 2) Filter by locale (pseudocode style; specifics depend on theme utility functions)

If your theme has no encapsulated filter utility, the simplest way is to use `if` in the template:

```scriban
{{ for m in site.modules.faq }}
  {{ if m.meta.locale == site.language }}
    ...
  {{ end }}
{{ end }}
```

You can also store locale in fields (e.g., `fields.locale.value`), following your theme's data conventions.

## Modules Modeling Suggestions in Notion Mode (Example)

If you want operations team members to manage modules in Notion, you can create a Modules database (paired with `mode: data` in sources):

Recommended fields (illustrative):

| Field Name | Type | Description |
|---|---|---|
| `Enabled` | checkbox | Whether enabled |
| `Title` | title | Module title |
| `Type` | select | banner/nav/faq/pricing... (module type) |
| `order` | number | Sort order |
| `locale` | select | zh-CN/en-US |
| `image` | files/url | Banner image etc. |
| `link` | url | Navigation link |

Matching sources config example:

```yaml
content:
  sources:
    - type: notion
      name: modules
      mode: data
      notion:
        databaseId: "db_modules"
        filterProperty: Enabled
        filterType: checkbox_true
        fieldPolicy: { mode: all }
```

## FAQ

### 1) Why don't modules appear in the output directory?

Normal: modules don't generate routes, so you won't see `dist/pages/...`. They only affect page HTML during template rendering.

### 2) Why is `site.modules.banner` empty in the template?

Check:

- Whether modules in sources has `mode: data`
- Whether the module data includes `type: banner`
- Whether the multilingual site is filtered out by locale (e.g., site language is en-US, but you only entered zh-CN modules)

### 3) How do multiple `mode: data` sources merge?

You can configure multiple `mode: data` sources. The engine will load all content items from these sources and inject them into `site.modules` as a unified set:

- All `mode: data` content items will not generate route pages, they only affect template rendering
- Modules are grouped by each content item's `type` (from front matter / Notion properties); same-named types from different sources will merge into the same `site.modules.<type>[]`
- In multi-source mode, each content item's `id` automatically gets a prefix: `<sourceKey>:<sourceId>` (to avoid id conflicts across sources)

Example: 3 data sources (2 markdown data + 1 notion data)

```yaml
content:
  sources:
    - type: markdown
      name: modules_marketing
      mode: data
      markdown: { dir: data/marketing, defaultType: module }
    - type: markdown
      name: modules_product
      mode: data
      markdown: { dir: data/product, defaultType: module }
    - type: notion
      name: modules_ops
      mode: data
      notion:
        databaseId: "db_modules_ops"
        filterProperty: Enabled
        filterType: checkbox_true
        fieldPolicy: { mode: all }
```

Template reading remains unchanged:

```scriban
{{ for b in site.modules.banner }}
  <h2>{{ b.title }}</h2>
{{ end }}
```
