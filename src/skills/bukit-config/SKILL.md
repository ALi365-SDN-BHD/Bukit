---
name: bukit-config
description: Use when creating or modifying site.yaml, asking about the meaning of a configuration field, encountering config validation errors, or needing to configure a specific Bukit feature (collections, taxonomy, i18n, plugins, media) through YAML
---

# Bukit 站点配置

## Overview

`site.yaml` 是 Bukit 的唯一配置入口，采用约定优于配置哲学。六个顶级节点：`site`、`content`、`build`、`theme`、`taxonomy`、`logging`。大部分字段有合理默认值，最小可用的 site.yaml 仅需约 20 行。

## 配置模型速查

| 节点 | 职责 | 关键字段 |
|------|------|---------|
| `site` | 站点元信息和全局行为 | name, title, url, baseUrl, language, collections, plugins, externalPlugins |
| `content` | 内容源定义 | provider (notion/markdown), sources, media |
| `build` | 构建行为 | output, clean, draft, listPageContentMode |
| `theme` | 主题定位 | name, layouts, assets, static, params |
| `taxonomy` | 分类法配置 | template, kinds, pageSize, outputMode |
| `logging` | 日志级别 | level (debug/info/warn/error) |

## 场景模板

### 博客站（Markdown 内容源）

```yaml
site:
  name: my-blog
  title: 我的博客
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
  collections:
    post:
      permalink: /blog/{year}/{month}/{slug}/
      template: pages/post.html
      listRoute: /blog/
      pagination:
        enabled: true
        pageSize: 10
    page:
      permalink: /{slug}/
      template: pages/page.html

content:
  provider: markdown
  markdown:
    dir: content
    defaultType: post

build:
  output: dist
  clean: true

theme:
  name: starter
  layouts: layouts
  assets: assets
  static: static

taxonomy:
  template: pages/page.html
  pageSize: 10
  kinds:
    - key: tags
    - key: categories

logging:
  level: info
```

### 文档站（扁平化 URL）

```yaml
site:
  name: my-docs
  title: 项目文档
  baseUrl: /
  collections:
    doc:
      permalink: /docs/{slug}/
      template: pages/page.html
      listRoute: /docs/
```

### 多语言站点

```yaml
site:
  name: my-site
  title: My Site
  baseUrl: /
  language: zh-CN
  languages: [zh-CN, en]
  defaultLanguage: zh-CN
  sitemapMode: merged
  rssMode: merged
  searchMode: merged
```

### Notion 驱动站点

```yaml
site:
  name: my-notion-site
  title: My Notion Site
  baseUrl: /
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/

content:
  provider: notion
  notion:
    databaseId: "your-database-id"
    filterProperty: Published
    filterType: checkbox_true

theme:
  name: starter
```

## 字段详解

### site 节点

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `name` | string | **必填** | 站点标识名 |
| `title` | string | **必填** | 站点标题，模板中 `{{ site.title }}` |
| `url` | string | — | 站点完整 URL，必须以 `http://` 或 `https://` 开头 |
| `description` | string | — | 站点描述 |
| `baseUrl` | string | `/` | 站点根路径，必须以 `/` 开头。子目录部署时设为 `/subpath/` |
| `language` | string | `zh-CN` | 默认内容语言 |
| `timezone` | string | `Asia/Shanghai` | IANA 时区标识 |
| `languages` | string[] | — | 多语言列表，如 `[zh-CN, en]` |
| `defaultLanguage` | string | 首个 language | 多语言模式下的默认语言 |
| `outputPathEncoding` | string | `none` | 输出路径编码：`none`/`slug`/`urlencode`/`sanitize` |
| `sitemapMode` | string | `split` | Sitemap 模式：`split`/`merged`/`index` |
| `rssMode` | string | `split` | RSS 模式：`split`/`merged` |
| `searchMode` | string | `split` | 搜索索引模式：`split`/`merged`/`index` |
| `autoSummary` | bool | false | 自动生成摘要 |
| `autoSummaryMaxLength` | int | 200 | 自动摘要最大长度（1-5000） |
| `pluginFailMode` | string | `strict` | 插件失败策略：`strict`/`warn` |
| `deriveConflictPolicy` | string | `fail` | 派生页面路由冲突策略：`fail`/`warn`/`last-wins` |
| `externalAssemblyTrustMode` | string | `warn` | 外部程序集信任模式：`strict`/`warn` |
| `searchIncludeDerived` | bool | false | 搜索索引是否包含派生页面 |
| `externalProtocolIncludeRoutedPages` | bool | false | 外部协议插件是否接收已路由页面 |
| `collections` | map | — | 集合路由定义 |
| `plugins` | map | — | 插件开关（`{pluginName: {enabled: false}}`） |
| `externalPlugins` | map | — | 外部插件配置 |
| `externalAssemblyAllowlist` | map | — | 外部程序集白名单（`{文件名: SHA256}`） |
| `permalinks` | map | — | 全局永久链接自定义占位符 |

### content 节点

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `provider` | string | **必填** | 内容提供者：`notion` 或 `markdown` |
| `sources` | array | — | 多源内容配置（与 provider 互斥） |
| `notion` | map | — | Notion 内容源配置 |
| `markdown` | map | — | Markdown 内容源配置 |
| `media` | map | — | 媒体文件处理（下载、URL 重写） |

#### content.notion

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `databaseId` | string | **必填** | Notion 数据库 ID |
| `pageSize` | int | 50 | 分页大小（1-100） |
| `maxItems` | int | — | 最大拉取条目数 |
| `renderContent` | bool | — | 是否渲染页面块内容 |
| `renderConcurrency` | int | — | 块渲染并发度 |
| `maxRps` | int | — | API 请求速率限制 |
| `maxRetries` | int | — | 请求失败重试次数 |
| `filterProperty` | string | `Published` | 过滤属性名 |
| `filterType` | string | `checkbox_true` | 过滤类型：`checkbox_true`/`none` |
| `sortProperty` | string | — | 排序属性名 |
| `sortDirection` | string | `ascending` | 排序方向：`ascending`/`descending` |
| `includeSlugs` | string[] | — | 仅包含指定 slug 的页面 |
| `includeSlugProperty` | string | `Slug` | slug 属性名 |
| `cacheMode` | string | `off` | 缓存模式：`off`/`readwrite`/`readonly` |
| `cacheDir` | string | — | 缓存目录 |
| `fieldPolicy.mode` | string | `whitelist` | 字段策略：`whitelist`/`all` |
| `fieldPolicy.allowed` | string[] | — | 白名单允许的字段名列表 |

#### content.markdown

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `dir` | string | `content` | Markdown 文件目录（相对路径） |
| `defaultType` | string | `page` | 默认内容类型（映射到集合） |
| `maxItems` | int | — | 最大条目数 |
| `includePaths` | string[] | — | 指定的文件路径列表 |
| `includeGlobs` | string[] | — | Glob 模式过滤 |

#### content.media

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `downloadToLocal` | bool | true | 是否将远程图片下载到本地 |
| `downloadDir` | string | `assets/uploads` | 下载目录 |
| `urlBase` | string | `/assets/uploads` | HTML 中替换后的 URL 前缀 |
| `defaultImageUrl` | string | `/assets/images/noneimg-news.jpg` | 默认图片 URL |
| `fieldKeys` | string[] | `[cover,image,thumbnail,...]` | 要处理的图片字段 key |
| `maxConcurrency` | int | 4 | 下载并发度 |
| `maxRetries` | int | 3 | 下载重试次数 |
| `timeoutMs` | int | 10000 | 下载超时（毫秒） |
| `maxFileSizeBytes` | int | 52428800 | 最大文件大小（50MB） |
| `blockPrivateNetworks` | bool | true | 阻止下载内网地址 |

#### content.sources（多源模式）

```yaml
content:
  sources:
    - type: notion
      mode: content        # content 或 data
      notion:
        databaseId: "xxx"
    - type: markdown
      mode: data           # data 类型输出到 site.data 供模板使用
      name: faq            # 可选数据模块名
      markdown:
        dir: data/faq
```

`sources` 与 `provider` 互斥。`mode` 为 `data` 时内容进入 `site.data.<name>` 或 `site.data`，不生成页面路由。

### build 节点

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `output` | string | `dist` | 输出目录（相对路径，不能含 `..`） |
| `clean` | bool | true | 构建前是否清空输出目录 |
| `draft` | bool | false | 是否渲染草稿（draft: true 的页面） |
| `listPageContentMode` | string | `auto` | 列表页内容模式：`auto`/`always`/`never` |

### theme 节点

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `name` | string | — | 主题名（对应 `themes/<name>/`） |
| `layouts` | string | `layouts` | 模板子目录名 |
| `assets` | string | `assets` | 资源子目录名（SCSS 等需处理） |
| `static` | string | `static` | 静态文件子目录名（直接复制） |
| `params` | map | — | 主题参数，模板中 `{{ site.theme.params.xxx }}` |

### taxonomy 节点

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `template` | string | `pages/page.html` | 分类术语页默认模板 |
| `indexTemplate` | string | — | 分类索引页模板 |
| `termTemplate` | string | — | 分类术语页模板（覆盖 template） |
| `pageSize` | int | 10 | 每页条目数 |
| `indexEnabled` | bool | true | 是否生成分类索引页 |
| `outputMode` | string | `both` | 输出模式：`both`/`pages`/`data`/`fields_only` |
| `pinField` | string | `pinned` | 置顶字段名 |
| `pinOrderField` | string | — | 置顶排序字段 |
| `itemFields` | string[] | — | 从页面元数据提取为分类依据的字段 |
| `kinds` | array | — | 自定义分类法定义 |

#### taxonomy.kinds

```yaml
taxonomy:
  kinds:
    - key: tags            # 字段名（必填）
      title: 标签          # 显示名
      template: pages/tag.html
      indexEnabled: true
    - key: categories
      title: 分类
      singularTitlePrefix: "分类: "   # 页面标题前缀
```

### logging 节点

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `level` | string | `info` | 日志级别：`debug`/`info`/`warn`/`error` |

CLI 可用 `--log-format json` 切换 JSON 格式输出。

## 集合配置 (site.collections)

集合路由是 Bukit 的核心路由模型。每个条目必须声明：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `permalink` | string | **是** | URL 模式，**必须包含 `{slug}`**。支持 `{slug}`/`{year}`/`{month}`/`{day}`/`{type}` |
| `template` | string | **是** | 模板文件路径（如 `pages/post.html`） |
| `listRoute` | string | — | 列表页路由，必须以 `/` 开头 |
| `pagination.enabled` | bool | — | 启用分页 |
| `pagination.pageSize` | int | 10 | 每页条目数（正整数） |
| `output.rss` | bool | true | 集合是否生成 RSS |
| `output.sitemap` | bool | true | 集合是否加入 Sitemap |
| `output.archive` | bool | false | 是否生成按年归档 |

## CLI 参数覆盖

`bukit build` 支持通过 CLI 参数覆盖部分配置：

```
--output <dir>      覆盖 build.output
--base-url <url>    覆盖 site.baseUrl
--site-url <url>    覆盖 site.url
--draft             覆盖 build.draft = true
--clean             强制 clean
--no-clean          禁用 clean
--incremental       启用增量构建
--no-incremental    禁用增量构建
--jobs <n>          覆盖并行渲染并发度
```

这些覆盖仅影响当次构建，不修改 site.yaml。

## 常见配置错误

| 错误信息 | 原因 | 修复 |
|---------|------|------|
| `site.name is required` | 未设置站点名 | 添加 `site.name: my-site` |
| `site.title is required` | 未设置标题 | 添加 `site.title: My Site` |
| `site.baseUrl must start with '/'` | baseUrl 格式错误 | 改为 `/` 或 `/subpath/` |
| `site.collections.xxx.permalink must include {slug}` | permalink 缺少 {slug} 占位符 | 添加 `{slug}`，如 `/blog/{slug}/` |
| `site.collections.xxx.template is required` | 集合未指定模板 | 添加 `template: pages/post.html` |
| `site.collections.xxx.listRoute must start with '/'` | listRoute 格式错误 | 改为 `/blog/` |
| `content.provider is required` | 未指定内容源 | 设 `provider: markdown` 或 `provider: notion` |
| `NOTION_TOKEN is required...` | Notion API 密钥未设置 | 设置环境变量 `NOTION_TOKEN` |
| `content.notion.databaseId is required` | 未填数据库 ID | 填入 Notion 数据库 ID |
| `site.timezone '...' is not a valid time zone identifier` | 时区标识无效 | 使用 IANA 时区名，如 `Asia/Shanghai` |
| `build.output must not contain '..'` | 路径含 `..` 遍历 | 使用相对路径如 `dist` |
| `taxonomy.pageSize must be a positive integer` | 分页大小非正整数 | 设为正整数 |
| `site.collections keys must be non-empty` | 集合名为空字符串 | 确保集合名非空 |
| `site.languages has duplicate language` | 语言列表重复 | 去除重复项 |
| `site.defaultLanguage must be included in site.languages` | 默认语言不在列表中 | 将默认语言加入 languages |

## 清单：新站点配置检查

使用 `bukit doctor` 可自动检查大部分项目。配置完成后逐项确认：

- [ ] `site.name` 和 `site.title` 已设置
- [ ] `site.baseUrl` 以 `/` 开头
- [ ] `site.url`（如设置）以 `http://` 或 `https://` 开头
- [ ] `site.collections` 中每个集合都有 permalink（含 `{slug}`）、template
- [ ] `content.provider` 已设置，对应子配置已填写
- [ ] Notion 模式下 `NOTION_TOKEN` 环境变量已设置
- [ ] `build.output` 是合法相对路径
- [ ] `theme` 节点指向存在的主题目录
- [ ] 多语言时 `languages` 和 `defaultLanguage` 一致
