# 18 Clone a Website: Convert Any Live Site into a Bukit Theme

Clone captures a website's visual design — colors, typography, spacing, layout — and generates a Bukit theme that reproduces the same look. Three-phase workflow: extraction → generation → verification.

Related docs: [docs/clone.md](../../docs/clone.md)

## What You Will Get

- A Bukit theme directory that visually matches the target website
- Extracted design tokens (colors, fonts, shadows, spacing scale)
- Section/component layout analysis
- Downloaded assets (logos, icons, hero images)
- A verified build with the new theme

## When to Use

| Scenario | Tool |
|---------|------|
| Clone an existing live site's design | `bukit clone` (this page) |
| Create a fresh theme from presets | `bukit theme wizard --preset blog` |
| Install a community theme | `bukit theme install --registry <name>` (Experimental registry) |
| Copy built-in starter theme | `bukit theme create <name>` |

## How It Works

### Phase 1: Extraction (Browser MCP)

Use a browser automation tool (Chrome MCP / Playwright MCP) to extract design tokens from the target website. This produces JSON files that describe the visual design:

1. **Screenshots** — Full-page captures at desktop (1440px), tablet (768px), and mobile (390px) viewports
2. **Design tokens** (`tokens.json`) — Colors, font families, border radius, shadows, spacing scale, responsive breakpoints
3. **Page layout** (`page.json`) — Page title, description, SEO metadata, body content
4. **Section analysis** (`sections.json`) — Ordered visible sections with type, text, images, buttons, styles
5. **Assets** (`assets.json`) — Logo, hero image, favicon, feature images

See [bukit-clone skill](../../src/skills/bukit-clone/SKILL.md) for the detailed browser scripts.

### Phase 2: Generation (CLI)

```bash
bukit clone \
  --tokens tokens.json \
  --page page.json \
  --sections sections.json \
  --assets assets.json \
  --theme my-theme
```

This generates a complete theme directory under `themes/<name>/` with:
- `layouts/` — Scriban templates matching the site structure
- `assets/` — CSS with extracted design tokens
- `static/` — Static assets
- `theme.yaml` — Theme metadata

Update `site.yaml` to use the new theme:

```yaml
theme:
  name: my-theme
```

### Phase 3: Verification

```bash
# Check theme integrity
bukit doctor

# Build the site with the new theme
bukit build

# Or use built-in verification (pixel-diff + behavior checks)
bukit clone --verify
```

The `--verify` flag runs automated visual comparison between the original site screenshots and the Bukit-generated pages.

## Command Options

| Option | Description |
|--------|-------------|
| `--tokens <file>` | Path to design tokens JSON (required) |
| `--page <file>` | Path to page metadata JSON |
| `--sections <file>` | Path to sections JSON |
| `--assets <file>` | Path to assets JSON |
| `--theme <name>` | Theme name (required) |
| `--verify` | Run automated pixel-diff verification after clone |
| `--fail-on-visual-diff` | Exit with error if verification finds visual differences |

## What Gets Generated

```
themes/<name>/
  assets/
    style.css             # CSS with extracted colors, fonts, spacing
    images/               # Downloaded assets
  layouts/
    layouts/
      base.html           # Base layout with semantic HTML structure
    pages/
      index.html          # Homepage template
      page.html           # Generic page template
      post.html           # Blog post template
      list.html           # List page template
    partials/
      header.html         # Navigation partial
      footer.html         # Footer partial
  static/                 # Copied static files
  theme.yaml              # Theme metadata
```

## Limitations

- **JavaScript interactions** — Only static HTML/CSS is cloned. Animations, scroll effects, and client-side JS are not replicated.
- **Dynamic content** — Content fetched via API calls or rendered client-side will not be captured.
- **Complex layouts** — Sites with heavily nested CSS Grid or unusual layout patterns may need manual adjustment.
- **Custom fonts** — Licensed fonts may not be redistributable. Google Fonts URLs are preserved.

## Workflow Summary

```
1. Browser MCP: Open target URL, run extraction scripts
2. Save: tokens.json, page.json, sections.json, assets.json
3. CLI: bukit clone --tokens tokens.json ... --theme <name>
4. Verify: bukit doctor && bukit build
5. Iterate: Adjust templates/CSS as needed
```

## Next Steps

- [12 CLI Reference](./12-cli-reference.md) — Full `bukit clone` command reference
- [08 Themes & Templates](./08-themes-templates.md) — Theme creation and management
- [bukit-clone skill](../../src/skills/bukit-clone/SKILL.md) — Agent-facing step-by-step guide
