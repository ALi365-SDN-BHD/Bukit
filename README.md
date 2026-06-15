# Bukit - .NET Native AOT Static Site Engine

<p align="center">
  <img src="docs/bukit-logo.svg" alt="bukit logo" width="400">
</p>

Language versions: English (current) | [简体中文](./README.zh-CN.md) | [Bahasa Melayu](./README.ms.md)

Bukit is a .NET Native AOT static site generation engine for **Notes-as-CMS**, **AI agent workflows**, and **GEO-ready websites**. It turns Markdown and Notion content into fast, deployable static sites.

## What Bukit Is

Bukit is the runtime and build engine:

- content ingestion
- route generation
- Scriban template rendering
- SEO, GEO, feed, sitemap, and audit outputs
- static HTML generation
- GitHub Pages deployment through the Core CLI

BukitJalil is a separate local control panel. It is not part of the Bukit runtime, and it is not required to build a site with Bukit.

Bukit is not a SaaS platform, a full CMS backend, a visual page builder, or a replacement for BukitJalil.

## Core 1.0 Capabilities

- **Native AOT CLI**: fast startup, low memory, single-binary distribution for Linux, macOS, and Windows.
- **Content sources**: direct Core providers are Markdown and Notion.
- **Notes-as-CMS**: Obsidian and other notes apps are supported through Markdown-compatible exports. Feishu, Yuque, and other direct knowledge-base integrations are future work.
- **Scriban templates**: layouts, partials, snippets, collection pages, pagination, taxonomy, and multilingual output.
- **Filesystem themes**: local `themes/<name>/` directories with layouts, assets, static files, and optional `theme.yaml`.
- **SEO and GEO outputs**: sitemap, RSS/Atom/JSON Feed, JSON-LD, Open Graph, Twitter Cards, canonical URLs, hreflang, `llms.txt`, `robots.txt`, SEO audit, GEO audit, and publish audit reports.
- **LiveReload development server**: watches content and theme files, rebuilds incrementally, broadcasts over WebSocket, and refreshes the browser.
- **Static preview server**: serves an already-built output directory.
- **GitHub Pages deployment**: `deploy.provider: github-pages` plus the `bukit deploy` command.

## Quick Start

When developing Bukit from this repository:

```bash
dotnet build bukit.slnx -c Release
dotnet run --project src/Bukit.Cli -c Release -- config check --config path/to/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- doctor --config path/to/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config path/to/site.yaml --clean --site-url https://example.com
```

When using an installed or downloaded Bukit binary from a site directory:

```bash
bukit config check
bukit doctor
bukit build --clean
bukit dev
```

Use `bukit preview --dir dist` when you only want to serve an existing build output.

## Core CLI Commands

Bukit Core 1.0 exposes only this stable command surface:

| Command | Purpose |
|---|---|
| `build` | Build the static site |
| `doctor` | Diagnose config, templates, providers, and build readiness |
| `config` | Validate config or generate the config schema |
| `preview` | Serve an already-built output directory |
| `dev` | Run the LiveReload development server |
| `clean` | Remove output and cache directories |
| `version` | Print version information |
| `completion` | Generate shell completion |
| `seo` | Audit or diff SEO reports |
| `geo` | Audit GEO and `llms.txt` outputs |
| `publish` | Audit or diff publish-readiness reports |
| `deploy` | Deploy a built site to GitHub Pages |

Stable subcommands are `config check`, `config schema`, `seo audit`, `seo diff`, `geo audit`, `publish audit`, and `publish diff`.

## Minimal `site.yaml`

```yaml
site:
  name: my-site
  title: My Site
  url: https://example.com
  baseUrl: /
  language: en
  collections:
    page:
      permalink: /{slug}/
      template: pages/page.html
      listRoute: /
      listTemplate: pages/index.html
content:
  sources:
    - type: markdown
      name: pages
      mode: content
      collection: page
      markdown:
        dir: content
build:
  output: dist
  clean: true
theme:
  name: starter
logging:
  level: info
```

Notion is also a Core content provider. Add a `notion` source under `content.sources[]` and provide `NOTION_TOKEN` through the environment, never inside `site.yaml`.

## Theme Basics

A Core theme is a local filesystem directory:

```text
themes/<name>/
  layouts/
    layouts/base.html
    pages/page.html
    pages/post.html
    pages/index.html
    pages/list.html
    partials/
  assets/
  static/
  theme.yaml
```

Use `theme.name` in `site.yaml` to select the theme. Remote theme sources, theme registries, theme installation, and theme marketplace workflows are not part of Core 1.0.

## Development And Preview

`bukit dev` is a LiveReload development server. It performs an initial build, watches content, layout, asset, static, and active theme files, rebuilds incrementally, and refreshes connected browsers. It does not patch individual framework components in place.

`bukit preview` is a static file server for build output. Use it after `bukit build` when you do not need file watching.

## Deployment

For GitHub Pages deployment, configure the site:

```yaml
deploy:
  provider: github-pages
  branch: gh-pages
```

Then verify and deploy:

```bash
bukit config check
bukit doctor
bukit build --clean
bukit publish audit --dir dist
bukit deploy
```

For CI deployment, create a site-specific GitHub Pages workflow. The example at [`examples/github-pages-workflow.yml`](examples/github-pages-workflow.yml) is a site workflow starting point. Do not copy this repository's release workflow; it publishes Bukit binaries, not user sites.

## Documentation

| Area | Start here |
|---|---|
| Core user guide | [`guide/user`](guide/user/README.md) |
| Core developer guide | [`guide/dev`](guide/dev/README.md) |
| Core-aligned agent skills | [`guide/skills`](guide/skills/README.md) |
| Labs and preview workflows | [`guide/labs`](guide/labs/README.md) and [`guide/labs-skills`](guide/labs-skills/README.md) |
| Archived historical docs | [`guide/archive`](guide/archive/README.md) |
| CLI reference | [`guide/user/12-cli-reference.md`](guide/user/12-cli-reference.md) |
| Config reference | [`guide/user/04-site-yaml-config.md`](guide/user/04-site-yaml-config.md) |
| GitHub Pages deployment | [`guide/user/13-deploy-github-pages.md`](guide/user/13-deploy-github-pages.md) |

If a guide describes clone, import, intent, webhook, remote theme source, theme registry, or external plugin marketplace behavior, treat it as Labs, preview, or historical material unless it is explicitly promoted into the Core command whitelist.

## AI Agent Skills

Agent-facing instructions live under [`guide/skills`](guide/skills/README.md). That pack is aligned with Core 1.0 and should only teach stable Core commands and contracts.

Labs skills live under [`guide/labs-skills`](guide/labs-skills/README.md). They are opt-in and must not be treated as default Core behavior.

## Stability Scope

**Bukit Core 1.0 Stable** includes:

- CLI commands listed in [Core CLI Commands](#core-cli-commands)
- `content.sources[]` config contract
- Markdown and Notion providers
- Markdown-compatible notes exports through the Markdown provider
- `content.media`
- collection routing
- Scriban rendering
- local filesystem themes
- safe output filesystem behavior
- SEO, RSS, sitemap, JSON Feed, and search outputs
- GEO, `llms.txt`, and publish audit outputs
- build reports
- incremental builds
- Native AOT CLI
- GitHub Pages deployment

**Not included in Core 1.0**:

- clone-to-theme
- HTML demo import
- import seed workflows
- Notion push or Notion migration
- theme registry, theme marketplace, remote theme source, or theme install workflows
- external plugin ecosystem or plugin marketplace
- AI intent workflow
- webhook automation
- BukitJalil control panel
- broader direct integrations for Feishu, Yuque, and other knowledge bases

## Roadmap

| Area | Status |
|---|---|
| Build, preview, dev, routing, templates | Stable |
| Markdown, Notion, SEO/GEO, publish audit | Stable |
| GitHub Pages deployment | Stable |
| Theme ecosystem and template tooling | Labs / Future |
| AI intent workflow | Labs / Future |
| External plugin ecosystem and marketplace | Future |
| BukitJalil control panel | Future |
| Broader direct knowledge-source integrations | Future |

## Contributing

Contributions are welcome. See:

- [`guide/dev/README.md`](guide/dev/README.md)
- [`guide/dev/testing.md`](guide/dev/testing.md)
- [`guide/dev/documentation-governance.md`](guide/dev/documentation-governance.md)
- If you are preparing a release tag, run the repository precheck first: [`release-prerelease-template`](docs/release/release-prerelease-template.md)

## License

This project is licensed under the terms in [LICENSE](./LICENSE).
