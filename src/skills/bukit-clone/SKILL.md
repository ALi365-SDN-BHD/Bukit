---
name: bukit-clone
description: Clone any website's visual design into a Bukit theme. Use when the user wants to clone/copy a website's appearance, replicate a design, or create a theme from an existing site. This skill handles the full pipeline: browser design token extraction, layout analysis, and CLI-driven theme generation.
description_zh: 将任意网站的视觉设计克隆为 Bukit 主题。当用户想要克隆/复制网站外观、复刻设计或从现有站点创建主题时使用。本技能处理完整流水线：浏览器设计令牌提取、布局分析和 CLI 驱动主题生成。
description_ms: Klon reka bentuk visual mana-mana laman web ke dalam tema Bukit. Gunakan apabila pengguna mahu mengklon/menyalin penampilan laman web, meniru reka bentuk, atau mencipta tema daripada laman sedia ada. Kemahiran ini mengendalikan saluran paip penuh: pengekstrakan token reka bentuk pelayar, analisis susun atur, dan penjanaan tema dipacu CLI.
description_en: Clone any website's visual design into a Bukit theme. Use when the user wants to clone/copy a website's appearance, replicate a design, or create a theme from an existing site. This skill handles the full pipeline: browser design token extraction, layout analysis, and CLI-driven theme generation.
argument-hint: "<url> [--theme <name>]"
user-invocable: true
---

# Bukit Clone Website → Theme

## Overview

Clone any website's visual design as a Bukit theme. This is a two-phase workflow:

1. **Extraction** (you do this): Open the target URL with a browser MCP tool, extract design tokens and page layout, save as `tokens.json` and `layout.json`.
2. **Generation** (CLI does this): Run `bukit clone --tokens tokens.json --layout layout.json --theme <name>` to generate all 17 theme files.

**REQUIRED BACKGROUND:** The theme directory structure and Scriban template conventions are covered in bukit-theme and bukit-templating.
**REQUIRED SUB-SKILL:** Use bukit-cli-reference for command execution details.

## Phase 1: Reconnaissance & Token Extraction

You must use a browser MCP tool (Chrome MCP preferred; Playwright MCP, Browserbase MCP, or Puppeteer MCP also work). Without browser automation, this skill cannot work.

### Step 1.1: Take Screenshots

1. Open `$ARGUMENTS` with browser MCP
2. Take full-page screenshots at **desktop (1440px)** and **mobile (390px)** viewports
3. Save to `docs/design-references/` as reference

### Step 1.2: Extract Design Tokens

Run the following JavaScript via browser MCP console to extract design tokens. Save the output as `tokens.json`.

```javascript
(function() {
  function getStyle(el, prop) {
    try { return getComputedStyle(el)[prop]; } catch { return null; }
  }

  function findEl(selector) {
    return document.querySelector(selector);
  }

  function safeColor(val) {
    if (!val || val === 'rgba(0, 0, 0, 0)' || val === 'transparent') return null;
    return val;
  }

  const doc = document;
  const body = doc.body;
  const card = findEl('.card, article, [class*="card"], [class*="post"]');
  const heading = findEl('h1, h2, h3');
  const meta = findEl('.meta, .date, .subtitle, time, small');
  const link = findEl('a:not(.brand):not(.logo)');
  const button = findEl('button, .btn, [class*="button"], a[class*="button"]');
  const badge = findEl('.badge, .tag, .eyebrow, [class*="badge"], [class*="tag"]');
  const code = findEl('code, pre');

  const tokens = {
    bg: safeColor(getStyle(body, 'backgroundColor')) || '#ffffff',
    surface: safeColor(getStyle(card || findEl('section, [class*="container"]'), 'backgroundColor')) || '#ffffff',
    text: safeColor(getStyle(body, 'color')) || '#202124',
    muted: safeColor(getStyle(meta || findEl('.summary, .description, .subtitle'), 'color')) || '#66615b',
    border: safeColor(getStyle(card || findEl('nav, header'), 'borderBottomColor') || getStyle(card || findEl('hr'), 'borderTopColor')) || '#ded9d0',
    primary: safeColor(getStyle(link || button || findEl('a'), 'color')) || '#0b5fff',
    accent: safeColor(getStyle(badge || findEl('.highlight, [class*="accent"]'), 'color')) || '#0f7b6c',

    radius: getStyle(card || button || findEl('[class*="rounded"]'), 'borderRadius') || '8px',
    contentMax: getStyle(findEl('article, .content, .post-body, [class*="content"]'), 'maxWidth') || '760px',
    wideMax: getStyle(findEl('.container, nav, .wrapper, [class*="container"]'), 'maxWidth') || '1080px',
    shadow: getStyle(card || findEl('.shadow, [class*="shadow"]'), 'boxShadow') || '0 16px 40px rgba(32, 33, 36, 0.08)',

    fontFamily: getStyle(body, 'fontFamily') || 'system-ui, -apple-system, sans-serif',
    headingFontFamily: getStyle(heading || findEl('h1'), 'fontFamily') || null,
    codeFontFamily: getStyle(code || findEl('code'), 'fontFamily') || '"SFMono-Regular", Consolas, monospace',
    googleFontsUrl: null
  };

  // Detect Google Fonts
  const gfLinks = [...document.querySelectorAll('link[href*="fonts.googleapis.com"]')];
  if (gfLinks.length > 0) {
    tokens.googleFontsUrl = gfLinks[gfLinks.length - 1].href.replace(/&display=swap.*$/, '&display=swap');
  }

  console.log(JSON.stringify(tokens, null, 2));
  return tokens;
})();
```

### Step 1.3: Analyze Page Layout

Analyze the page DOM to identify sections. Produce `layout.json`:

```javascript
(function() {
  function getText(el) {
    return el?.textContent?.trim()?.substring(0, 200) || null;
  }

  const doc = document;

  const layout = {
    siteTitle: getText(doc.querySelector('nav .logo, nav .brand, header .brand, .site-title')),
    heroHeading: getText(doc.querySelector('section:first-of-type h1, .hero h1, main > section:first-child h1')),
    heroSubtext: getText(doc.querySelector('section:first-of-type p, .hero p, main > section:first-child p')),
    hasFeaturesSection: !!doc.querySelector('[class*="feature"], [class*="grid"], .card-list, [class*="services"]'),
    hasCTASection: !!doc.querySelector('[class*="cta"], [class*="call-to-action"], [class*="get-started"]'),
    extraSections: []
  };

  console.log(JSON.stringify(layout, null, 2));
  return layout;
})();
```

### Step 1.4: Save Files

Save the extracted data:
- `tokens.json` — design token output from Step 1.2
- `layout.json` — page layout output from Step 1.3

---

## Phase 2: Theme Generation

Run the CLI command with the extracted files:

```bash
bukit clone --tokens tokens.json --layout layout.json --theme <theme-name> --brand "<Brand Name>" --use
```

Options:
- `--tokens` (required): Path to the design tokens JSON file
- `--theme`: Theme name (default: `cloned`)
- `--layout`: Path to the layout JSON file (optional; uses defaults if omitted)
- `--brand`: Brand name for nav bar and footer
- `--use`: Automatically switch to the new theme after creation
- `--force`: Overwrite existing theme directory

The CLI generates 17 files under `themes/<name>/`:
- `assets/style.css` — full CSS with custom variable values
- `layouts/layouts/base.html` — HTML skeleton with Google Fonts link (if detected)
- `layouts/partials/header.html` — navigation bar
- `layouts/partials/footer.html` — footer with "Powered by bukit" attribution
- `layouts/partials/list-card.html` — reusable list item/card partial
- `layouts/partials/pagination-nav.html` — pagination navigation partial
- `layouts/pages/index.html` — homepage with Hero + Features (if detected) + Latest content
- `layouts/pages/page.html` — generic page template
- `layouts/pages/post.html` — blog post template
- `layouts/pages/list.html` — collection list template
- `layouts/pages/pagination.html` — paginated archive
- `layouts/pages/taxonomy-index.html` — taxonomy index
- `layouts/pages/taxonomy-term.html` — taxonomy term page
- `layouts/pages/search.html` — search page
- `layouts/bukit.templates.yaml` — template capability manifest

---

## Phase 3: Verification

After generation, verify the result:

```bash
# Check theme integrity
bukit doctor

# Build the site
bukit build
```

If the site renders correctly, the clone is complete. The user can now customize the theme further using `bukit-templating` patterns.

## Common Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| `--tokens` file not found | Path resolution failed | Use absolute path or path relative to project root |
| Theme already exists | Duplicate theme name without `--force` | Use `--force` to overwrite or choose a different `--theme` name |
| Invalid JSON in tokens file | Browser extraction script failed | Re-run extraction script and verify JSON syntax |
| Missing styles after clone | Design tokens extraction missed elements | Manually fill missing values in tokens.json, then re-run `bukit clone` |
| Base layout missing Google Fonts | Font was not detected as Google Fonts | Manually add `googleFontsUrl` to tokens.json |

## tokens.json Reference

Full list of accepted fields (all optional — missing fields fall back to starter defaults):

| Field | CSS Variable | Default |
|-------|-------------|---------|
| `bg` | `--bg` | `#fbfaf8` |
| `surface` | `--surface` | `#ffffff` |
| `surfaceMuted` | `--surface-muted` | `#f3f1ed` |
| `text` | `--text` | `#202124` |
| `muted` | `--muted` | `#66615b` |
| `border` | `--border` | `#ded9d0` |
| `primary` | `--primary` | `#0b5fff` |
| `primaryStrong` | `--primary-strong` | `#0846b8` |
| `accent` | `--accent` | `#0f7b6c` |
| `radius` | `--radius` | `8px` |
| `contentMax` | `--content` | `760px` |
| `wideMax` | `--wide` | `1080px` |
| `shadow` | `--shadow` | `0 16px 40px rgba(32, 33, 36, 0.08)` |
| `fontFamily` | `font-family` on `body` | System font stack |
| `headingFontFamily` | (used for custom heading font) | Same as `fontFamily` |
| `codeFontFamily` | `font-family` on `code/pre` | Monospace stack |
| `googleFontsUrl` | `<link>` in `<head>` | None |
