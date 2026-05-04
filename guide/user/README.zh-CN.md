# Bukit 使用文档（普通用户）

语言版本：简体中文（当前）| [English](./README.md) | [Bahasa Melayu](./README.ms.md)

本目录面向“站点使用者”（不是引擎维护者）：你将学会如何用 Bukit 从内容（Markdown/Notion）生成可部署的静态网站，并在常见场景下正确配置与排障。

如果你需要了解内部实现、扩展点、或想贡献代码，请阅读开发者文档：[guide/dev](../dev/README.zh-CN.md)。

## 推荐阅读路径

### 第一次上手（本地 Markdown）

1. [01-快速开始](./01-快速开始.md)
2. [04-配置-site-yaml](./04-配置-site-yaml.md)
3. [05-内容-Markdown](./05-内容-Markdown.md)
4. [12-命令行参考](./12-命令行参考.md)
5. [13-部署-GitHub-Pages](./13-部署-GitHub-Pages.md)

### 用 Notion 当 CMS

1. [01-快速开始](./01-快速开始.md)
2. [06-内容-Notion](./06-内容-Notion.md)
3. [10-内置功能与输出](./10-内置功能与输出.md)
4. [13-部署-GitHub-Pages](./13-部署-GitHub-Pages.md)
5. [14-故障排查](./14-故障排查.md)

### 企业官网 / Landing Page（Modules 结构化数据）

1. [07-内容-多源-sources](./07-内容-多源-sources.md)
2. [09-Modules-结构化数据](./09-Modules-结构化数据.md)
3. [08-主题与模板](./08-主题与模板.md)
4. [15-场景化示例（Recipes）](./15-场景化示例（Recipes）.md)

### 对话式建站（ChatGPT / 官方 GPT）

1. Prompt Pack（可复制粘贴模板）：[ai/chatgpt](../ai/chatgpt/README.md)
2. Intent 契约（AI ↔ Bukit）：[guide/dev/intent-cli](../dev/intent-cli.md)
3. 必用命令（validate/doctor/build）：[12-命令行参考](./12-命令行参考.md)

## 目录导航

- [01-快速开始](./01-快速开始.md)
- [02-核心概念](./02-核心概念.md)
- [03-项目目录与约定](./03-项目目录与约定.md)
- [04-配置-site-yaml](./04-配置-site-yaml.md)
- [05-内容-Markdown](./05-内容-Markdown.md)
- [06-内容-Notion](./06-内容-Notion.md)
- [07-内容-多源-sources](./07-内容-多源-sources.md)
- [08-主题与模板](./08-主题与模板.md)
- [09-Modules-结构化数据](./09-Modules-结构化数据.md)
- [10-内置功能与输出](./10-内置功能与输出.md)
- [11-多语言与SEO](./11-多语言与SEO.md)
- [12-命令行参考](./12-命令行参考.md)
- [13-部署-GitHub-Pages](./13-部署-GitHub-Pages.md)
- [14-故障排查](./14-故障排查.md)
- [15-场景化示例（Recipes）](./15-场景化示例（Recipes）.md)
- [16-参数速查表](./16-参数速查表.md)

## 仓库内“可运行示例”在哪里

文档中的大多数示例都能在 `examples/starter/` 找到对照版本：

- 最小 Markdown 站点配置：[examples/starter/site.yaml](../../examples/starter/site.yaml)
- 多语言配置：[examples/starter/site.i18n.yaml](../../examples/starter/site.i18n.yaml)
- Modules（mode=data）配置：[examples/starter/site.modules.yaml](../../examples/starter/site.modules.yaml)
- Modules 模拟数据（banner/nav/faq...）：[examples/starter/data](../../examples/starter/data)
- 多站点配置示例（sites/）：[examples/starter/sites](../../examples/starter/sites)

## 深入阅读对照（用户操作 ↔ 开发者契约）

当你需要更“权威的字段表/边界/实现约束”（例如排查复杂 bug、或做主题/插件深度定制），按下表跳转到开发者文档：

| 主题 | 用户文档 | 开发者文档 |
|---|---|---|
| CLI 参数与覆盖关系 | [12-命令行参考](./12-命令行参考.md) | [guide/dev/cli](../dev/cli.md) |
| site.yaml 字段与校验 | [04-配置-site-yaml](./04-配置-site-yaml.md) | [guide/dev/config-site-yaml](../dev/config-site-yaml.md) |
| 内容模型与字段归一化 | [05-内容-Markdown](./05-内容-Markdown.md)、[06-内容-Notion](./06-内容-Notion.md)、[07-内容-多源-sources](./07-内容-多源-sources.md) | [guide/dev/content](../dev/content.md) |
| 主题目录约定与模板模型 | [08-主题与模板](./08-主题与模板.md) | [guide/dev/theme](../dev/theme.md)、[guide/dev/rendering-scriban](../dev/rendering-scriban.md) |
| Modules（mode=data）注入规则 | [09-Modules-结构化数据](./09-Modules-结构化数据.md) | [guide/dev/modules-data](../dev/modules-data.md) |
| sitemap/rss/search 与派生页 | [10-内置功能与输出](./10-内置功能与输出.md) | [guide/dev/built-in-plugins](../dev/built-in-plugins.md)、[guide/dev/plugins](../dev/plugins.md) |
| 多语言与 SEO 输出细节 | [11-多语言与SEO](./11-多语言与SEO.md) | [guide/dev/i18n-seo](../dev/i18n-seo.md) |
| 缓存/增量构建与清理 | [14-故障排查](./14-故障排查.md) | [guide/dev/incremental-build](../dev/incremental-build.md)、[guide/dev/cache-clean](../dev/cache-clean.md) |

## 术语速览（用 1 分钟对齐）

- 站点配置：`site.yaml`（也可以放在 `sites/<name>.yaml` 作为多站点配置）
- 内容源（Content Provider）：从 Markdown/Notion 读取内容
- 页面/文章：由 `type: page|post`（或 Notion 的 Type 字段）决定默认路由与模板
- 主题（Theme）：模板 + 资源 + 静态文件目录约定
- Modules（结构化数据）：通过 `content.sources[].mode: data` 读取，不生成路由，只注入 `site.modules.*` 供模板渲染
- 内置产物：构建结束后额外生成的 `sitemap.xml` / `rss.xml` / `search.json` 等
