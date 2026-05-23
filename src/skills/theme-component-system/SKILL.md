---
name: theme-component-system
description: Use when working with Bukit's componentized theme system — theme.yaml V2 manifest, sections, components, pageTemplates, data bindings, tokens, theme-catalog.json, section schemas, page composer, and theme inheritance chains. Covers the structured approach for building modular, AI-consumable themes.
---

# Theme Component System

## Overview

The componentized theme system (`ThemeManifestV2`) is Bukit's next-generation theme architecture that replaces the flat `theme.yaml` V1 format. Instead of a simple name/version/params structure, a V2 theme defines **sections**, **components**, **pageTemplates**, **data bindings**, and **design tokens** in a structured, machine-readable format. This enables AI Agents to understand theme capabilities through `theme-catalog.json` and empowers the Page Composer to assemble pages from modular building blocks.

Key concepts:
- **Section**: A self-contained template unit (hero, cta, features, etc.) with optional schema validation, variants, and data source bindings.
- **Component**: A reusable Scriban partial with declared props, invokable via `{{ comp.render "Name" ... }}`.
- **PageTemplate**: A named page-level template with content-type acceptance rules.
- **Data Binding**: Sections can declare data sources (`source`, `sort`, `limit`, `filter`) to auto-resolve content items at render time.
- **Tokens**: Design tokens in `tokens.yaml` (colors, fonts, radius, spacing, layout) that merge across inheritance chains.
- **Inheritance**: Child themes extend parent themes; sections, components, pageTemplates, and tokens cascade (child overrides parent).

**REQUIRED SUB-SKILLS:** `bukit-theme` for theme basics (layout/assets/static), `bukit-templating` for Scriban syntax.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "组件化主题"、"theme.yaml V2"、"section schema"、"theme-catalog.json"、"页面组合器"、"主题继承链"、"tokens.yaml" |
| English | "componentized theme", "theme.yaml V2", "section schema", "theme-catalog.json", "page composer", "theme inheritance chain", "tokens.yaml" |
| Bahasa Melayu | "tema komponen", "theme.yaml V2", "skema seksyen", "theme-catalog.json", "penggubah halaman", "rantaian pewarisan tema", "tokens.yaml" |

## Directory Structure

A componentized theme extends the standard Bukit theme structure with additional metadata and schema files:

```
themes/<name>/
  layouts/                       ← Scriban templates
    layouts/                     ← Layout templates
    pages/                       ← Page templates
    partials/                    ← Partial templates
    sections/                    ← Section templates (hero/, cta/, features/, etc.)
      hero/
        hero.html                ← Section template
        hero.schema.json         ← Section JSON Schema (optional)
      cta/
        cta.html
    components/                  ← Component templates
      cards/
        card.html
  assets/                        ← Processable assets
  static/                        ← Static files
  theme.yaml                     ← V2 manifest (sections, components, pageTemplates, etc.)
  tokens.yaml                    ← Design tokens
  schemas/                       ← Shared JSON Schemas (optional)
```

## theme.yaml Reference (V2)

The V2 manifest is the single source of truth for a componentized theme. It is loaded by `ThemeManifestLoader.Load()` and consumed by `ThemeComponentRegistry`.

```yaml
name: my-theme
version: 2.0.0
display_name: My Componentized Theme
engine: bukit
min_engine_version: ">=3.0.0"
description: A modular theme with sections and components
extends: parent-theme-name
capabilities:
  i18n: true
  seo: true
  geo: false
  dark_mode: true
  search: true
  taxonomy: true
tokens: tokens.yaml
assets:
  css:
    - assets/style.css
  js:
    - assets/behaviors.js
```

### Key Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | **Yes** | Theme identifier |
| `version` | string | No | Semantic version |
| `display_name` | string | No | Human-readable name |
| `engine` | string | No | Engine name (always "bukit") |
| `min_engine_version` | string | No | Minimum engine version constraint |
| `description` | string | No | Theme description |
| `extends` | string | No | Parent theme name for inheritance |
| `capabilities` | object | No | Feature flags (i18n, seo, geo, dark_mode, search, taxonomy) |
| `tokens` | string | No | Path to tokens file (default: `tokens.yaml`) |
| `layouts` | map | No | Custom layout template mappings |
| `page_templates` | map | No | Named page template definitions |
| `sections` | map | No | Section definitions (keyed by section name) |
| `components` | map | No | Component definitions (keyed by component name) |
| `assets` | object | No | CSS/JS asset paths |

### Sections

```yaml
sections:
  hero:
    template: sections/hero/hero.html
    schema: sections/hero/hero.schema.json
    description: Full-width hero banner
    preview: sections/hero/preview.png
    variants:
      centered:
        template: sections/hero/hero--centered.html
        label: Centered
        description: Centered text layout
      split:
        template: sections/hero/hero--split.html
        label: Split
    data:
      source: posts
      limit: 3
      sort: publishAt desc
      filters:
        featured: true
```

| Field | Type | Description |
|-------|------|-------------|
| `template` | string | Path to section Scriban template (relative to `layouts/`) |
| `schema` | string | Path to JSON Schema for props validation (optional) |
| `preview` | string | Path to preview image (optional) |
| `description` | string | Human-readable description |
| `variants` | map | Named variant definitions (each has `template`, `label`, `description`) |
| `data` | object | Data binding (`source`, `mode`, `limit`, `sort`, `filters`) |

### Components

```yaml
components:
  PostCard:
    template: components/cards/post-card.html
    props:
      title: string
      url: string
      cover: string
      summary: string
```

| Field | Type | Description |
|-------|------|-------------|
| `template` | string | Path to component Scriban template |
| `props` | map | Declared props (key → type hint) |

### Page Templates

```yaml
page_templates:
  blog-post:
    template: pages/post.html
    label: Blog Post
    accepts:
      type: post
      collection: blog
    required_fields:
      - cover
      - author
```

| Field | Type | Description |
|-------|------|-------------|
| `template` | string | Path to page template |
| `label` | string | Display label |
| `accepts` | object | Content-type filter (`type`, `collection`) |
| `required_fields` | list | Fields that must exist on the content item |

## tokens.yaml Reference

Design tokens are stored in a separate YAML file, loaded by `ThemeTokensLoader`, and processed by `ThemeTokensProcessor` to generate CSS custom properties. Tokens merge across inheritance chains (child overrides parent).

```yaml
colors:
  primary: "#0b5fff"
  accent: "#0f7b6c"
  background: "#fbfaf8"
  text: "#202124"
  text_muted: "#66615b"
  surface: "#ffffff"
  border: "#ded9d0"

font:
  base: "system-ui, sans-serif"
  heading: "var(--font-base)"
  mono: "'SFMono-Regular', monospace"
  size_base: "1rem"
  size_lg: "1.125rem"
  size_xl: "1.25rem"
  size_2xl: "1.5rem"
  size_3xl: "2rem"
  size_display: "clamp(2rem, 5vw, 4.2rem)"
  weight_normal: "400"
  weight_bold: "700"
  line_height_tight: "1.2"
  line_height_normal: "1.65"

radius:
  sm: "4px"
  md: "8px"
  lg: "12px"
  full: "9999px"

spacing:
  section_y: "64px"
  container_px: "24px"
  card_gap: "24px"

layout:
  content_max: "760px"
  wide_max: "1080px"
```

| Category | CSS Variable Prefix | Example Output |
|----------|---------------------|----------------|
| `colors` | `--color-` | `--color-primary: #0b5fff;` |
| `font` | `--font-` | `--font-size-base: 1rem;` |
| `radius` | `--radius-` | `--radius-md: 8px;` |
| `spacing` | `--spacing-` | `--spacing-section-y: 64px;` |
| `layout` | `--layout-` | `--layout-content-max: 760px;` |

Underscores in key names are converted to hyphens. Tokens can be generated to CSS via `ThemeTokensProcessor.GenerateCss()` or `ThemeTokensProcessor.WriteToFile()`.

### Token Inheritance

When a theme extends a parent, tokens merge with child-overrides-parent semantics:

```csharp
var childTokens = new ThemeTokensLoader().LoadWithInheritance(childRoot, parentRoot);
// child tokens override parent keys; parent keys not in child are inherited
```

## Section Schema Reference

Section schemas are JSON Schema files that validate props passed to section templates. They provide structure and documentation for AI Agents consuming `theme-catalog.json`.

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Hero Section",
  "type": "object",
  "properties": {
    "title": {
      "type": "string",
      "description": "Hero heading text"
    },
    "eyebrow": {
      "type": "string",
      "description": "Small text above the heading"
    },
    "ctaText": {
      "type": "string",
      "description": "Call-to-action button text"
    },
    "ctaUrl": {
      "type": "string",
      "description": "Call-to-action button URL"
    },
    "background": {
      "type": "string",
      "enum": ["light", "dark", "image"],
      "default": "light"
    }
  },
  "required": ["title"]
}
```

Schemas provide:
- **AI Agent guidance**: `theme-catalog.json` extracts `requiredProps` and `optionalProps` from schemas.
- **Runtime validation**: `SectionSchemaValidator` validates props in `warn` or `strict` mode.

## Page Composer Usage

The Page Composer (`PageComposer.Compose()`) merges page-level section definitions with theme section defaults:

```json
[
  { "type": "hero", "props": { "title": "Welcome" } },
  { "type": "features", "source": "features", "limit": 6 },
  { "type": "cta", "props": { "text": "Get Started", "url": "/signup" } }
]
```

The composer:
1. Matches each section type against `theme.yaml` section definitions.
2. Merges props (page props take precedence).
3. Merges data bindings (page `source`/`limit`/`sort`/`filter` override theme defaults).
4. Returns a fully resolved list for the Scriban renderer.

In Scriban, sections are rendered via:

```
{{ render_section section }}
```

Or in list page templates:

```
{{ for section in page.sections }}
  {{ render_section section }}
{{ end }}
```

## Theme Inheritance

Child themes extend parent themes via `extends` in theme.yaml. The `ThemeComponentRegistry` resolves definitions through the inheritance chain:

| Resolution | Logic |
|------------|-------|
| `ResolveSection(name)` | Child → recurse to parent |
| `ResolveComponent(name)` | Child → recurse to parent |
| `ResolveSectionTemplate(name, variant?)` | Child template → parent template |
| `ResolvePageTemplate(name)` | Child pages/ → parent pages/ |
| `ResolveLayoutTemplate(name)` | Child layouts/ → parent layouts/ |
| `GetAllSectionNames()` | Union of child + parent names |
| `GetAllComponentNames()` | Union of child + parent names |
| Tokens (`LoadWithInheritance`) | Child keys override parent keys |

## CLI Commands

### `bukit theme doctor`

Validates the componentized theme:

```bash
bukit theme doctor              # Validates active theme
bukit theme doctor my-theme     # Validates specific theme
```

Checks performed:
- `theme.yaml` exists and parses correctly
- Page templates declared in `page_templates` exist
- Section templates exist (with variant templates)
- Schema files exist for sections that declare them
- Tokens file exists and parses
- Component names are unique
- Unused components (not yet implemented)

### `bukit theme list-components`

Lists all sections and components available in a theme:

```bash
bukit theme list-components              # Active theme
bukit theme list-components my-theme     # Specific theme
```

Output:
```
Sections:
  hero                     Full-width hero banner
  cta                      Call to action
  features                 Feature grid
  ...

Components:
  PostCard                 props: [title, url, cover, summary]
  ...
```

### `bukit theme export-catalog`

Exports `theme-catalog.json` to the `.cache/` directory for AI Agent consumption:

```bash
bukit theme export-catalog              # Active theme
bukit theme export-catalog my-theme     # Specific theme
```

Output: `.cache/theme-catalog.json`

## theme-catalog.json Format

The catalog is a JSON file designed for AI Agent consumption. It describes all sections and components a theme provides, including required/optional props, data sources, and best-fit suggestions.

Generated by `ThemeCatalogWriter.GenerateJson()`:

```json
{
  "theme": "my-theme",
  "version": "2.0.0",
  "description": "A modular theme with sections and components",
  "extends": null,
  "sections": [
    {
      "name": "hero",
      "description": "Full-width hero banner",
      "variants": ["centered", "split"],
      "requiredProps": ["title"],
      "optionalProps": ["eyebrow", "ctaText", "ctaUrl", "background"],
      "dataSources": null,
      "bestFor": ["home page"]
    },
    {
      "name": "cta",
      "description": "Call to action",
      "requiredProps": ["text", "url"],
      "optionalProps": ["style"],
      "dataSources": null,
      "bestFor": ["landing page"]
    }
  ],
  "components": [
    {
      "name": "PostCard",
      "props": {
        "title": "string",
        "url": "string",
        "cover": "string",
        "summary": "string"
      }
    }
  ]
}
```

| Field | Description |
|-------|-------------|
| `theme` | Theme name |
| `version` | Theme version |
| `description` | Theme description |
| `extends` | Parent theme name (or null) |
| `sections[]` | List of section catalog entries |
| `components[]` | List of component catalog entries |

### Section Entry

| Field | Source |
|-------|--------|
| `name` | Section key in theme.yaml |
| `description` | `sections.<name>.description` |
| `variants` | Keys from `sections.<name>.variants` |
| `requiredProps` | Props where schema `required` includes the key |
| `optionalProps` | Props not in `required` |
| `dataSources` | `sections.<name>.data.source` |
| `bestFor` | Inferred from section name (hero→home page, grid→listing page, cta→landing page) |

## Data Resolution (SectionDataResolver)

Sections with `data.source` automatically resolve content items at render time. The `SectionDataResolver.Resolve()` method:

```csharp
SectionDataResolver.Resolve(sectionDef, allPages)
```

### Source Matching

| Source Pattern | Matches |
|----------------|---------|
| `"posts"` | Items with `type: posts` or `collections` containing `posts` |
| `"*"` or `"all"` | All items |
| `"type:post"` | Items with `type: post` |
| `"collection:blog"` | Items with `collections` containing `blog` |
| `"posts,features"` | Items matching any of the comma-separated sources |

### Sorting

- `"publishAt"` / `"publish_at"` / `"date"` — sort by publish date
- `"title"` — sort alphabetically by title
- Append `desc` for descending order (e.g., `"publishAt desc"`)

### Filtering

Filters match against `ContentItem.Fields` values:

```yaml
data:
  source: posts
  filters:
    featured: true
    category: technology
```

Boolean values match boolean fields or string `"true"`/`"false"`.

## Backward Compatibility

The V2 componentized theme system coexists with V1 themes:

- **V1 themes** (`name`, `version`, `params`) continue to work. They are loaded by `ThemeManifest.Load()` from `Bukit.Cli.Commands`.
- **V2 themes** (`name`, `sections`, `components`, `page_templates`, `tokens`) are loaded by `ThemeManifestLoader.Load()` from `Bukit.Theme`.
- Both formats use `theme.yaml` as the manifest file. The loader determines the format based on the presence of V2-specific keys.
- **Templates**: Scriban `{{ include }}` and `{% layout %}` directives work the same in both systems.
- **Components**: The V1 `theme.components` in `site.yaml` (PostCard) is separate from V2 `components` in `theme.yaml`.
- **Theme inheritance** (`extends`) is a V2-only feature.
- **Section schemas** and **data bindings** are V2-only.

## Related Skills

- [bukit-theme](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/bukit-theme/SKILL.md) — Theme basics: layouts, assets, static, theme.yaml V1, theme.params, wizard presets, SCSS, image optimization
- [bukit-templating](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/bukit-templating/SKILL.md) — Scriban template syntax, includes, layouts, variables, functions
- [bukit-design-tokens](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/bukit-design-tokens/SKILL.md) — Design token system, CSS custom properties, color palettes, typography scales
- [bukit-config](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/bukit-config/SKILL.md) — site.yaml configuration, theme config section
- [bukit-cli-reference](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/bukit-cli-reference/SKILL.md) — CLI command reference
