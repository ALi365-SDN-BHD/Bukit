# bukit (.NET 10 Native AOT Static Site Engine)

<p align="center">
  <img src="docs/bukit-logo.svg" alt="bukit logo" width="400">
</p>

Language versions: English (current) | [简体中文](./README.zh-CN.md) | [Bahasa Melayu](./README.ms.md)

Bukit is a .NET Native AOT static site generation engine designed for Notes-as-CMS workflows, AI Agent automation, and GEO-ready websites. It transforms structured notes, Notion databases, Markdown content, and future knowledge sources into fast, deployable, AI-readable static websites.

## Product Ecosystem

```
 Bukit
 = Static Site Engine
 = Build Core
 = Content ingestion, route generation, template rendering, SEO/GEO output

 BukitJalil
 = Local App / Control Panel
 = Project management, theme management, AI-powered conversational workflows, build & deploy control

 Notes-as-CMS
 = Content Production
 = Notion / Markdown / Obsidian / Feishu / Yuque / WeChat Official Account / other knowledge bases
```

## Documents

- User guide: [`guide/user`](guide/user/README.md)
- Developer guide: [`guide/dev`](guide/dev/README.md)
- Agent skills: [`src/skills`](src/skills/README.md)
- Governance notes: [`guide/dev/perf-aot-governance.md`](guide/dev/perf-aot-governance.md)
- Full Chinese reference: [`README.zh-CN.md`](README.zh-CN.md)

## Agent Skills

`src/skills/` is the agent-facing Bukit knowledge layer, not a runtime source directory. It splits site creation, command execution, configuration, theming, templating, Notion integration, routing, i18n, and debugging into focused `SKILL.md` files so an agent can load the right context for Bukit work.

- Unified entry: [`using-bukit`](src/skills/using-bukit/SKILL.md)
- Command source of truth: [`bukit-cli-reference`](src/skills/bukit-cli-reference/SKILL.md)
- Full navigation: [`src/skills/README.md`](src/skills/README.md)
- Coverage: CLI, `site.yaml`, theme, Scriban templates, Notion, routing, i18n, plugins, debugging, **clone**, **theme registry**

## Quick Start (using the example site in this repo)

```bash
dotnet build bukit.slnx -c Release
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c Release -- preview --dir examples/starter/dist --port auto
```

## Core CLI Commands

### Create a site

```bash
dotnet run --project src/Bukit.Cli -c Release -- create my-site
dotnet run --project src/Bukit.Cli -c Release -- create my-site --provider notion
```

### Build

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean
dotnet run --project src/Bukit.Cli -c Release -- build --clean --metrics metrics.json --log-format json
dotnet run --project src/Bukit.Cli -c Release -- build --clean --jobs 8
dotnet run --project src/Bukit.Cli -c Release -- build --site blog --clean
```

### Validate / Clean / Theme / Clone

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- clean --dir dist
dotnet run --project src/Bukit.Cli -c Release -- seo audit --dir dist
dotnet run --project src/Bukit.Cli -c Release -- theme list --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- theme use alt --config site.yaml
# Interactive theme wizard with 5 presets (blog|docs|landing|minimal|portfolio)
dotnet run --project src/Bukit.Cli -c Release -- theme wizard my-theme --preset blog
# Clone any website's design into a Bukit theme
dotnet run --project src/Bukit.Cli -c Release -- clone --tokens tokens.json --theme my-clone
```

### Theme Distribution & Registry

```bash
# Pack a theme for sharing
bukit theme pack my-blog          # → my-blog-1.0.0.tar.gz

# Install from local / URL / registry
bukit theme install ./my-blog-1.0.0.tar.gz
bukit theme install --registry blog-clean

# Search community themes
bukit theme search blog           # filter by name/tags
```

### Template Commands

```bash
bukit template list               # List all templates
bukit template create pages/gallery.html  # Interactive creation
bukit template validate           # Validate Scriban syntax
bukit template snippets           # Browse snippet library (8 Scriban + 9 CSS)
bukit template hints              # Available template variables
bukit template sync               # Auto-generate bukit.templates.yaml
```

## Key `site.yaml` Fields

- `site.collections`: primary recommended model for content organization and routing (declare `permalink`, `template`, optional `listRoute`, and optional `listTemplate` per collection). `post/page` defaults remain as a compatibility fallback.
- `site.baseUrl`: GitHub Pages subpath (`/my-repo`) or `/` for root.
- `site.base_url` in templates is an empty string when `site.baseUrl: /`; use `site.base_path` when a literal `/` root prefix is required.
- `site.url`: canonical site URL (sitemap/rss); can be overridden by `--site-url`.
- `site.seo`: engine-level SEO model and index policy for canonical, robots, OG, Twitter, hreflang, JSON-LD, sitemap/search/RSS filtering, optional head injection, `seo-report.json`, `bukit seo audit`, and optional `robots.txt`. See [`docs/seo.md`](docs/seo.md).
- `site.analytics.google_analytics_id`: GA4 Measurement ID; Bukit emits gtag when the ID exists unless `site.analytics.enabled: false`.
- `content.provider`: `markdown` or `notion`.
- `content.markdown.maxItems`: max Markdown items to load.
- `content.notion.maxItems`: max Notion pages to fetch.
- `content.notion.filterType/filterValue`: Notion database filters support `checkbox_true`, `checkbox_false`, `select_equals`, `status_equals`, `rich_text_equals`, and `none`.
- `content.notion.cacheMode/cacheDir`: Notion render cache options.
- `content.sources[].collection/addToCollections`: map a source into one or more `site.collections` routes; `mode: data` sources are exposed as `site.data.{name}` and still populate `site.modules` by type.
- `build.output`: output directory.
- `theme.layouts/assets/static`: theme directories. `static` is copied unchanged; use content pages or collections when you need Scriban includes/partials.

## AI Site Building (v2)

- Guide: [`guide/ai/chatgpt/README.md`](guide/ai/chatgpt/README.md)
- Intent contract: [`guide/dev/intent-cli.md`](guide/dev/intent-cli.md)
- ChatGPT prompt pack: [`guide/ai/chatgpt`](guide/ai/chatgpt/README.md)

## Notion Content Source

- Token must be provided via environment variable only: `NOTION_TOKEN`.
- For v1 schema reference: [`guide/dev/content.md`](guide/dev/content.md)

## GitHub Actions + GitHub Pages

A workflow template is provided at [`.github/workflows/release.yml`](.github/workflows/release.yml).
Copy it to your repository and customize as needed. See [`guide/user/13-deploy-github-pages.md`](guide/user/13-deploy-github-pages.md) for detailed guidance.

Typical setup:

1. In GitHub Settings → Pages, choose "GitHub Actions".
2. If you use Notion, add `NOTION_TOKEN` in repository secrets.
3. Push to `main` after your workflow is configured to build and deploy the site.

## AOT Publishing

```bash
dotnet publish src/Bukit.Cli -c AOT -r linux-x64 -o out/bukit
dotnet publish src/Bukit.Cli -c AOT -r linux-x64 -o out/bukit -p:BukitStripSymbols=true
dotnet publish src/Bukit.Cli -c AOT -r win-x64 -o out/bukit
```

## Validation Matrix

```bash
dotnet build bukit.slnx -c Release -warnaserror
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release
dotnet format bukit.slnx --verify-no-changes
```
