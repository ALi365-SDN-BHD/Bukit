# Theme Runtime

Bukit Core 1.0 theme work is filesystem-based. The Core CLI has no dedicated
theme command surface.

Source anchors:

- `src/Bukit.Theme/ThemeManifestLoader.cs`
- `src/Bukit.Engine/ThemePathResolver.cs`
- `src/Bukit.Config/ThemeManifestStrictValidator.cs`

## Directory Contract

```text
themes/<name>/
  theme.yaml
  layouts/
    layouts/base.html
    pages/index.html
    pages/page.html
    pages/post.html
    pages/list.html
    partials/header.html
    partials/footer.html
  assets/
    style.css
  static/
```

Point `site.yaml` at the local theme:

```yaml
theme:
  name: starter
  layouts: layouts
  assets: assets
  static: static
```

## `theme.yaml`

Known root fields are:

`name`, `display_name`, `version`, `engine`, `min_engine_version`,
`description`, `extends`, `capabilities`, `layouts`, `templates`,
`page_templates`, `sections`, `components`, `assets`, `tokens`.

Example:

```yaml
name: starter
display_name: Starter
version: 1.0.0
engine: bukit
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
assets:
  css:
    - assets/style.css
```

## Inheritance

Theme inheritance is declared in `theme.yaml`:

```yaml
extends: parent-theme
```

Site-level theme inheritance fields are not part of Core 1.0.

## Site-Owned Theme Settings

`site.yaml` owns runtime knobs such as:

- `theme.params`
- `theme.shortcodes`
- `theme.components`
- `theme.scss`
- `theme.images`
- `theme.componentValidation`

Use `theme.yaml` for theme metadata and template capability
declarations. Use `site.yaml` for site-specific runtime configuration.

## Verification

```bash
bukit doctor
bukit build
```
