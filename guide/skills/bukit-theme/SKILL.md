---
name: bukit-theme
description: Use when creating or changing a Bukit theme — theme settings in site.yaml, theme directory layout, theme.yaml manifest and template matching, parent themes, static and asset files, tokens, SCSS, image variants, components, sections, and shortcodes.
---

# Bukit Themes

Core themes are local folders. There are no built-in, remote, or registry themes in
Core; theme scaffolding and marketplaces are Labs-only.

## Two setups

| Setup | site.yaml | Directories |
|---|---|---|
| Named theme | `theme: { name: site }` | `themes/site/{theme.yaml, tokens.yaml, layouts/, assets/, static/}`; `theme.yaml` is required |
| Unnamed | `theme: { layouts: layouts }` | `layouts/`, `assets/`, `static/` at the site root; `layouts/theme.yaml` optional |

A site-root `layouts/` folder always overrides theme layouts file by file. With a named
theme, custom `layouts`/`assets`/`static` values resolve from the site root.

## site.yaml `theme`

```yaml
theme:
  name: site
  params:                  # free-form; templates read site.params
    brand: Example
    nav:
      - { title: Blog, url: /blog/ }
  staticTemplate: layouts/static.html   # render static/*.html through this layout
  scss: { enabled: true, entryPoint: css/main.scss, outputDir: assets }
  images: { enabled: true, formats: [webp], sizes: [480, 768, 1200], quality: 80 }
  componentValidation: off  # off|warn|strict (lowercase)
  shortcodes:
    youtube: '<iframe src="https://www.youtube.com/embed/{{ $1 }}" loading="lazy"></iframe>'
```

`theme.name` allows letters, digits, `_`, `.`, `-` only and always means
`<root>/themes/<name>`.

## theme.yaml

Keys are snake_case and validated strictly.

```yaml
name: site
version: 1.0.0          # System.Version: 1.0 or 1.0.0; not 1 or 1.0.0-beta
engine: bukit
extends: base           # optional single parent at themes/base
templates:
  home:           { template: pages/index.html, required: true }
  post:           { template: pages/post.html, accepts: { collection: post } }
  page:           { template: pages/page.html, accepts: { kind: detail } }
  list:           { template: pages/list.html, accepts: { kind: list } }
  taxonomy_index: { template: pages/taxonomy-index.html, accepts: { kind: taxonomy_index } }
  taxonomy_term:  { template: pages/taxonomy-term.html, accepts: { kind: taxonomy_term } }
components:
  card: { template: components/card.html, props: { title: string, url: string } }
sections:
  hero: { template: sections/hero.html, schema: schemas/hero.json }
```

- Required: `name`, `version`, `engine: bukit`. A named theme without `theme.yaml` fails.
- Content pages take the first matching `templates` entry in YAML order, so put
  collection- or type-specific entries before a generic `kind: detail`.
- Include taxonomy templates if any content uses `tags` or `categories`.
- `assets.css/js`, `capabilities`, `layouts`, `page_templates`, and `tokens` are
  validated but do not inject anything. Link CSS and JS in your layout yourself.
- Do not use `sections.<name>.plugin`; the loader rejects it.

## Files

- `static/` → copied to the output root (parent first, child wins). Only the active
  theme's `static/` is used: with a named theme the project-root `static/` is ignored. `static/404.html`
  becomes `/404.html`. Dot files are skipped unless `build.publishDotFiles: true`.
- `assets/` → `/assets/...`.
- `static/*.html` is rendered through `theme.staticTemplate` when set
  (`about.html` → `/about/`).
- Two sources writing the same output path fail with `Asset output collision at
  '<path>'` (exit 1); case-only differences collide too.

## Tokens

`<themeRoot>/tokens.yaml` (groups `colors`, `font`, `radius`, `spacing`, `layout`)
generates `/assets/css/theme-tokens.css` with CSS variables. Link it yourself. Parse
errors are ignored silently, so check the generated file. Tokens are read only when
`theme.yaml` is present; for an unnamed theme both files live in `layouts/`.

## SCSS

Requires the Dart Sass CLI (`sass` or `dart-sass`) on PATH. Without it the build only
warns `event=scss.skip reason=sass_cli_not_found` and produces no CSS. Compile errors
are warnings too. `assets/css/main.scss` → `/assets/css/main.css`. Without
`entryPoint`, every `.scss` file (partials included) is compiled.

## Images

With `theme.images.enabled: true`, JPG/PNG under `/assets/` and media uploads get
`name-<width>w.<ext>` and `.webp` variants for configured widths smaller than the
source, and plain `<img>` tags (without `srcset`) are rewritten to `<picture>`.
Variant failures fail the build under `site.pluginFailMode: strict`.

## Components, sections, shortcodes

- `{{ comp.render "card" item }}` with theme.yaml components (object argument), or
  `{{ comp.render "name" "a" "b" }}` with site.yaml `theme.components` (positional props).
  If theme.yaml declares any `components`, `comp.render "name"` sees only those and every
  site.yaml component becomes unreachable, so use one source per site. theme.yaml
  components are not inherited from a parent theme.
- theme.yaml component and section templates resolve under `<themeRoot>/layouts/`. For
  an unnamed theme the root is the layouts folder itself (`layouts/layouts/...`), so
  prefer a named theme when you use them. site.yaml `theme.components` templates use
  the normal layout lookup instead.
- `{{ render_section {type: "hero", variant: "dark", props: {...}} }}`; schemas are
  checked when `componentValidation` is `warn` or `strict`.
- Shortcodes: `{% youtube "abc123" %}` anywhere in the final HTML, including Markdown
  bodies. Arguments must be quoted.

## Check

```bash
bukit doctor     # theme manifest, templates, includes, assets
bukit build
```
