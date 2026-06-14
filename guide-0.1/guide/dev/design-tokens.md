# Design Tokens Reference

Design Tokens are the visual atoms of a theme, defined in `tokens.yaml` (colors, fonts, border-radius, spacing, layout variables), and automatically generated as CSS custom properties during build.

Implementation reference:
- `src/Bukit.Theme/Models/ThemeTokens.cs`
- `src/Bukit.Theme/ThemeTokensLoader.cs`
- `src/Bukit.Theme/ThemeTokensProcessor.cs`

## tokens.yaml Format

```yaml
colors:
  primary: "#0b5fff"
  accent: "#0f7b6c"
  bg: "#ffffff"
  surface: "#f8fafc"
  text: "#1a1a2e"
  text_muted: "#6b7280"
  border: "#e5e7eb"

font:
  family_base: "'Inter', system-ui, sans-serif"
  family_heading: "'Inter', system-ui, sans-serif"
  size_base: "1rem"
  size_sm: "0.875rem"
  size_lg: "1.125rem"
  size_xl: "1.25rem"
  size_2xl: "1.5rem"

radius:
  sm: "4px"
  md: "8px"
  lg: "12px"
  full: "9999px"

spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "32px"
  section: "64px"

layout:
  content_max: "720px"
  wide_max: "1200px"
  header_height: "64px"
```

### Top-Level Fields

| Field | Description | CSS Prefix |
|---|---|---|
| `colors` | Color variables | `--color-*` |
| `font` | Font-related variables | `--font-*` |
| `radius` | Border radius variables | `--radius-*` |
| `spacing` | Spacing variables | `--spacing-*` |
| `layout` | Layout variables | `--layout-*` |

Each field uses `snake_case` keys; generated CSS variables use `kebab-case` (underscores replaced with hyphens).

### Nested Token Syntax (Recommended)

For deep merge support, tokens can also be written in nested YAML:

```yaml
colors:
  brand:
    primary: "#0b5fff"
    accent: "#0f7b6c"
  neutral:
    bg: "#ffffff"
    text: "#1a1a2e"
```

These are automatically flattened to dot-separated keys during loading (e.g., `brand.primary`, `neutral.bg`).

## CSS Generation Rules

`ThemeTokensProcessor.GenerateCss()` converts tokens to:

```css
:root {
  --color-primary: #0b5fff;
  --color-accent: #0f7b6c;
  --color-bg: #ffffff;
  --color-surface: #f8fafc;
  --color-text: #1a1a2e;
  --color-text-muted: #6b7280;
  --color-border: #e5e7eb;
  --font-family-base: 'Inter', system-ui, sans-serif;
  --font-family-heading: 'Inter', system-ui, sans-serif;
  --font-size-base: 1rem;
  --font-size-sm: 0.875rem;
  --font-size-lg: 1.125rem;
  --font-size-xl: 1.25rem;
  --font-size-2xl: 1.5rem;
  --radius-sm: 4px;
  --radius-md: 8px;
  --radius-lg: 12px;
  --radius-full: 9999px;
  --spacing-xs: 4px;
  --spacing-sm: 8px;
  --spacing-md: 16px;
  --spacing-lg: 24px;
  --spacing-xl: 32px;
  --spacing-section: 64px;
  --layout-content-max: 720px;
  --layout-wide-max: 1200px;
  --layout-header-height: 64px;
}
```

Key conversion rule: `snake_case` → `kebab-case`, prefixed by field name. E.g., `colors.primary` → `--color-primary`.

## Output Path

The generated CSS file is output to:

```
dist/assets/css/theme-tokens.css
```

During build, the engine detects a component-based theme (`theme.yaml` exists) and automatically runs token generation, logging:

```
event=tokens.generated output=dist/assets/css/theme-tokens.css
```

## Token Inheritance & Deep Merge

When a child theme inherits a parent theme via `extends`, tokens are merged using `ThemeTokens.DeepMerge()`:

- **Child priority**: child theme tokens override parent with the same key
- **Parent supplement**: keys not defined in the child are inherited from the parent
- **Deep merge**: nested token structures (dot-separated keys like `brand.primary`) are reconstructed into a tree and merged recursively — a child's `brand.primary` only overrides that specific leaf, preserving parent's `brand.secondary`

### Merge Behavior Comparison

Given parent tokens:
```yaml
colors:
  brand:
    primary: "#000000"
    secondary: "#333333"
```

And child tokens:
```yaml
colors:
  brand:
    primary: "#ff0000"
```

| Merge Mode | Result `brand.primary` | Result `brand.secondary` |
|---|---|---|
| Shallow (`Merge`) | `#ff0000` | Preserved (`#333333`) |
| Deep (`DeepMerge`) | `#ff0000` | Preserved (`#333333`) |

For flat key structures, both modes behave identically. Deep merge provides additional safety for nested structures where intermediate keys may collide with leaf values.

### Loading Flow

1. Load child theme `tokens.yaml`
2. Load parent theme `tokens.yaml` (if `extends` is set)
3. Flatten nested YAML structures into dot-separated keys
4. Call `child.DeepMerge(parent)` — child values override parent at the leaf level

## Using Tokens in Scriban Templates

Tokens are not directly injected into templates as Scriban variables. The recommended approach is to include them via `<link>` in `base.html`:

```html
<link rel="stylesheet" href="{{ site.base_url }}/assets/css/theme-tokens.css" />
```

Or inline CSS custom properties in page templates:

```html
<style>
  .custom-banner {
    background: var(--color-primary);
    padding: var(--spacing-lg);
    border-radius: var(--radius-md);
  }
</style>
```

## Using Tokens in CSS

The theme's `style.css` can directly reference CSS custom properties:

```css
.card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--spacing-md);
}

.card-title {
  color: var(--color-primary);
  font-family: var(--font-family-heading);
  font-size: var(--font-size-lg);
}

.hero {
  max-width: var(--layout-wide-max);
  padding: var(--spacing-section) var(--spacing-lg);
}
```