---
name: bukit-theme
description: Use when authoring Bukit theme directories, `theme.yaml`, theme params, assets, static files, theme inheritance, component definitions, SCSS/image settings, or static-resource troubleshooting.
status: stable
since: "v4.0.0-core1"
verified_by:
  - "tests/Bukit.Theme.Tests/ThemeManifestLoaderTests.cs"
  - "tests/Bukit.Engine.Tests/ThemePathResolverTests.cs"
source_anchors:
  - "src/Bukit-Core/Bukit.Engine/ThemePathResolver.cs"
  - "src/Bukit-Core/Bukit.Config/ConfigValidator.cs"
  - "src/Bukit-Core/Bukit.Config/ThemeManifestStrictValidator.cs"
  - "src/Bukit-Core/Bukit.Theme/ThemeManifestLoader.cs"
  - "src/Bukit-Core/Bukit.Theme/ThemeComponentRegistry.cs"
guide_chapters:
  - "guide/skills/README.md"
---

# Bukit Theme

Bukit Core 1.0 theme work is filesystem-based. The Core CLI does not provide a dedicated theme command surface.

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

Then point `site.yaml` at the theme:

```yaml
theme:
  name: my-theme
  layouts: layouts
  assets: assets
  static: static
```

## `theme.yaml`

Core requires `theme.yaml` at the theme root. Required fields are `name`, `version`, and `engine`.

```yaml
name: my-theme
version: 1.0.0
engine: bukit
display_name: My Theme
description: Clean content theme
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

Known root fields are `name`, `display_name`, `version`, `engine`, `min_engine_version`, `description`, `extends`, `capabilities`, `layouts`, `templates`, `page_templates`, `sections`, `components`, `assets`, and `tokens`.

`theme.params` belongs in `site.yaml`, not in `theme.yaml`.

## Inheritance

Theme inheritance is declared with `extends` in `theme.yaml`. Site-level `theme.extends` is not part of Core 1.0.

```yaml
extends: parent-theme
```

Child templates override parent templates. Missing child templates may fall back through the theme resolver when the parent is valid.

## Authoring Workflow

1. Create `themes/<name>/theme.yaml`.
2. Create `layouts`, `assets`, and `static` directories.
3. Update `site.yaml` `theme.name`.
4. Add or update Scriban templates.
5. Verify:

```bash
bukit doctor
bukit build
```

## Troubleshooting

| Symptom | Check |
|---|---|
| Template not found | `theme.name`, `theme.layouts`, and `theme.yaml.templates` paths |
| CSS missing | `theme.assets`, output `/assets/`, and link paths using `site.base_url` |
| Static file missing | `theme.static` and copied output location |
| Manifest error | Unknown `theme.yaml` fields or missing required fields |
