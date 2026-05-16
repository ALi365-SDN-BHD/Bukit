---
name: bukit-clone
description: Clone any website's visual design into a Bukit theme. Use when the user wants to clone/copy a website's appearance, replicate a design, or create a theme from an existing site. This skill handles the full pipeline: browser design token extraction, layout analysis, asset download, and CLI-driven theme generation.
description_zh: 将任意网站的视觉设计克隆为 Bukit 主题。处理完整流水线：浏览器设计令牌提取、布局分析、资源下载和 CLI 驱动主题生成。
description_ms: Klon reka bentuk visual mana-mana laman web ke dalam tema Bukit. Kemahiran ini mengendalikan saluran paip penuh: pengekstrakan token reka bentuk pelayar, analisis susun atur, muat turun aset, dan penjanaan tema dipacu CLI.
description_en: Clone any website's visual design into a Bukit theme. Full pipeline: browser design token extraction, layout analysis, asset download, and CLI-driven theme generation.
argument-hint: "<url> [--theme <name>]"
user-invocable: true
---

# Bukit Clone Website → Theme

## Overview

Clone any website's visual design as a Bukit theme. Three-phase workflow:

1. **Extraction** (you): Browser MCP → extract design tokens + layout + assets → `tokens.json` + `layout.json`
2. **Generation** (CLI): `bukit clone --tokens tokens.json --layout layout.json --theme <name>`
3. **Verification**: `bukit doctor && bukit build`

**REQUIRED BACKGROUND:** bukit-theme (directory structure), bukit-templating (Scriban conventions).
**REQUIRED SUB-SKILL:** bukit-cli-reference for CLI execution.

---

## Phase 1: Reconnaissance & Token Extraction

Use a browser MCP tool (Chrome MCP preferred). Without browser automation, this skill cannot work.

### Step 1.1: Take Screenshots

1. Open `$ARGUMENTS` with browser MCP
2. Take full-page screenshots at **desktop (1440px)** and **mobile (390px)** viewports
3. Save to `docs/design-references/`

### Step 1.2: Extract Design Tokens

Run this script via browser MCP and save as `tokens.json`:

```javascript
(function() {
  function gs(el, prop) { try { return getComputedStyle(el)[prop]; } catch { return null; } }
  function find(sel) { return document.querySelector(sel); }
  function findAll(sel) { return [...document.querySelectorAll(sel)]; }
  function sc(val) { if (!val || val === 'rgba(0, 0, 0, 0)' || val === 'transparent') return null; return val; }

  const doc = document, body = doc.body;
  const card = find('.card, article, [class*="card"], [class*="post"], [class*="box"]');
  const heading = find('h1, h2, h3');
  const meta = find('.meta, .date, .subtitle, time, small, .desc, .summary');
  const link = find('a:not(.brand):not(.logo):not(.btn):not(.button)');
  const button = find('button, .btn, [class*="button"], a[class*="button"]');
  const badge = find('.badge, .tag, .eyebrow, [class*="badge"], [class*="tag"]');
  const nav = find('nav, header');
  const footer = find('footer');
  const code = find('code, pre');

  // Detect Google Fonts
  let gfUrl = null;
  const gfLinks = [...document.querySelectorAll('link[href*="fonts.googleapis.com"]')];
  if (gfLinks.length > 0) gfUrl = gfLinks[gfLinks.length - 1].href.replace(/&display=swap.*$/, '&display=swap');

  // Detect responsive breakpoints from CSS
  let bpMobile = '680px', bpTablet = '1024px', bpDesktop = '1440px';
  try {
    for (const sheet of document.styleSheets) {
      try { if (!sheet.cssRules) continue; } catch { continue; }
      for (const rule of sheet.cssRules) {
        if (rule instanceof CSSMediaRule) {
          const c = rule.conditionText;
          const m = c.match(/max-width:\s*(\d+px)/);
          const n = c.match(/min-width:\s*(\d+px)/);
          if (n && !m) { const v = n[1]; if (parseInt(v) > 960) bpDesktop = v; else if (parseInt(v) > 640) bpTablet = v; }
          if (m && !n) { const v = m[1]; if (parseInt(v) < 900) bpMobile = v; }
        }
      }
    }
  } catch {}

  // Detect spacing scale
  let xs, sm, md, lg, xl;
  const samples = findAll('.container, section, .card, .p-\\[.*\\], .py-\\[.*\\], .px-\\[.*\\]');
  const gaps = new Set(), pads = new Set();
  for (const el of samples.slice(0, 20)) {
    const g = parseInt(gs(el, 'gap')); if (g > 0) gaps.add(g);
    const p = parseInt(gs(el, 'padding')); if (p > 0) pads.add(p);
  }
  const allSizes = [...new Set([...gaps, ...pads])].sort((a,b)=>a-b);
  if (allSizes.length >= 3) { xs = allSizes[0]+'px'; sm = allSizes[1]+'px'; md = allSizes[2]+'px'; }
  if (allSizes.length >= 5) { lg = allSizes[3]+'px'; xl = allSizes[4]+'px'; }

  const tokens = {
    bg: sc(gs(body, 'backgroundColor')) || '#ffffff',
    surface: sc(gs(card || find('section, [class*="container"]'), 'backgroundColor')) || '#ffffff',
    surfaceMuted: sc(gs(card || find('section'), 'backgroundColor')) || '#f3f1ed',
    text: sc(gs(body, 'color')) || '#202124',
    muted: sc(gs(meta || find('.summary, .description, .subtitle'), 'color')) || '#66615b',
    border: sc(gs(card || nav, 'borderColor') || gs(card || nav, 'borderBottomColor')) || '#ded9d0',
    primary: sc(gs(button || find('a, [class*="primary"]'), 'color')) || '#0b5fff',
    primaryStrong: null,
    accent: sc(gs(badge || find('.highlight, [class*="accent"]'), 'color')) || '#0f7b6c',

    radius: gs(card || button || find('[class*="rounded"]'), 'borderRadius') || '8px',
    contentMax: gs(find('article, .content, .post-body, [class*="content"]'), 'maxWidth') || '760px',
    wideMax: gs(find('.container, nav, .wrapper, [class*="container"]'), 'maxWidth') || '1080px',
    shadow: gs(card || find('.shadow, [class*="shadow"]'), 'boxShadow') || '0 16px 40px rgba(32, 33, 36, 0.08)',
    cardShadow: gs(card || find('.card, article'), 'boxShadow') || null,
    modalShadow: gs(find('[role="dialog"], .modal, [class*="modal"]'), 'boxShadow') || null,
    dropdownShadow: gs(find('[role="menu"], .dropdown, [class*="dropdown"], [class*="popup"]'), 'boxShadow') || null,

    navPadding: gs(nav || find('nav'), 'padding') || '18px 24px',
    containerPadding: gs(find('.container, main'), 'padding') || '42px 24px 64px',
    sectionGap: '34px',

    fontFamily: gs(body, 'fontFamily') || 'system-ui, -apple-system, sans-serif',
    headingFontFamily: gs(heading || find('h1'), 'fontFamily') || null,
    codeFontFamily: gs(code || find('code'), 'fontFamily') || '"SFMono-Regular", Consolas, monospace',
    googleFontsUrl: gfUrl,

    responsiveBreakpoints: { mobile: bpMobile, tablet: bpTablet, desktop: bpDesktop },
    spacingScale: { xs, sm, md, lg, xl }
  };

  console.log(JSON.stringify(tokens, null, 2));
  return tokens;
})();
```

### Step 1.3: Extract SVG Icons

```javascript
(function() {
  const icons = [];
  const seen = new Set();
  for (const svg of document.querySelectorAll('svg')) {
    const html = svg.outerHTML.replace(/\s+/g, ' ').trim().substring(0, 2000);
    const key = svg.getAttribute('aria-label') || svg.getAttribute('data-icon') || svg.className?.baseVal || html.substring(0, 100);
    if (seen.has(key)) continue;
    seen.add(key);
    icons.push({ name: (svg.getAttribute('aria-label') || 'icon-' + icons.length), svg: html, width: svg.getAttribute('width') || '24', height: svg.getAttribute('height') || '24' });
  }
  console.log(JSON.stringify(icons, null, 2));
  return icons;
})();
```

Save the output as `icons.json`. Copy the extracted SVGs into `themes/<name>/assets/` after theme generation.

### Step 1.4: Download Static Assets

```javascript
(function() {
  const assets = [];
  // Logo
  const logo = document.querySelector('nav img, header img, .logo img, .brand img');
  if (logo?.src) assets.push({ type: 'logo', src: logo.src, alt: logo.alt });

  // Hero image
  const hero = document.querySelector('section:first-of-type img, .hero img, main > section:first-child img');
  if (hero?.src) assets.push({ type: 'hero', src: hero.src, alt: hero.alt });

  // Favicon
  const fav = document.querySelector('link[rel="icon"], link[rel="shortcut icon"]');
  if (fav?.href) assets.push({ type: 'favicon', src: fav.href });

  // OG image
  const og = document.querySelector('meta[property="og:image"]');
  if (og?.content) assets.push({ type: 'og-image', src: og.content });

  // All feature/card images
  document.querySelectorAll('.card img, [class*="feature"] img, [class*="grid"] img').forEach(img => {
    if (img.src && !img.src.startsWith('data:')) assets.push({ type: 'content', src: img.src, alt: img.alt });
  });

  console.log(JSON.stringify(assets, null, 2));
  return assets;
})();
```

Download each asset to `themes/<name>/assets/images/` after theme generation using the browser MCP download tool or curl.

### Step 1.5: Analyze Page Layout

```javascript
(function() {
  function gt(el) { return el?.textContent?.trim()?.substring(0, 200) || null; }

  const doc = document;

  // Navigation links
  const navLinks = [...doc.querySelectorAll('nav a, header a')]
    .filter(a => !a.querySelector('img') && a.textContent.trim().length > 0)
    .slice(0, 8)
    .map(a => ({ label: a.textContent.trim().substring(0, 40), url: a.getAttribute('href') || '' }));

  // Footer links
  const footerLinks = [...doc.querySelectorAll('footer a')]
    .filter(a => a.textContent.trim().length > 0)
    .slice(0, 10)
    .map(a => ({ label: a.textContent.trim().substring(0, 40), url: a.getAttribute('href') || '' }));

  // Hero CTA
  const heroBtn = doc.querySelector('section:first-of-type .btn, .hero .btn, section:first-of-type a[class*="button"], .hero a[class*="button"], main > section:first-child .btn, main > section:first-child a[class*="button"]');
  const hasHeroCta = !!heroBtn;
  const heroCtaText = heroBtn?.textContent?.trim() || null;
  const heroCtaUrl = heroBtn?.getAttribute('href') || null;

  const layout = {
    siteTitle: gt(doc.querySelector('nav .logo, nav .brand, header .brand, .site-title')),
    heroHeading: gt(doc.querySelector('section:first-of-type h1, .hero h1, main > section:first-child h1')),
    heroSubtext: gt(doc.querySelector('section:first-of-type p, .hero p, main > section:first-child p')),
    hasFeaturesSection: !!doc.querySelector('[class*="feature"], [class*="grid"], .card-list, [class*="services"]'),
    hasCTASection: !!doc.querySelector('[class*="cta"], [class*="call-to-action"], [class*="get-started"]'),
    hasHeroCta: hasHeroCta,
    heroCtaText: heroCtaText,
    heroCtaUrl: heroCtaUrl,
    navLinks: navLinks,
    footerLinks: footerLinks,
    extraSections: []
  };

  console.log(JSON.stringify(layout, null, 2));
  return layout;
})();
```

### Step 1.6: Save Files

- `tokens.json` — design tokens from Step 1.2
- `layout.json` — page layout from Step 1.5
- `icons.json` — SVG icons from Step 1.3 (optional enhancement)
- `assets.json` — static assets to download from Step 1.4 (optional enhancement)

---

## Phase 2: Theme Generation

```bash
bukit clone --tokens tokens.json --layout layout.json --theme <theme-name> --brand "<Brand Name>" --use
```

Options:
- `--tokens` (required): Path to tokens JSON file
- `--theme`: Theme name (default: `cloned`)
- `--layout`: Path to layout JSON file (optional; defaults used if omitted)
- `--brand`: Brand name for nav bar and footer
- `--use`: Automatically switch to the new theme
- `--force`: Overwrite existing theme directory

The CLI generates 15 files under `themes/<name>/`:
- `assets/style.css` — Full CSS with custom variables (colors, shadows, spacing, breakpoints)
- `layouts/layouts/base.html` — HTML skeleton with Google Fonts
- `layouts/partials/header.html` — Navigation bar (with extracted nav links)
- `layouts/partials/footer.html` — Footer with extracted links + bukit attribution
- `layouts/partials/list-card.html` — Reusable card partial
- `layouts/partials/pagination-nav.html` — Pagination navigation
- `layouts/pages/index.html` — Homepage (Hero + Features + Latest content + CTA)
- `layouts/pages/page.html` — Generic page template
- `layouts/pages/post.html` — Blog post template
- `layouts/pages/list.html` — Collection list template
- `layouts/pages/pagination.html` — Paginated archive
- `layouts/pages/taxonomy-index.html` / `taxonomy-term.html` — Taxonomy pages
- `layouts/pages/search.html` — Search page
- `layouts/bukit.templates.yaml` — Template capability manifest

---

## Phase 3: Verification & Asset Download

```bash
# Verify theme
bukit doctor

# Download assets if extracted
# Use browser MCP or curl to download images from assets.json to themes/<name>/assets/images/

# Build
bukit build
```

---

## tokens.json Reference

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
| `shadow` | `--shadow` | `0 16px 40px rgba(32,33,36,0.08)` |
| `cardShadow` | `--card-shadow` | Same as `shadow` |
| `modalShadow` | `--modal-shadow` | `0 24px 80px rgba(32,33,36,0.18)` |
| `dropdownShadow` | `--dropdown-shadow` | `0 8px 24px rgba(32,33,36,0.12)` |
| `radius` | `--radius` | `8px` |
| `contentMax` | `--content` | `760px` |
| `wideMax` | `--wide` | `1080px` |
| `navPadding` | `--nav-padding` | `18px 24px` |
| `containerPadding` | `--container-padding` | `42px 24px 64px` |
| `sectionGap` | `--section-gap` | `34px` |
| `responsiveBreakpoints.mobile` | `--bp-mobile` | `680px` |
| `responsiveBreakpoints.tablet` | `--bp-tablet` | `1024px` |
| `responsiveBreakpoints.desktop` | `--bp-desktop` | `1440px` |
| `spacingScale.{xs,sm,md,lg,xl}` | `--space-*` | None (optional) |
| `fontFamily` | `font-family` on `body` | System font stack |
| `headingFontFamily` | `font-family` on `h1-h6` | Same as `fontFamily` |
| `codeFontFamily` | `font-family` on `code` | Monospace stack |
| `googleFontsUrl` | `<link>` in `<head>` | None |
