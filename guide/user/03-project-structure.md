# 03 Project Structure & Conventions: Where to Put Files, How Relative Paths Work

This page addresses two high-frequency questions:

1. "Where should I put content, themes, and assets?"
2. "What is `dir: content` in the config relative to?"

## Recommended Minimal Directory Structure

Take a Markdown site as an example:

```text
my-site/
  site.yaml
  content/            # Markdown content
    about.md
    hello-world.md
  assets/             # Assets (e.g., CSS)
    style.css
  static/             # Static files copied as-is (optional)
    robots.txt
  layouts/            # Theme templates (or use themes/<name>)
    layouts/
      base.html
    pages/
      index.html
      page.html
      post.html
      list.html
    partials/
      header.html
      footer.html
  dist/               # Build output (build.output)
```

A runnable example exists in the repository: `examples/starter/`, with a more complete structure for direct reference.

## "Relative Path Base" (Very Important)

In Bukit, the vast majority of relative paths are resolved relative to **the directory containing the config file** (the directory of `site.yaml`).

For example, if you write:

```yaml
content:
  sources:
    - type: markdown
      name: content
      mode: content
      collection: page
      markdown:
        dir: content
build:
  output: dist
theme:
  layouts: layouts
  assets: assets
```

This means:

- The content directory is `<site.yaml directory>/content`
- The output directory is `<site.yaml directory>/dist`
- The template directory is `<site.yaml directory>/layouts`

This is also why `--config <path>` is critical: it not only specifies the config file, but also establishes the path base.

## Multi-Site: How sites/<name>.yaml Works

When you maintain multiple sites in the same repository (e.g., `main` and `blog`), you can use:

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --site blog --clean
```

It reads `sites/blog.yaml` as the config, but **rootDir is still the current directory** (not the `sites/` directory).

Refer to the example:

- `examples/starter/sites/blog.yaml`

Recommended convention:

```text
repo/
  site.yaml           # Main site config (default)
  sites/
    blog.yaml         # Blog site config
  content/            # Reusable content
  themes/             # Theme collection
```

## Theme Directory Convention: layouts/assets/static

You can place `layouts/assets/static` directly in the site root, or you can collect themes under `themes/<name>/` and switch using `theme.name`.

### Method A: Maintain Templates Directly in the Site

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

### Method B: Switch Themes Using themes/<name> (More Recommended)

```yaml
theme:
  name: alt
```

And place the theme directory under:

```text
themes/
  alt/
    layouts/
    assets/
    static/
```

Runnable examples:

- `examples/starter/themes/alt/`
- `examples/starter/site.theme.yaml`

## Content File Naming & Field Conventions (Suggestions)

### slug (Strongly Recommended to Keep Stable)

- slug is the core fragment of the URL; changing it often means the URL changes
- Recommendation: keep the slug consistent with the filename (e.g., `hello-world.md` → slug `hello-world`)
- If you need multilingual and i18n linking, it is also recommended to maintain a stable `i18n_key` (especially common in Notion)

### collection / type (Routing Match Fields)

> It is recommended to prioritize using `site.collections` to define content collections and routing rules (see [04 Site YAML Config](./04-site-yaml-config.md)).

For routed content, declare a `collection` value that matches a key under `site.collections`, or provide an explicit `template` / `route` in front matter. The `type` field can still be used as metadata or as a theme `templates.*.accepts.type` matching key, but it does not create built-in `page` / `post` routes by itself.

Themes generally distinguish templates and list pages by collection, type, or other `accepts` rules. Add custom values only when your site config or theme declares the corresponding route/template behavior.

### language (Multilingual)

For multilingual sites, each piece of content should explicitly belong to a language:

- Markdown: Write `language: zh-CN` / `language: en-US` in Front Matter
- Notion: Add a `language` field (it will be promoted to meta)

For multilingual output and SEO, see: [11 Multilingual & SEO](./11-i18n-seo.md).

## Advanced: Route Override Fields (Use with Caution)

If you truly need a custom public URL, use route override fields:

- `route.url` or top-level `url`: Specifies the public URL
- `route.template` or top-level `template`: Specifies which template to use
- `outputPath`: Removed in Bukit 1.0; it is derived from the final URL and manual values are rejected

Common consequences of misconfiguring these fields:

- Pages "disappear" (output to unexpected paths)
- Incorrect links in sitemap/rss/search
- GitHub Pages 404 errors (baseUrl/path mismatch)

It is recommended to solve routing needs through `collection` and `slug` first; consult [14 Troubleshooting](./14-troubleshooting.md) when you truly need overrides.
