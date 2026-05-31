# Bukit Developer Guide (Maintenance and Extension)

Language versions: English (current) | [简体中文](./README.zh-CN.md) | [Bahasa Melayu](./README.ms.md)

This directory is for maintainers and contributors. It explains stable contracts (configuration/parameters/data models) and implementation details (pipeline/incremental build/plugin loading) for safe and fast iteration.

## Fastest Onboarding Path

1. Run the example site first (see [CLI](./cli.md)).
2. Understand `site.yaml` fields and validations (see [Config](./config-site-yaml.md)).
3. Learn the end-to-end flow: Config →Content →Routing →Rendering →Plugins →Output (see [Architecture](./architecture.md)).

## Maintainer Task Map

| Task | Start here |
|---|---|
| Change CLI behavior | [CLI argument reference](./cli.md) |
| Change `site.yaml` schema | [`site.yaml` field reference](./config-site-yaml.md) |
| Change content model | [Content system](./content.md) |
| Change routing | [Routing system](./routing.md) |
| Change rendering / Scriban | [Rendering (Scriban)](./rendering-scriban.md) |
| Change theme behavior | [Theme development](./theme.md), [Theme source](./theme-source.md) |
| Change plugins | [Plugin system](./plugins.md), [Built-in plugins](./built-in-plugins.md) |
| Change Notion provider | [Content system](./content.md) (Notion section) |
| Change AOT / performance | [AOT](./aot.md), [Performance notes](./perf-aot-governance.md) |
| Change release / docs process | [Documentation governance](./documentation-governance.md), [Release checklist](./release-checklist.md) |

## If You Maintain Bukit Through AI / Agents

If you maintain Bukit in a skill-aware environment such as Trae, Claude Code, Copilot CLI, Codex CLI, or Gemini CLI, use `src/skills/` as the agent-facing Bukit navigation layer and this directory as the maintainer-facing contract and implementation reference.

- Agent skills overview: [`src/skills`](../../src/skills/README.md)
- Unified entry: [`using-bukit`](../../src/skills/using-bukit/SKILL.md)
- Command execution reference: [`bukit-cli-reference`](../../src/skills/bukit-cli-reference/SKILL.md)
- This maintainer guide remains the source for architecture, configuration contracts, rendering internals, plugins, observability, testing, and operational governance

## Navigation

- [Code Wiki (repository overview)](./code-wiki.md)
- [Module call graph](./code-wiki-call-graph.md)
- [30-minute onboarding for new developers](./new-developer-30min.md)
- [Entry points by change type](./maintainer-entrypoints.md)
- [Architecture review draft](./architecture-review.md)
- [Architecture and module boundaries](./architecture.md)
- [Maintenance governance checklist](./governance-checklist.md)
- [CLI argument reference](./cli.md)
- [`site.yaml` field reference](./config-site-yaml.md)
- [Init/Create scaffolding](./init-create.md)
- [Content system (Markdown / Notion / sources)](./content.md)
- [Routing system](./routing.md)
- [Rendering and templates (Scriban)](./rendering-scriban.md)
- [Theme development](./theme.md)
- [Git theme source](./theme-source.md)
- [Modules data source (`mode=data`)](./modules-data.md)
- [Engine fixed outputs](./engine-outputs.md)
- [Plugin system](./plugins.md)
- [Built-in plugin outputs and boundaries](./built-in-plugins.md)
- [Intent CLI integration](./intent-cli.md)
- [AOT vs non-AOT build modes](./aot.md)
- [Performance/AOT/governance notes](./perf-aot-governance.md)
- [Publish and deploy](./publish-deploy.md)
- [Incremental build](./incremental-build.md)
- [Cache and clean](./cache-clean.md)
- [Doctor checks](./doctor.zh-CN.md) (currently available in Chinese and Malay)
- [Observability (logs and metrics)](./observability.md)
- [I18n and SEO](./i18n-seo.md)
- [Webhook trigger and security constraints](./webhook.md)
- [Testing and smoke acceptance](./testing-smoke.md)
- [Documentation governance](./documentation-governance.md)
- [Release checklist](./release-checklist.md)
- [Public preview scope](./public-preview-scope.md)

## How to Use Other Docs in This Repository

The `docs/` directory focuses on product/proposal/acceptance topics. This `guide/dev` directory links to those sources instead of duplicating content.

Common entries:

- AI site building guide: [chatgpt/README.md](../ai/chatgpt/README.md)
- Intent contract and mapping: [intent-cli.md](./intent-cli.md)
- Notion schema template: [content.md](./content.md)
- Enterprise website modules modeling: [modules-data.md](./modules-data.md)
- Acceptance docs: [testing-smoke.md](./testing-smoke.md)

## Quick Concepts

- `ContentItem`: unified content model from Markdown or Notion.
- Meta vs Fields: Meta drives engine behavior; Fields are consumed by templates (`page.fields.*.value`).
- `mode=content` vs `mode=data`: content creates routes/pages; data injects into `site.modules`.
- Plugins: two lifecycle hooks (`derive-pages`, `after-build`).

Canonical reference: [README.zh-CN.md](./README.zh-CN.md)

