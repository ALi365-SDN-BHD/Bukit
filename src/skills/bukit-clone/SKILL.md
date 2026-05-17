---
name: bukit-clone
description: Clone any website's visual design into a Bukit theme. Use when the user wants to clone/copy a website's appearance, replicate a design, or create a theme from an existing site. This skill handles the full pipeline: browser design token extraction, layout analysis, asset download, and CLI-driven theme generation.
description_zh: 将任意网站的视觉设计克隆为 Bukit 主题。处理完整流水线：浏览器设计令牌提取、布局分析、资源下载和 CLI 驱动主题生成。
description_ms: Klon reka bentuk visual mana-mana laman web ke dalam tema Bukit. Kemahiran ini mengendalikan saluran paip penuh: pengekstrakan token reka bentuk pelayar, analisis susun atur, muat turun aset, dan penjanaan tema dipacu CLI.
description_en: Clone any website's visual design into a Bukit theme. Full pipeline: browser design token extraction, layout analysis, asset download, and CLI-driven theme generation.
argument-hint: "<url> [--theme <name>] [--verify] [--fail-on-visual-diff]"
user-invocable: true
---

# Bukit Clone Website → Theme

## Overview

Clone any website's visual design and visible content as a Bukit theme plus Bukit content/data. Three-phase workflow:

1. **Extraction** (you): Browser MCP → extract design tokens + page metadata + sections + assets → `tokens.json` + `page.json` + `sections.json` + `assets.json`
2. **Generation** (CLI): `bukit clone --tokens tokens.json --page page.json --sections sections.json --assets assets.json --theme <name>`
3. **Verification**: `bukit doctor && bukit build` (or `--verify` for automated pixel-diff + behavior checks)

**REQUIRED BACKGROUND:** bukit-theme (directory structure), bukit-templating (Scriban conventions), bukit-config (site.yaml source updates).
**REQUIRED SUB-SKILL:** bukit-cli-reference for CLI execution.
**REQUIRED TOOL:** Browser MCP (Chrome MCP / Playwright MCP). Without browser automation, this skill cannot work.

**Related commands (simpler alternatives):**
- `bukit theme wizard <name> --preset blog|docs|landing|minimal|portfolio` — interactive theme creation with 5 presets
- `bukit theme create <name>` — copy from built-in starter
- `bukit theme install --registry <name>` — install community theme from registry

Use `bukit clone` when you need an exact visual replica of an existing live site. Use wizard/presets when you want a fresh theme in a known design style.

---

## Phase 1: Reconnaissance & Token Extraction

Use a browser MCP tool (Chrome MCP preferred). Without browser automation, this skill cannot work.

### Step 1.1: Take Screenshots

1. Open `$ARGUMENTS` with browser MCP
2. Take full-page screenshots at **desktop (1440px)**, **tablet (768px)**, and **mobile (390px)** viewports
3. Save to `docs/design-references/`
4. Create `docs/research/` with:
   - `DESIGN_TOKENS.md` — colors, typography, spacing, shadows, breakpoints
   - `PAGE_TOPOLOGY.md` — ordered section map and content/data mapping
   - `BEHAVIORS.md` — detected interactions, states, thresholds, unsupported behavior notes
   - `components/*.spec.md` — one spec per meaningful section/component

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
- `page.json` — page title, source URL, summary/description, body fallback, SEO metadata
- `sections.json` — ordered visible sections with type, text/HTML, items, buttons, assets, styles, states, responsive hints
- `layout.json` — legacy/simple page layout from Step 1.5; use only when `page.json`/`sections.json` are not available
- `behaviors.json` — interactive behaviors from Step 1.7
- `icons.json` — SVG icons from Step 1.3 (optional enhancement)
- `assets.json` — static assets to download from Step 1.4 (optional enhancement)

### Step 1.6a: Run Section/Component Extractor

Run this browser MCP script at each required viewport. Merge the results into `sections.json`; keep the desktop ordering as canonical, and store tablet/mobile differences under `responsive.viewports`.

```javascript
(function() {
  const gs = (el) => getComputedStyle(el);
  const css = (el) => {
    const s = gs(el);
    return {
      display: s.display,
      position: s.position,
      gridTemplateColumns: s.gridTemplateColumns,
      flexDirection: s.flexDirection,
      alignItems: s.alignItems,
      justifyContent: s.justifyContent,
      gap: s.gap,
      padding: s.padding,
      margin: s.margin,
      background: s.background,
      color: s.color,
      fontFamily: s.fontFamily,
      fontSize: s.fontSize,
      fontWeight: s.fontWeight,
      lineHeight: s.lineHeight,
      borderRadius: s.borderRadius,
      boxShadow: s.boxShadow,
      transform: s.transform,
      transition: s.transition
    };
  };
  const box = (el) => {
    const r = el.getBoundingClientRect();
    return { x: Math.round(r.x), y: Math.round(r.y + scrollY), width: Math.round(r.width), height: Math.round(r.height) };
  };
  const clean = (text) => (text || '').replace(/\s+/g, ' ').trim();
  const typeOf = (el) => {
    const c = el.className?.toString().toLowerCase() || '';
    const id = el.id?.toLowerCase() || '';
    const hay = `${c} ${id}`;
    if (el.matches('header, nav') || /nav|navbar|header/.test(hay)) return 'navigation';
    if (el.matches('footer') || /footer/.test(hay)) return 'footer';
    if (/hero|jumbotron|masthead/.test(hay)) return 'hero';
    if (/pricing|plans/.test(hay)) return 'pricing';
    if (/faq|accordion/.test(hay)) return 'faq';
    if (/feature|benefit/.test(hay)) return 'features';
    if (/cta|call-to-action/.test(hay)) return 'cta';
    return 'rich_section';
  };
  const candidates = [...document.querySelectorAll('header, nav, main > section, main > div, section, footer')]
    .filter((el, index, arr) => {
      const r = el.getBoundingClientRect();
      return r.width > 80 && r.height > 30 && clean(el.innerText).length > 0 && !arr.some((other) => other !== el && other.contains(el) && other.matches('section'));
    });
  return {
    viewport: { width: innerWidth, height: innerHeight },
    sections: candidates.map((el, i) => {
      const heading = clean(el.querySelector('h1,h2,h3')?.innerText);
      const buttons = [...el.querySelectorAll('a,button')].filter(x => clean(x.innerText)).slice(0, 8).map(x => ({
        label: clean(x.innerText),
        url: x.href || x.getAttribute('data-href') || '#',
        variant: /secondary|outline|ghost/.test(x.className?.toString().toLowerCase() || '') ? 'secondary' : 'primary'
      }));
      const components = [...el.querySelectorAll('h1,h2,h3,p,a,button,img,video,[role="tab"],[role="dialog"]')].slice(0, 40).map((node, n) => ({
        id: `${i + 1}-${n + 1}`,
        type: node.tagName.toLowerCase(),
        selector: node.id ? `#${node.id}` : node.className ? `.${node.className.toString().trim().split(/\s+/)[0]}` : node.tagName.toLowerCase(),
        text: clean(node.innerText || node.alt),
        html: node.outerHTML.slice(0, 2000),
        bounds: box(node),
        computedStyles: css(node)
      }));
      return {
        id: el.id || `section-${i + 1}`,
        type: typeOf(el),
        heading,
        text: clean(el.innerText),
        contentHtml: el.innerHTML,
        order: (i + 1) * 10,
        bounds: box(el),
        computedStyles: css(el),
        buttons,
        components,
        imageUrls: [...el.querySelectorAll('img')].map(img => img.currentSrc || img.src).filter(Boolean),
        responsive: { viewports: { [`${innerWidth}`]: { bounds: box(el), styles: css(el) } } }
      };
    })
  };
})();
```

### Step 1.6b: Build `page.json`

```json
{
  "title": "Target Site",
  "url": "https://example.com/",
  "summary": "Visible page summary",
  "bodyMarkdown": "# Target Site\n\nFallback editable page content.",
  "screenshots": [
    { "name": "desktop", "width": 1440, "screenshot": "docs/design-references/target-1440.png" },
    { "name": "tablet", "width": 768, "screenshot": "docs/design-references/target-768.png" },
    { "name": "mobile", "width": 390, "screenshot": "docs/design-references/target-390.png" }
  ],
  "seo": {
    "title": "Target Site",
    "description": "Meta description",
    "image": "https://example.com/og.png"
  }
}
```

### Step 1.6c: Build `sections.json`

Every visible section must become a Bukit data module. Preserve real text and content first; use `contentHtml` for complex sections that cannot be safely decomposed.

```json
{
  "sections": [
    {
      "id": "hero",
      "type": "hero",
      "heading": "Real headline",
      "subheading": "Real supporting copy",
      "contentHtml": "<p>Original visible content.</p>",
      "buttons": [{ "label": "Get started", "url": "/start", "variant": "primary" }],
      "imageUrls": ["https://example.com/hero.png"],
      "styles": { "padding": "72px 0", "background": "#ffffff" },
      "bounds": { "x": 0, "y": 72, "width": 1440, "height": 680 },
      "computedStyles": { "display": "grid", "fontSize": "18px" },
      "components": [
        {
          "type": "button",
          "selector": ".hero-cta",
          "text": "Get started",
          "bounds": { "x": 120, "y": 520, "width": 140, "height": 48 },
          "computedStyles": { "borderRadius": "999px" }
        }
      ],
      "interactions": [{ "type": "click", "trigger": "click", "target": ".hero-cta", "description": "Primary CTA link" }],
      "responsive": {
        "columnsDesktop": "1.2fr 0.8fr",
        "columnsMobile": "1fr",
        "viewports": {
          "390": { "bounds": { "x": 0, "y": 64, "width": 390, "height": 720 }, "styles": { "display": "block" } }
        }
      }
    },
    {
      "type": "features",
      "title": "Features",
      "items": [
        { "title": "Fast", "description": "Original card text", "image": "https://example.com/a.png" }
      ]
    }
  ]
}
```

### Step 1.7: Detect Interactive Behaviors

Run this script via browser MCP and save as `behaviors.json`:

```javascript
(function() {
  const doc = document, win = window;
  const body = doc.body;
  const gs = (el, prop) => { try { return getComputedStyle(el)[prop]; } catch { return null; } };

  const behaviors = {};

  // Sticky header
  const header = doc.querySelector('header, nav');
  if (header) {
    const pos = gs(header, 'position');
    behaviors.stickyHeader = pos === 'sticky' || pos === 'fixed';
  }

  // Scroll shrink (header hides on scroll down)
  behaviors.scrollShrinkNav = false;
  try {
    for (const sheet of doc.styleSheets) {
      try { if (!sheet.cssRules) continue; } catch { continue; }
      for (const rule of sheet.cssRules) {
        if (rule.selectorText && rule.selectorText.includes('header') && rule.style.transform && rule.style.transform.includes('translateY')) {
          behaviors.scrollShrinkNav = true;
          break;
        }
      }
    }
  } catch {}

  // Card hover lift
  const card = doc.querySelector('.card, article, [class*="card"]');
  if (card) {
    const hov = gs(card, 'transform') || '';
    behaviors.cardHoverLift = hov.includes('translateY') || hov.includes('scale');
    try {
      for (const sheet of doc.styleSheets) {
        try { if (!sheet.cssRules) continue; } catch { continue; }
        for (const rule of sheet.cssRules) {
          if (rule.selectorText && rule.selectorText.includes(':hover') && (rule.style.transform || rule.style.boxShadow)) {
            if (!behaviors.cardHoverLift) behaviors.cardHoverLift = true;
            break;
          }
        }
      }
    } catch {}
  }

  // Animate on scroll
  behaviors.animateOnScroll = !!doc.querySelector('[data-aos], [data-scroll], [class*="animate"], [class*="fade-in"], [class*="reveal"]');
  if (!behaviors.animateOnScroll) {
    try {
      for (const sheet of doc.styleSheets) {
        try { if (!sheet.cssRules) continue; } catch { continue; }
        for (const rule of sheet.cssRules) {
          if (rule.selectorText && (rule.selectorText.includes('animate') || rule.selectorText.includes('fade'))) {
            if (rule.style.animation || rule.style.animationName) {
              behaviors.animateOnScroll = true;
              break;
            }
          }
        }
      }
    } catch {}
  }

  // Dark mode
  behaviors.darkModeToggle = !!doc.querySelector('[class*="dark"], [class*="theme"], [aria-label*="dark"], [aria-label*="theme"]');
  if (!behaviors.darkModeToggle) {
    try { if (localStorage.getItem('theme') || localStorage.getItem('darkMode')) behaviors.darkModeToggle = true; } catch {}
    if (matchMedia('(prefers-color-scheme: dark)').matches) behaviors.darkModeToggle = true;
  }

  // Mobile hamburger
  behaviors.mobileHamburger = !!doc.querySelector('[class*="hamburger"], [class*="burger"], [aria-label*="menu"], button[aria-expanded]');

  // Smooth scroll
  behaviors.smoothScroll = gs(doc.documentElement, 'scrollBehavior') === 'smooth';
  if (!behaviors.smoothScroll) {
    const anchors = doc.querySelectorAll('a[href^="#"]');
    for (const a of anchors) {
      if (a.getAttribute('data-scroll') || a.onclick) { behaviors.smoothScroll = true; break; }
    }
  }

  // Back to top
  behaviors.backToTop = !!doc.querySelector('[class*="back-to-top"], [class*="scroll-top"], [aria-label*="top"]');

  // Modal
  behaviors.hasModal = !!doc.querySelector('[role="dialog"], [class*="modal"], [class*="overlay"], [class*="popup"]');

  // Dropdown
  behaviors.hasDropdown = !!doc.querySelector('[class*="dropdown"], [aria-haspopup], [role="menu"]');

  // Tabs
  behaviors.hasTabs = !!doc.querySelector('[role="tablist"], [class*="tabs"], [class*="tab-nav"]');

  // Lenis / smooth scroll library
  behaviors.useLenis = typeof window.lenis !== 'undefined' ||
    !!document.querySelector('.lenis-init') ||
    document.documentElement.style.scrollBehavior === 'smooth';

  console.log(JSON.stringify(behaviors, null, 2));
  return behaviors;
})();
```

---

## Phase 2: Theme Generation

```bash
bukit clone --tokens tokens.json --page page.json --sections sections.json --behaviors behaviors.json --icons icons.json --assets assets.json --theme <theme-name> --brand "<Brand Name>" --use --verify
```

Options:
- `--tokens` (required): Path to tokens JSON file
- `--theme`: Theme name (default: `cloned`)
- `--page`: Page metadata JSON file. Enables high-fidelity content/data clone mode.
- `--sections`: Ordered section JSON file. Enables generation of `data/*.md` modules and a section-aware homepage.
- `--layout`: Path to layout JSON file (optional; defaults used if omitted)
- `--behaviors`: Path to behaviors JSON file (optional; generated from Step 1.7)
- `--icons`: Path to icons JSON file from Step 1.3 (optional; writes SVGs to `assets/icons/`)
- `--assets`: Path to assets JSON file from Step 1.4 (optional; auto-downloads assets to theme asset dirs)
- `--brand`: Brand name for nav bar and footer
- `--use`: Automatically switch to the new theme
- `--force`: Overwrite existing theme directory
- `--verify`: Run clone verification after generation (`doctor`-style checks + `build` + pixel diff + behavior verify script)
- `--visual-threshold`: Allowed pixel mismatch ratio for screenshot pairs (default `0.03`)
- `--fail-on-visual-diff`: Return non-zero when any screenshot pair exceeds `--visual-threshold`

In high-fidelity mode (`--page` or `--sections`), the CLI also generates:
- `content/index.md` — editable page metadata and fallback body
- `data/clone-*.md` — one Bukit data module per visible section
- `site.yaml` source updates — switches to `provider: sources`

The CLI generates files under `themes/<name>/`:
- `assets/style.css` — Full CSS with custom variables + behavior enhancements + state section styles
- `assets/behaviors.js` — (conditional) Vanilla JS for behaviors + Lenis init
- `assets/icons/*.svg` — (conditional, if `--icons`) Individual SVG icon files from extraction
- `assets/images/`, etc. — (conditional, if `--assets`) Downloaded static assets by type
- `layouts/layouts/base.html` — HTML skeleton with Google Fonts + Lenis CDN + behaviors.js
- `layouts/partials/clone-section.html` plus aliases
- `layouts/partials/header.html`, `footer.html`, `list-card.html`, `pagination-nav.html`
- `layouts/partials/modal.html`, `dropdown.html`, `tabs.html` — conditional
- `layouts/pages/index.html`, `page.html`, `post.html`, `list.html`, etc.
- `layouts/bukit.templates.yaml`

### Lenis Smooth Scroll

When `useLenis: true` in `behaviors.json`:

1. CDN script injected in `base.html` (`<script src="https://cdn.jsdelivr.net/npm/lenis@1.1/dist/lenis.min.js">`)
2. `behaviors.js` initializes Lenis with `duration: 1.2` and exponential easing
3. All scroll interactions become smooth (wheel, keyboard, anchor links)

```json
{ "useLenis": true }
```

### Multi-State Sections

```json
{
  "extraSections": [
    {
      "heading": "Pricing",
      "states": [
        { "label": "Monthly", "contentHtml": "<p>$9/mo</p>" },
        { "label": "Annual", "contentHtml": "<p>$90/yr</p>" }
      ]
    }
  ]
}
```

### Summary Output

```
Theme cloned: my-theme
  Files: 17
  Behaviors: 3
  Icons: 12
  Assets: 5 (theme asset dirs created)
  Extra sections: 2
```

---

## Phase 3: Verification

```bash
bukit clone --tokens tokens.json --page page.json --sections sections.json --behaviors behaviors.json --theme my-site --force --verify --fail-on-visual-diff --visual-threshold 0.03
```

Produces:
- `docs/research/VERIFY_REPORT.md` — human-readable markdown report
- `docs/research/VERIFY_REPORT.json` — machine-readable JSON with `comparisons`, `missingScreenshots`, `affectedSections`, `passed`
- `docs/research/BEHAVIORS_VERIFY.js` — interactive behavior check script

### Behavior Verification

After `--verify`, run `docs/research/BEHAVIORS_VERIFY.js` in the browser console or via automation:

| Check | What it tests |
|-------|--------------|
| `HeaderSticky` | `.site-header` has `position: sticky` or `position: fixed` |
| `HeaderShrink` | `.nav-hidden` class toggles on scroll |
| `DarkModeToggle` | `.dark-mode-toggle` exists and toggles `body.dark` |
| `Modal` | `.modal-overlay` opens/closes and responds to Escape |
| `Hamburger` | `.hamburger` button toggles `.nav-links.open` |
| `Tabs` | `.tab-nav` or `.state-tabs` switches panels on click |
| `Lenis` | `window.lenis` is defined |
| `BackToTop` | `.back-to-top` button exists |
| `AnimateOnScroll` | `.animate-in` elements found |

Results: console colored PASS/FAIL/WARN + JSON via `window.__bukitBehaviorResults`.

### JSON-Driven Repair Loop

1. `buildPassed: false` → fix build/template/config first
2. `missingScreenshots` → recapture screenshots
3. `affectedSections` → fix `sections.json` styles/bounds/assets, then generated partials
4. Rerun `--verify --fail-on-visual-diff` after each repair

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
| `hoverLift` | Card hover distance | `3px` |
| `hoverShadow` | Card hover shadow | `var(--modal-shadow)` |

## behaviors.json Reference

| Field | Effect | Default |
|-------|--------|---------|
| `stickyHeader` | Header `position: sticky; top: 0; z-index: 100` | `false` |
| `scrollShrinkNav` | Hide header on scroll down (`.nav-hidden` + JS scroll listener) | `false` |
| `cardHoverLift` | Card hover lift + shadow (uses `hoverLift`/`hoverShadow` from tokens) | `false` |
| `animateOnScroll` | `@keyframes` + `.animate-in/.animate-visible` + IntersectionObserver | `false` |
| `mobileHamburger` | Hamburger button + mobile nav toggle (CSS + JS) | `false` |
| `darkModeToggle` | Dark mode CSS variables + toggle button with localStorage | `false` |
| `smoothScroll` | Smooth scroll for `#anchor` links (vanilla JS) | `false` |
| `backToTop` | Floating back-to-top button (JS-injected) | `false` |
| `hasModal` | `partials/modal.html` + modal CSS + JS (open/close/Escape) | `false` |
| `hasDropdown` | `partials/dropdown.html` + dropdown CSS + JS | `false` |
| `hasTabs` | `partials/tabs.html` + tabs CSS + JS (tab switching) | `false` |
| `animationStyle` | `"fadeInUp"` / `"fadeIn"` / `"slideUp"` / `"scaleIn"` | `"fadeInUp"` |
| `scrollThreshold` | px value at which scroll-shrink nav hides | `60` |
| `useLenis` | Injects Lenis CDN + RAF-based smooth scroll | `false` |

Each behavior generates **CSS rules only**, **JS only**, or **both**:
- **CSS-only**: `stickyHeader`, `cardHoverLift`
- **CSS+JS**: `scrollShrinkNav`, `animateOnScroll`, `mobileHamburger`, `darkModeToggle`, `hasModal`, `hasDropdown`, `hasTabs`
- **JS-only**: `smoothScroll`, `backToTop`, `useLenis`

### Lenis Smooth Scroll

When `useLenis: true` in `behaviors.json`:
1. CDN script injected in `base.html` (`lenis@1.1`)
2. `behaviors.js` initializes with `duration: 1.2` + exponential easing
3. All scroll (wheel/keyboard/anchor) becomes smooth

```json
{ "useLenis": true }
```

---

## Troubleshooting & Common Errors

| Symptom | Cause | Fix |
|---------|------|------|
| `Failed to parse tokens file` | `tokens.json` YAML/JSON syntax error or wrong format | Validate JSON with `jq` or a linter; ensure it matches the `CloneTokens` schema |
| `Theme already exists: cloned` | Previous clone not cleaned up | Add `--force` to overwrite |
| Pixel diff > threshold on every comparison | Screenshot dimensions mismatch or different fonts/CSS rendering | Increase `--visual-threshold` (e.g., `0.08`); ensure screenshots taken at exactly 1440/768/390 viewports |
| `Unsupported PNG format` in VERIFY_REPORT | Non-standard PNG (grayscale, indexed, 16-bit) | Re-save screenshots as 24-bit/32-bit RGBA PNG |
| `behaviors.json` all fields `false` | Target site has no detectable JS behaviors OR browser scripts ran on wrong page | Verify the extraction scripts ran on the correct URL; manually inspect behaviors and hardcode expected values if automation misses them |
| `No paired screenshots found` in VERIFY_REPORT | Local screenshots not yet captured | After build, capture browser screenshots at each viewport into `docs/research/local-screenshots/local-{viewport}.png` |
| Build fails with "template not found" | Clone generated incomplete theme directory | Check `tokens.json` has required fields; verify `--tokens` path is correct; re-run with `--force` |
| `site.collections` migration warning | Clone wrote `provider: sources` but collections not configured | Run `bukit doctor` — it provides the exact migration instructions |
| `doctor` reports manifest mismatch | `bukit.templates.yaml` missing or stale vs actual template files | Run `bukit template sync` to auto-generate; or re-run clone with `--force` |
| Behaviors not working in preview | `behaviors.js` not loaded or browser cached old version | Check `base.html` includes `<script src="{{ site.base_url }}/assets/behaviors.js">`; hard-refresh browser (Cmd+Shift+R) |

### Doctor Enhanced Checks

After cloning, `bukit doctor` also runs:
- **Template completeness report** — compares `bukit.templates.yaml` declarations vs actual files
- **Template chain analysis** — `{% layout %}` inheritance and `{{ include }}` dependency references
- **Unused parameter warnings** — `theme.params` declared but not used in any template

These help catch gaps in the generated theme before building.

---

## CI Integration

### GitHub Actions Workflow

```yaml
name: Clone QA
on:
  push:
    paths:
      - 'tokens.json'
      - 'sections.json'
      - 'behaviors.json'
      - 'themes/cloned/**'
jobs:
  verify:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Install bukit
        run: dotnet tool install --global Bukit.Cli || dotnet tool update --global Bukit.Cli
      - name: Clone & Verify
        run: |
          bukit clone --tokens tokens.json --sections sections.json \
            --behaviors behaviors.json --icons icons.json \
            --theme cloned --force --use
          bukit clone --tokens tokens.json --theme cloned \
            --verify --fail-on-visual-diff --visual-threshold 0.03
      - name: Upload report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: verify-report
          path: docs/research/VERIFY_REPORT.*
```

### Exit Codes

| Code | Meaning |
|------|---------|
| `0` | All passed |
| `1` | Verify failed (build/template/diff) |
| `2` | Usage error |

### Behavior Verify with Playwright

```bash
bukit build && bukit preview --port 4173 &
sleep 2
npx playwright test << 'EOF'
import { test, expect } from '@playwright/test';
test('behavior checks', async ({ page }) => {
  await page.goto('http://localhost:4173');
  await page.addScriptTag({ path: 'docs/research/BEHAVIORS_VERIFY.js' });
  const results = await page.evaluate(() => window.__bukitBehaviorResults);
  expect(results.filter(r => r.status === 'fail')).toHaveLength(0);
});
EOF
```