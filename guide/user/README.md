# Bukit User Guide

Language versions: English (current) | [简体中文](./README.zh-CN.md) | [Bahasa Melayu](./README.ms.md)

This directory is for site users (not engine maintainers). It helps you build and deploy static websites from Markdown/Notion content, with practical setup and troubleshooting guidance.

If you need internals, extension points, or contribution details, read the developer guide: [guide/dev](../dev/README.md).

## Recommended Reading Paths

### First-time setup (local Markdown)

1. [01-快速开始](./01-快速开始.md)
2. [04-配置-site-yaml](./04-配置-site-yaml.md)
3. [05-内容-Markdown](./05-内容-Markdown.md)
4. [12-命令行参考](./12-命令行参考.md)
5. [13-部署-GitHub-Pages](./13-部署-GitHub-Pages.md)

### Using Notion as CMS

1. [01-快速开始](./01-快速开始.md)
2. [06-内容-Notion](./06-内容-Notion.md)
3. [10-内置功能与输出](./10-内置功能与输出.md)
4. [13-部署-GitHub-Pages](./13-部署-GitHub-Pages.md)
5. [14-故障排查](./14-故障排查.md)

### Company website / landing page (Modules data)

1. [07-内容-多源-sources](./07-内容-多源-sources.md)
2. [09-Modules-结构化数据](./09-Modules-结构化数据.md)
3. [08-主题与模板](./08-主题与模板.md)
4. [15-场景化示例（Recipes）](./15-场景化示例（Recipes）.md)

### Conversational site building (ChatGPT / official GPT)

1. Prompt Pack: [ai/chatgpt](../ai/chatgpt/README.md)
2. Intent contract (AI ↔ Bukit): [guide/dev/intent-cli](../dev/intent-cli.md)
3. Required commands (`validate/doctor/build`): [12-命令行参考](./12-命令行参考.md)

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

Full Chinese source: [README.md](./README.md)
