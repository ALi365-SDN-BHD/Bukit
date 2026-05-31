---
name: bukit-design-tokens
description: Use when creating or customizing a Bukit theme's design system — defining CSS variables, color palettes, typography scales, spacing systems, dark mode, or when wanting to improve the visual consistency of a Bukit site

status: stable
since: "v3.0.0"
verified_by:
  - "src/Bukit.Cli/Commands/ThemeCommand.cs"
source_anchors:
  - "src/Bukit.Cli/Commands/ThemeCommand.cs"
guide_chapters:
  - "guide/user/08-themes-templates.md"
---

# Bukit Design Tokens

## Overview

Design tokens are the visual atoms of a Bukit theme — CSS custom properties that define colors, typography, spacing, shadows, and more. A well-designed token system makes your theme consistent, maintainable, and easy to customize.

**REQUIRED BACKGROUND:** Theme structure and CSS file location in bukit-theme. Template syntax in bukit-templating.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "设计令牌"、"CSS 变量"、"设计系统"、"调色板"、"深色模式"、"配色方案" |
| English | "design tokens", "CSS variables", "design system", "color palette", "dark mode", "typography scale" |
| Bahasa Melayu | "token reka bentuk", "pembolehubah CSS", "sistem reka bentuk", "palet warna", "mod gelap" |

## Quick Start: Complete Token System

```css
:root {
  /* === Colors === */
  --color-bg: #fbfaf8;
  --color-surface: #ffffff;
  --color-surface-muted: #f3f1ed;
  --color-text: #202124;
  --color-text-muted: #66615b;
  --color-border: #ded9d0;
  --color-primary: #0b5fff;
  --color-primary-hover: #0846b8;
  --color-accent: #0f7b6c;

  /* === Typography === */
  --font-family-base: system-ui, -apple-system, sans-serif;
  --font-family-heading: var(--font-family-base);
  --font-family-mono: "SFMono-Regular", Consolas, monospace;
  --font-size-xs: 0.75rem; --font-size-sm: 0.875rem; --font-size-base: 1rem;
  --font-size-lg: 1.125rem; --font-size-xl: 1.25rem; --font-size-2xl: 1.5rem;
  --font-size-3xl: 2rem; --font-size-4xl: 2.5rem;
  --font-weight-normal: 400; --font-weight-bold: 700;
  --line-height-tight: 1.2; --line-height-base: 1.65;

  /* === Spacing (4px base) === */
  --spacing-1: 4px; --spacing-2: 8px; --spacing-3: 12px; --spacing-4: 16px;
  --spacing-5: 20px; --spacing-6: 24px; --spacing-8: 32px; --spacing-10: 40px;
  --spacing-12: 48px; --spacing-16: 64px; --spacing-20: 80px;

  /* === Layout === */
  --content-max: 760px; --wide-max: 1080px;

  /* === Borders === */
  --radius-sm: 4px; --radius-md: 8px; --radius-lg: 12px; --border-width: 1px;

  /* === Shadows === */
  --shadow-xs: 0 1px 2px rgba(0,0,0,0.05);
  --shadow-sm: 0 1px 3px rgba(0,0,0,0.1);
  --shadow-md: 0 4px 6px rgba(0,0,0,0.1);
  --shadow-lg: 0 10px 15px rgba(0,0,0,0.1);

  /* === Transitions === */
  --transition-fast: 150ms ease; --transition-base: 300ms ease;

  /* === Z-Index === */
  --z-header: 100; --z-dropdown: 200; --z-modal: 300;
}
```

## Color Palette Generation

### Method: Single Brand Color → Full Palette
Given a primary color, generate: Primary Hover (darken 20%), Accent (complementary), Background (light neutral), Text (dark neutral), Muted (desaturated mid-tone), Border (light warm gray).

### 8 Curated Palettes
- **Ocean Blue**: primary=#0b5fff accent=#0f7b6c bg=#fbfaf8 text=#202124 (professional)
- **Warm Earth**: primary=#b45309 accent=#0d9488 bg=#faf7f2 text=#2d2a22 (organic)
- **Midnight Ink**: primary=#1e1b4b accent=#e11d48 bg=#f8f9fb text=#1a1a2e (elegant)
- **Forest Calm**: primary=#166534 accent=#ca8a04 bg=#f7faf5 text=#1a2e1a (peaceful)
- **Modern Slate**: primary=#2563eb accent=#7c3aed bg=#fafafa text=#18181b (clean)
- **Sunset Warm**: primary=#ea580c accent=#9333ea bg=#fefbf6 text=#292524 (energetic)
- **Nordic Light**: primary=#475569 accent=#0d9488 bg=#f5f7fa text=#1e293b (minimal)
- **Rose Gold**: primary=#be185d accent=#b45309 bg=#fdf2f8 text=#4a1942 (elegant)

## Typography System

### Font Pairing
| Style | Heading | Body |
|---|---|---|
| Modern Sans | Inter, DM Sans | System UI, Inter |
| Classic Serif | Georgia, Lora | Georgia, Charter |
| Tech/Mono | DM Mono, JetBrains Mono | System UI |
| Minimalist | System UI | System UI |

### Typography Scale (ratio 1.25)
xs(0.75rem) → sm(0.875rem) → base(1rem) → lg(1.125rem) → xl(1.25rem) → 2xl(1.5rem) → 3xl(2rem) → 4xl(2.5rem)

## Spacing System

4px base grid. Always use `--spacing-*` tokens — never magic numbers:
```css
/* DO: */ .card { padding: var(--spacing-5); }
/* DON'T: */ .card { padding: 19px; }
```

## Dark Mode

### Auto (prefers-color-scheme)
```css
@media (prefers-color-scheme: dark) {
  :root {
    --color-bg: #1a1a2e; --color-surface: #16213e;
    --color-text: #e5e5e5; --color-text-muted: #9ca3af;
    --color-primary: #60a5fa; --color-accent: #34d399;
  }
}
```

### Manual Toggle
```html
<button onclick="document.documentElement.classList.toggle('dark')">🌓</button>
```
```css
.dark { --color-bg: #1a1a2e; --color-text: #e5e5e5; /* ...all dark tokens */ }
```

## Token Patterns by Site Type

- **Blog**: 680-760px content, generous line-height, serif/hybrid fonts, subtle shadows
- **Docs**: wider layout (1200px with sidebar), dense typography, sans-serif, sticky header
- **Portfolio**: full-width possible, large images, dark backgrounds, smooth hover transitions
- **Landing**: extra-wide hero, large display type, bold primary, 80px+ section gaps

## Token Inheritance & Deep Merge

When a child theme extends a parent theme via `extends` in theme config, design tokens are merged using **deep merge** — child tokens override parent at the leaf level while preserving parent's sibling keys.

### Configuration

```yaml
theme:
  name: my-child-theme
  extends: parent-theme-name
```

### Merge Rules

- **Child priority**: child `tokens.yaml` values override parent with the same key
- **Parent supplement**: keys not defined in child are inherited from parent
- **Deep tree merge**: nested token structures (dot-separated keys like `brand.primary`) are reconstructed into a tree and merged recursively

### Example

Parent `tokens.yaml`:
```yaml
colors:
  brand:
    primary: "#000000"
    secondary: "#333333"
```

Child `tokens.yaml`:
```yaml
colors:
  brand:
    primary: "#ff0000"
```

Result after `DeepMerge`:
- `brand.primary` → `#ff0000` (child overrides)
- `brand.secondary` → `#333333` (inherited from parent)

### Flat Format Compatibility

Both flat and nested YAML formats are supported. The loader automatically flattens nested structures to dot-separated keys before merging:

```yaml
# These two are equivalent after loading:
colors:
  brand.primary: "#ff0000"
  brand.secondary: "#333333"

colors:
  brand:
    primary: "#ff0000"
    secondary: "#333333"
```

## Integration with theme.params

```yaml
theme:
  params:
    primary_color: "#0b5fff"
    accent_color: "#0f7b6c"
    font_family: "Inter, system-ui, sans-serif"
```

In base.html:
```html
<style>
:root {
  --color-primary: {{ site.params.primary_color }};
  --color-accent: {{ site.params.accent_color }};
  --font-family-base: {{ site.params.font_family }};
}
</style>
```

---

## External Framework Integration

Design tokens work alongside external CSS/JS frameworks. Bukit's `style.css` loads last, enabling token values to override framework defaults.

### Loading Strategy

```html
<!-- 1. External framework CSS (Tailwind, DaisyUI, Bootstrap, etc.) -->
<script src="https://cdn.tailwindcss.com"></script>

<!-- 2. Bukit CSS with design tokens (loads last, overrides framework) -->
<link rel="stylesheet" href="{{ site.base_url }}/assets/style.css" />
```

### Token Compatibility with Tailwind

| Bukit Token | Tailwind Equivalent | Notes |
|---|---|---|
| `--color-primary` | `text-primary-*` | Bukit tokens define semantic meaning |
| `--font-size-*` | `text-sm/lg/xl/...` | Bukit tokens follow modular scale (1.25) |
| `--spacing-*` | `p-*/m-*/gap-*` | Bukit uses 4px base grid, Tailwind uses 4px base — compatible |
| `--radius-*` | `rounded-sm/md/lg` | Similar ranges, Bukit tokens are overridable |

### Example: Tailwind + Bukit Hybrid

```yaml
theme:
  params:
    external_css:
      - "https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700"
      - "https://cdn.jsdelivr.net/npm/modern-normalize/modern-normalize.min.css"
    primary_color: "#7c3aed"
    font_family: "Inter, system-ui, sans-serif"
```

For layout (grid, flex, spacing) use Tailwind. For theming (colors, fonts, content styles) use Bukit tokens.

```html
<div class="grid grid-cols-1 md:grid-cols-3 gap-6">

  <!-- Bukit tokens for theming -->
  <article class="card" style="color: var(--color-primary)">
    <h2 class="card-title">{{ item.title }}</h2>
  </article>

</div>
```

### Accessing Tokens from External Framework JS

When Alpine.js or other JS frameworks need design token values:

```html
<script>
  const styles = getComputedStyle(document.documentElement);
  const primaryColor = styles.getPropertyValue('--color-primary');
  // Use with Alpine, htmx, etc.
</script>
```

## Common Token Issues: Symptom-Cause-Fix

| Symptom | Likely Cause | Fix |
|---|---|---|
| CSS variable appears unset in the browser | Token is defined in `tokens.yaml` but the generated CSS is not included, or the variable prefix differs from the template usage | Verify the theme CSS is loaded after framework CSS and match generated names such as `--color-primary`, `--font-size-base`, and `--spacing-section-y` |
| Dark mode changes only some colors | Light and dark token sets do not define the same semantic variables | Mirror every semantic color token in the dark-mode block, especially background, surface, text, muted text, border, primary, and accent |
| Components look inconsistent across pages | Components use raw hex values or spacing literals instead of semantic tokens | Replace hard-coded values with shared tokens and centralize component styling around `--color-*`, `--spacing-*`, `--radius-*`, and `--shadow-*` |
| Child theme overrides one token but loses sibling tokens | Flat and nested token formats are mixed incorrectly, or inheritance expectations do not match deep-merge behavior | Keep token keys consistent across parent and child themes and remember that child leaf values override parent leaf values while sibling keys are inherited |
| Tailwind, Bootstrap, or another framework overrides Bukit styling | External framework CSS loads after the Bukit theme CSS | Load external frameworks first and load Bukit's `style.css` last so token-backed theme styles win the cascade |
| Text contrast is poor in dark mode | Primary/accent tokens were reused from light mode without contrast checks | Choose separate dark-mode brand tokens and verify contrast for text, links, badges, borders, and focus states |
| Spacing scale feels uneven | Mixed naming such as `--space-*` and `--spacing-*`, or ad hoc pixel values were introduced | Pick one naming family per theme, prefer the documented `--spacing-*` scale, and refactor component CSS to use that scale consistently |
| Font tokens do not apply to headings | Heading styles reference a raw font stack or a different variable name | Point heading rules to `var(--font-family-heading)` and keep body rules on `var(--font-family-base)` |
| Manual dark-mode toggle conflicts with auto mode | `.dark` class and `prefers-color-scheme` both set the same variables without precedence planning | Choose either auto mode or manual mode; if both are needed, define cascade order so the explicit `.dark` or theme class wins |
| Token values work in CSS but not JavaScript interactions | JS reads token names before CSS loads or reads from the wrong element | Read computed styles from `document.documentElement` after the page has loaded and trim returned values before using them |
