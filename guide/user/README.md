# Bukit User Guide

Language versions: English (current) | [简体中文](./README.zh-CN.md) | [Bahasa Melayu](./README.ms.md)

This directory is for site users (not engine maintainers). It helps you build and deploy static websites from Markdown/Notion content, with practical setup and troubleshooting guidance.

If you need internals, extension points, or contribution details, read the developer guide: [guide/dev](../dev/README.md).

## Recommended Reading Paths

### First-time setup (local Markdown)

1. [01 Quick Start](./01-quick-start.md)
2. [04 Site YAML Config](./04-site-yaml-config.md)
3. [05 Content Markdown](./05-markdown-content.md)
4. [12 CLI Reference](./12-cli-reference.md)
5. [13 Deploy GitHub Pages](./13-deploy-github-pages.md)

### Using Notion as CMS

1. [01 Quick Start](./01-quick-start.md)
2. [06 Content Notion](./06-notion-content.md)
3. [10 Built-in Features & Output](./10-built-in-features.md)
4. [13 Deploy GitHub Pages](./13-deploy-github-pages.md)
5. [14 Troubleshooting](./14-troubleshooting.md)

### Company website / landing page (Modules data)

1. [07 Multi-Source (Chinese)](./07-multi-source.zh-CN.md)
2. [09 Modules Structured Data](./09-modules-data.md)
3. [08 Themes & Templates](./08-themes-templates.md)
4. [15 Recipes](./15-recipes.md)

### Conversational site building (ChatGPT / official GPT)

1. Prompt Pack: [ai/chatgpt](../ai/chatgpt/README.md)
2. Intent contract (AI ↔ Bukit): [guide/dev/intent-cli](../dev/intent-cli.md)
3. Required commands (`validate/doctor/build`): [12 CLI Reference](./12-cli-reference.md)

## If You Use Bukit Through AI / Agents

If you use Bukit in a skill-aware environment such as Trae, Claude Code, Copilot CLI, Codex CLI, or Gemini CLI, treat `src/skills/` as the agent-facing navigation entry and this directory as the user-facing operating guide.

- Agent skills overview: [`src/skills`](../../src/skills/README.md)
- Unified entry: [`using-bukit`](../../src/skills/using-bukit/SKILL.md)
- Command execution reference: [`bukit-cli-reference`](../../src/skills/bukit-cli-reference/SKILL.md)
- This user guide still covers the complete operational path for setup, configuration, theming, content organization, deployment, and troubleshooting

## Where to Find Runnable Examples

Most examples in this guide have runnable counterparts in `examples/starter/`:

- Minimal Markdown config: [examples/starter/site.yaml](../../examples/starter/site.yaml)
- I18n config: [examples/starter/site.i18n.yaml](../../examples/starter/site.i18n.yaml)
- Modules (`mode=data`) config: [examples/starter/site.modules.yaml](../../examples/starter/site.modules.yaml)
- Modules mock data: [examples/starter/data](../../examples/starter/data)
- Multi-site examples: [examples/starter/sites](../../examples/starter/sites)

## Developer Cross-Reference

For authoritative field boundaries and implementation constraints, cross-check:

- CLI behavior: [guide/dev/cli](../dev/cli.md)
- `site.yaml` contract: [guide/dev/config-site-yaml](../dev/config-site-yaml.md)
- Content modeling: [guide/dev/content](../dev/content.md)
- Theme/template internals: [guide/dev/theme](../dev/theme.md), [guide/dev/rendering-scriban](../dev/rendering-scriban.md)
- Modules injection rules: [guide/dev/modules-data](../dev/modules-data.md)
- Built-in outputs and plugins: [guide/dev/built-in-plugins](../dev/built-in-plugins.md), [guide/dev/plugins](../dev/plugins.md)

## Quick Terms

- Site config: `site.yaml` (or `sites/<name>.yaml` for multi-site).
- Content provider: reads content from Markdown/Notion.
- Page/Post: controlled by `type: page|post` (or Notion `Type` field).
- Theme: template + assets + static directories.
- Modules data: loaded with `content.sources[].mode: data`; injected to `site.modules.*` only.
- Built-in outputs: `sitemap.xml`, `rss.xml`, `search.json`, etc.

Full Chinese source: [README.zh-CN.md](./README.zh-CN.md)
