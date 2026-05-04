# Bukit Developer Guide (Maintenance and Extension)

Language versions: English (current) | [简体中文](./README.zh-CN.md) | [Bahasa Melayu](./README.ms.md)

This directory is for maintainers and contributors. It explains stable contracts (configuration/parameters/data models) and implementation details (pipeline/incremental build/plugin loading) for safe and fast iteration.

## Fastest Onboarding Path

1. Run the example site first (see [CLI](./cli.md)).
2. Understand `site.yaml` fields and validations (see [Config](./config-site-yaml.md)).
3. Learn the end-to-end flow: Config → Content → Routing → Rendering → Plugins → Output (see [Architecture](./architecture.md)).

## Navigation

- [Code Wiki (repository overview)](./code-wiki.md)
- [Module call graph](./code-wiki-call-graph.md)
- [30-minute onboarding for new developers](./new-developer-30min.md)
- [Entry points by change type](./maintainer-entrypoints.md)
- [Architecture review draft](./architecture-review.md)
- [Architecture and module boundaries](./architecture.md)
- [CLI argument reference](./cli.md)
- [`site.yaml` field reference](./config-site-yaml.md)
- [Init/Create scaffolding](./init-create.md)
- [Content system (Markdown / Notion / sources)](./content.md)
- [Routing system](./routing.md)
- [Rendering and templates (Scriban)](./rendering-scriban.md)
- [Theme development](./theme.md)
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
- [Doctor checks](./doctor.md)
- [Observability (logs and metrics)](./observability.md)
- [I18n and SEO](./i18n-seo.md)
- [Webhook trigger and security constraints](./webhook.md)
- [Testing and smoke acceptance](./testing-smoke.md)

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

Full Chinese source: [README.md](./README.md)
