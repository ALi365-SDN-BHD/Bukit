# 08 Themes & Templates: What Your Site Looks Like, How Fields Are Used in Templates

A theme determines the visual appearance and page structure of your site. As a general user, you will typically do three things:

1. Choose/switch a theme
2. Adjust theme parameters (e.g., brand name, navigation toggles, SEO snippets)
3. Make small template changes (e.g., homepage layout, footer content, insert analytics code)

## What Directories Make Up a Theme

A theme typically contains three types of directories (relative to the directory containing `site.yaml`):

- `layouts`: Templates (Scriban syntax)
- `assets`: Resources copied to the output directory during build (e.g., CSS)
- `static`: Static files copied as-is to the output directory (optional)

In-repo example themes:

- `examples/starter/layouts/` + `examples/starter/assets/` (the default starter structure)
- `examples/starter/themes/alt/`
- `examples/starter/themes/seo-best-practice/`

`bukit init <dir>` now creates `themes/starter/` with the same content-site starter design: reusable partials, card lists, pagination/search/taxonomy templates, and a `bukit.templates.yaml` capability manifest.

## Method A: Switch Themes Using themes/&lt;name&gt; (Recommended)

### Config Syntax

```yaml
theme:
  name: alt
  params:
    brand: my-site
```

### CLI: List and Switch Themes

Create a starter-based custom theme and switch to it:

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme create custom --config site.yaml --brand "My Site" --primary-color "#0b5fff" --accent-color "#0f7b6c" --use
```

Create from an existing local theme:

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme create custom --from alt --config site.yaml
```

List `themes/<name>` under the project root:

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme list --config site.yaml
```

Write back to config (set `theme.name`):

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme use alt --config site.yaml
```

## Method B: Maintain Templates Directly in the Site Root (for single-site quick edits)

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

You can directly edit template files under `layouts/`.

## Recommended Starter Customization Path

Start with the generated starter theme and make changes in this order:

1. Edit `theme.params` in `site.yaml` for simple branding:

```yaml
theme:
  name: starter
  params:
    brand: My Site
    footer_text: My Site
```

2. Edit `assets/style.css` for visual tokens such as `--primary`, `--accent`, spacing, and typography.
3. Edit `layouts/partials/header.html` and `layouts/partials/footer.html` for site chrome.
4. Edit page templates only when layout behavior changes: `layouts/pages/index.html`, `list.html`, `post.html`, and `page.html`.

The starter also includes optional templates for generated features:

- `layouts/pages/pagination.html`
- `layouts/pages/taxonomy-index.html`
- `layouts/pages/taxonomy-term.html`
- `layouts/pages/search.html`

For a new reusable theme, prefer `theme create <name>` first, then customize the generated files. Use `--force` only when intentionally replacing an existing theme directory.

## What Variables Can Be Used in Templates (Most Common for Users)

You don't need to understand the engine's internal model; just remember four kinds of objects:

- `site`: Site info and global data (`site.title/site.baseUrl/site.modules...`)
- `page`: Info about the current page/article (`page.title/page.slug/page.contentHtml/page.fields...`)
- `pages`: Page collection in list pages (common on homepage, blog list, page list)
- `paginator` (if your theme/page has pagination): Pagination info (see: [10 Built-in Features & Output](./10-built-in-features.md))

### 1) Read Site Info

```scriban
<h1>{{ site.title }}</h1>
```

### 2) Read Custom Fields (Markdown/Notion universal)

```scriban
{{ if page.fields.seo_title }}
  <title>{{ page.fields.seo_title.value }}</title>
{{ end }}
```

### 3) Read Theme Parameters (theme.params)

In your `site.yaml`:

```yaml
theme:
  params:
    brand: starter
    showNewsletter: true
```

In templates, Bukit exposes `theme.params` as `site.params`:

```scriban
{{ if site.params.showNewsletter }}
  <section class="newsletter">…</section>
{{ end }}
```

If you're unsure how the current theme exposes parameters, the safest approach is to:

- Search for `params` usage within the theme templates
- Or compare with example themes: `examples/starter/themes/*/layouts/`

### 4) Read Modules (site.modules)

When you enable sources with `mode: data`, modules are injected into `site.modules.<type>[]`:

```scriban
{{ for b in site.modules.banner }}
  <a href="{{ b.fields.link.value }}">
    <img src="{{ b.fields.image.value }}" alt="{{ b.title }}" />
  </a>
{{ end }}
```

See Modules data modeling and examples: [09 Modules Structured Data](./09-modules-data.md).

### 5) Look Up Page Details by pageId (site.data.pages_by_id)

When you have a page id in a template (e.g., a Notion relation's pageId) and want to get that page's URL/title etc., you can use the index injected by the built-in plugin:

- `site.data.pages_by_id[pageId]` → `{ id, title, url, slug, type, publish_date, summary, fields }`
- pages-index is content-source agnostic: Markdown/Notion/multi-source sources can all use this index
- The index is generated during the build phase; template reads do not trigger API requests

Notion relation completion (optional):
- If the relation points to a page not within this site's output scope, you can enable pages-index's Notion completion capability to include those pages in the index and provide `external_url` (Notion URL).

Example:

```scriban
{{ p = site.data.pages_by_id[pid] }}
{{ if p }}
  {{ if p.url }}
    <a href="{{ site.base_url }}{{ p.url }}">{{ p.title }}</a>
  {{ else }}
    <a href="{{ p.external_url }}">{{ p.title }}</a>
  {{ end }}
{{ end }}
```

## Common Modification Checklist (with examples)

### 1) Edit Homepage Layout

Common file:

- `layouts/pages/index.html`

See example: `examples/starter/layouts/pages/index.html`.

If you loop over `pages` on the homepage or list pages:

- Prefer using `p.title`, `p.summary`, `p.publish_date`
- Only use `p.content` when you explicitly need body snippets

When you genuinely rely on `p.content`, there are two approaches:

1. Set `build.listPageContentMode: always` in `site.yaml`
2. Explicitly declare that the template needs list page body content in `layouts/bukit.templates.yaml`

`bukit.templates.yaml` can declare not only body content dependencies but also record template capabilities such as pagination, taxonomy, search summary snippets, etc. The current build process already validates this file's format and template paths.

### 2) Edit Header/Footer (partials)

Common files:

- `layouts/partials/header.html`
- `layouts/partials/footer.html`

### 3) Insert Analytics Code / Meta Tags

Usually done in the base layout:

- `layouts/layouts/base.html`

SEO-related advice: [11 Multilingual & SEO](./11-i18n-seo.md) and the `seo-best-practice` example theme.

## Common Errors and Fixes

- Missing template file: build reports "cannot find template/layout" → check if `theme.name` exists and the directory structure is complete
- CSS/resource 404: often caused by `site.baseUrl` misconfiguration or templates not prepending baseUrl (see: [13 Deploy GitHub Pages](./13-deploy-github-pages.md))
- Empty field: template reads `page.fields.xxx` but the content does not provide that field → add the field in the content or add `if` guards
- `p.content` is empty in list pages: not necessarily because the content isn't loaded; it could be that `build.listPageContentMode` is `never`, or the current theme hasn't declared that the list template needs body content
