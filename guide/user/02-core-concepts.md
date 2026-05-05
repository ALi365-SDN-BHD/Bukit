# 02 Core Concepts: What You Configure, What the Engine Does

This page explains Bukit's core objects from a "user perspective": what files you write, what web pages those files become, and what fields you can use to control the output.

## Understanding the Build Pipeline in One Diagram

```text
site.yaml
  │
  ├─ content (Markdown / Notion / sources)
  │     └─ reads content → normalizes into ContentItem
  │
  ├─ routing (priority via site.collections to determine URL; falls back to type/permalinks compatibility layer)
  │
  ├─ rendering (renders content into HTML using templates)
  │
  └─ plugins (optional: generates sitemap/rss/search, derived pages, etc.)
        ↓
      dist/ (static file output directory)
```

There are only three things to remember:

1. **Where content comes from** (content.provider / sources)
2. **Where each piece of content is output** (prefer configuring via site.collections; compatible via slug/type + route override fields)
3. **What template is used for rendering** (collection specifies template; can also be overridden by template)

## Site Config (site.yaml)

- `site.*`: Site-level information (site name, title, URL, baseUrl, multilingual, SEO output mode, etc.)
- `content.*`: Content sources (Markdown / Notion / multi-source)
- `build.*`: Output directory, whether to clean, whether to render drafts
- `theme.*`: Theme directories and parameters (templates/assets/static files)
- `logging.*`: Log level

For detailed fields, see: [04 Site YAML Config](./04-site-yaml-config.md).

## Content (ContentItem) = A piece of data that "will be rendered / injected into templates"

Regardless of whether your content comes from Markdown or Notion, the engine normalizes everything into "content items." What matters most to you is: **which fields affect site behavior**.

### 1) Meta: Metadata that influences engine decisions (keep it few, keep it stable)

Common Meta keys (you provide them in Markdown Front Matter or Notion fields):

- `collection`: The collection the content belongs to (recommended), corresponds to a key in site.collections, determines routing and template
- `type`: `page` or `post` (compatibility layer; used to decide default routing and template when no collection is configured)
- `slug`: Core component of the URL (generally recommended to keep stable)
- `language`: Language affiliation of the content (used for filtering and linking in multilingual setups)
- `tags` / `categories`: Tags/categories (used to derive list pages)
- `route` / `url` / `outputPath` / `template`: Advanced usage for "overriding default routing/template" (use with caution)

### 2) Fields: Custom fields intended for template consumption (add whatever you want)

The unified entry point for reading fields in templates is:

- `page.fields.<key>.value`
- `page.fields.<key>.type`

For example, if you write `seo_title` in Markdown, you can use it in a template like this:

```scriban
<title>
  {{ if page.fields.seo_title }}
    {{ page.fields.seo_title.value }}
  {{ else }}
    {{ page.title }}
  {{ end }}
  - {{ site.title }}
</title>
```

In Notion mode, whether a field enters `page.fields` is controlled by `fieldPolicy` (see: [06 Content Notion](./06-notion-content.md)).

## Routing: What URL Will a Piece of Content Become?

Recommended approach: Define permalink, template, and listRoute for each collection via `site.collections` (see: [04 Site YAML Config](./04-site-yaml-config.md)).

Compatibility layer explanation (takes effect when site.collections is not configured or a content item lacks a collection):

- `type: page`: Default output to `/pages/<slug>/`
- `type: post`: Default output to `/blog/<slug>/`

You can control the result through the following methods:

- Declare collection rules in site.collections (recommended)
- Specify `collection` in content meta matching a collection key (recommended)
- Change `slug`: alters one segment of the path
- Change `type`: alters the type classification for the compatibility layer's behavior
- Use `route/url/outputPath` override: stronger, but easier to misconfigure (see: [03 Project Structure](./03-project-structure.md) and [14 Troubleshooting](./14-troubleshooting.md))

## Themes & Templates: What Pages Look Like

A theme is essentially three kinds of things:

- layouts: Templates (Scriban)
- assets: Resources copied to the output directory during build (e.g., CSS)
- static: Static files copied as-is (e.g., robots.txt, images)

You can switch themes, override parameters, and read `site.* / page.* / site.modules.*` in templates (see: [08 Themes & Templates](./08-themes-templates.md)).

## Plugins: Generate Extra Files After Build (sitemap/rss/search, etc.)

After the build completes, the engine generates additional artifacts based on configuration and built-in plugins, such as:

- `sitemap.xml`
- `rss.xml`
- `search.json` / `search.index.json`
- Tag/category list pages (and derived pages for tags/categories)

From a user perspective, you only need to know:

- You can use `site.sitemapMode/rssMode/searchMode` to control multilingual output modes
- You can use `site.pluginFailMode` to decide whether a plugin failure interrupts the build

See: [10 Built-in Features & Output](./10-built-in-features.md) and [11 Multilingual & SEO](./11-i18n-seo.md).

## Modules: No Routes Generated, Only "Provide Data" to Templates

Modules are used for the "structured content blocks" very common on company websites and landing pages:

- banner, navigation, features, faq, pricing, footer...

They come from `content.sources[].mode: data`, do not become independent pages, but are grouped and injected into `site.modules.<type>[]` for rendering by homepage/section page templates.

See: [09 Modules Structured Data](./09-modules-data.md).
