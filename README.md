# Bukit — .NET Native AOT Static Site Engine

<p align="center">
  <img src="docs/bukit-logo.svg" alt="bukit logo" width="400">
</p>

Language versions: English (current) | [简体中文](./README.zh-CN.md) | [Bahasa Melayu](./README.ms.md)

Bukit is a .NET Native AOT static site generation engine for **Notes-as-CMS**, **AI Agent workflows**, and **GEO-ready websites**. Turn Notion databases and Markdown into fast, deployable static sites.

## What is Bukit?

```
 Bukit
 = Static Site Engine
 = Build Core
 = Content ingestion, route generation, Scriban template rendering, SEO/GEO output

 BukitJalil
 = Local App / Control Panel
 = Project management, theme management, AI conversational workflows, build & deploy control

 Notes-as-CMS
 = Content Production
 = Notion / Markdown / Obsidian / Feishu / Yuque / other knowledge bases
```

Bukit handles content ingestion, route generation, Scriban template rendering, SEO/GEO output, and static HTML generation. It powers company websites, documentation sites, content sites, landing pages, and AI-assisted publishing workflows.

**BukitJalil** is a separate local control panel — not part of the Bukit runtime engine, and not required to build sites with Bukit.

Bukit is **not** a SaaS platform, a full CMS backend, a visual page builder, or a replacement for BukitJalil.

## Why Bukit?

- **Native AOT** — sub-50ms startup, low memory, single-binary deployment on Linux, macOS, and Windows
- **Notes-as-CMS** — write content in Notion or Markdown; Bukit turns it into a static site
- **AI Agent native** — `src/skills/` provides a knowledge layer for AI coding agents
- **GEO-ready** — built-in AI search engine optimization with `llms.txt`, FAQ/HowTo structured data, and GEO audit

## Core Features

- **Markdown & Notion content providers** with configurable field mapping
- **Scriban template engine** with layout inheritance, partials, and snippet library
- **Collection-based routing** with permalinks, list pages, pagination, and taxonomy
- **Multilingual support** — per-language builds, merged sitemap/RSS/search
- **SEO** — sitemap, RSS/Atom/JSON Feed, JSON-LD, Open Graph, Twitter Cards, canonical URLs, hreflang
- **GEO** — `llms.txt`, AI crawler `robots.txt` rules, FAQ/HowTo structured data, GEO Score audit
- **Theme system** with design tokens and componentized themes; theme registry is planned for the next stage
- **HMR dev server** with WebSocket live reload; preview server for build output
- **Plugin system** — `derive-pages` and `after-build` hooks; WASM and process plugin support
- **Incremental builds** — content-aware change detection; optional SHA256 asset hashing
- **GitHub Pages deployment** via CLI or GitHub Actions workflow

## Quick Start

```bash
# Build the CLI
dotnet build bukit.slnx -c Release

# Validate the example site configuration
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml

# Build the example site
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com

# Preview locally
dotnet run --project src/Bukit.Cli -c Release -- preview --dir examples/starter/dist --port auto
```

For a full walkthrough, see the [Quick Start guide](guide/user/01-quick-start.md).

## Documentation

| Audience | Start here |
|---|---|
| New users | [`guide/user`](guide/user/README.md) — Quick Start, config, content, deployment, troubleshooting |
| Maintainers / contributors | [`guide/dev`](guide/dev/README.md) — architecture, CLI contract, rendering, plugins, observability |
| AI Agent users | [`src/skills`](src/skills/README.md) — skill files for Codex, Claude Code, Copilot, Gemini CLI |
| AI site building | [`guide/ai/chatgpt`](guide/ai/chatgpt/README.md) — ChatGPT prompt pack and intent contract |
| CLI reference | [`guide/user/12-cli-reference.md`](guide/user/12-cli-reference.md) |
| Config reference | [`guide/user/04-site-yaml-config.md`](guide/user/04-site-yaml-config.md) |
| Deployment | [`guide/user/13-deploy-github-pages.md`](guide/user/13-deploy-github-pages.md) |

## Notion CMS Workflow

- Add a `notion` source in `content.sources[]` (see [config reference](guide/user/04-site-yaml-config.md))
- Provide your token as an environment variable: `NOTION_TOKEN` (never in `site.yaml`)
- Default database fields: `Published` (checkbox), `Title`, `Slug`, `Type` (post/page), `PublishAt`
- Full guide: [`guide/user/06-notion-content.md`](guide/user/06-notion-content.md)
- Schema reference: [`guide/dev/content.md`](guide/dev/content.md)

## AI / Agent Workflow

`src/skills/` is an AI Agent knowledge layer — not runtime code. It helps coding agents understand Bukit CLI, configuration, themes, templates, Notion, routing, i18n, deployment, SEO/GEO, and debugging.

- Intended for: Codex CLI, Claude Code, Copilot CLI, Gemini CLI, and similar tools
- Normal users: start from [`guide/user`](guide/user/README.md)
- Agent users: start from [`src/skills/using-bukit/SKILL.md`](src/skills/using-bukit/SKILL.md) or [`src/skills/bukit-cli-reference/SKILL.md`](src/skills/bukit-cli-reference/SKILL.md)
- Skill catalog: [`src/skills/README.md`](src/skills/README.md)

## Deployment

A GitHub Actions workflow template is at [`.github/workflows/release.yml`](.github/workflows/release.yml).

1. Go to GitHub **Settings → Pages** and choose "GitHub Actions"
2. If using Notion, add `NOTION_TOKEN` to repository secrets
3. Push to `main` — the workflow builds and deploys your site

See [`guide/user/13-deploy-github-pages.md`](guide/user/13-deploy-github-pages.md) for detailed guidance.

## Project Status

**Bukit Core 1.0 Stable**

Stability commitments:

- CLI: build / doctor / config / preview / clean
- `content.sources[]` config contract
- Markdown source
- Notion source
- `content.media`
- collection-based routing
- Scriban rendering
- safe output filesystem
- SEO / RSS / sitemap / JSON Feed
- GEO / `llms.txt` / publish audit
- build reports
- incremental build
- Native AOT CLI
- GitHub Pages deployment

**Next Stage / Preview**

Not in the 1.0 stability commitment:

- theme registry
- clone-to-theme
- import html-demo workflow
- import seed
- notion push / Notion migration
- external plugin ecosystem
- plugin marketplace
- BukitJalil
- advanced AI automation

## Roadmap

| Area | Status |
|---|---|
| Build, preview, routing, templates | Stable |
| Markdown, Notion, SEO/GEO | Stable |
| Theme ecosystem, template tooling | Improving |
| AI intent workflow | Improving |
| BukitJalil control panel | Future |
| Plugin marketplace / registry | Future |
| Broader knowledge-source integrations | Future |

## Contributing

Contributions are welcome. See the developer guide for architecture docs, testing procedures, and contribution workflows:

- [`guide/dev/README.md`](guide/dev/README.md)
- [`guide/dev/testing-smoke.md`](guide/dev/testing-smoke.md)

## License

This project is licensed under the terms in [LICENSE](./LICENSE).
