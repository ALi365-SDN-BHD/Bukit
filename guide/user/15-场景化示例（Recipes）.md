# 15 场景化示例（Recipes）：按“我要达到的效果”一步一步做

本页把常见需求按“目标 → 配置 → 数据 → 命令”组织，适合直接照做。

建议你把它当作“菜谱”：先完全复刻一份跑通，再按你的需求删改。

## Recipe 1：最小博客（Markdown）

### 目标

- 只用本地 Markdown 写博客
- 生成 blog 列表与文章页（取决于主题）

### 配置（site.yaml）

```yaml
site:
  name: my-blog
  title: My Blog
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
content:
  provider: markdown
  markdown:
    dir: content
    defaultType: post
build:
  output: dist
  clean: true
theme:
  name: alt
logging:
  level: info
```

> **推荐：显式声明 site.collections。** 以上配置依赖 post/page 兼容路由。更规范的做法是添加：
> ```yaml
> site:
>   collections:
>     post:
>       permalink: /blog/{slug}/
>       template: pages/post.html
>       listRoute: /blog/
> ```

### 模拟数据（content/）

`content/2026-01-first.md`

```markdown
---
type: post
title: 第一篇文章
slug: first
publishAt: 2026-01-01T10:00:00+08:00
tags: [demo]
categories: updates
summary: 这是第一篇文章
---

# 第一篇文章

Hello Bukit.
```

### 构建与预览

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config site.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c Release -- preview --dir dist --port auto
```

## Recipe 2：多语言站点（Markdown 双语）

### 目标

- zh-CN + en-US 双语输出
- 每条内容标记 language

### 配置

直接参考可运行示例：`examples/starter/site.i18n.yaml`。

最小版本：

```yaml
site:
  name: my-i18n
  title: My i18n Site
  baseUrl: /
  language: zh-CN
  languages: [zh-CN, en-US]
  defaultLanguage: zh-CN
  timezone: Asia/Shanghai
content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page
build:
  output: dist
  clean: true
theme:
  name: alt
```

> **推荐：为多语言站点显式声明 collections。** 新增 `site.collections.page` 和 `site.collections.post` 可以让路由更可控，尤其是在多语言 + collection 组合时。

### 模拟数据

`content/greeting-zh.md`

```markdown
---
type: page
title: 你好
slug: greeting
language: zh-CN
---

# 你好
```

`content/greeting-en.md`

```markdown
---
type: page
title: Hello
slug: greeting
language: en-US
---

# Hello
```

## Recipe 3：企业官网首页（Modules data + 主题）

### 目标

- 首页由 banner/features/faq/pricing/footer 这些模块拼装
- 模块内容从 `data/` 管理，模板读取 `site.modules.*`

### 配置

直接参考可运行示例：`examples/starter/site.modules.yaml`。

### 模拟数据（复刻三块）

`data/banner-1.md`

```markdown
---
type: banner
title: Banner 1
order: 1
locale: zh-CN
image: https://example.com/banner.png
link: https://example.com/
---
```

`data/features-main.md`

```markdown
---
type: features
title: 核心能力
order: 10
locale: zh-CN
f1_title: 快速
f1_desc: 10 分钟上手
f2_title: 可控
f2_desc: 配置驱动，模板可扩展
---
```

`data/footer-main.md`

```markdown
---
type: footer
title: Footer
order: 100
locale: zh-CN
copyright: "© 2026 My Site"
---
```

### 模板要做什么

在主题模板里读取：

- `site.modules.banner`
- `site.modules.features`
- `site.modules.footer`

示例见：[09-Modules-结构化数据](./09-Modules-结构化数据.md)。

## Recipe 4：Notion 当 CMS（只渲染 Published）

### 目标

- 内容由运营在 Notion 数据库维护
- 只渲染 Published=✅ 的内容

### 配置（site.yaml）

```yaml
site:
  name: notion-site
  title: Notion Site
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
content:
  provider: notion
  notion:
    databaseId: "你的数据库ID"
    filterProperty: Published
    filterType: checkbox_true
    sortProperty: PublishAt
    sortDirection: descending
    fieldPolicy:
      mode: whitelist
      allowed: [seo_title, seo_desc, cover, reading_time]
build:
  output: dist
  clean: true
theme:
  name: alt
```

> **推荐：声明 site.collections 并配合 Notion Collection 字段。** 建议在 site.yaml 中添加 `site.collections` 节点，同时在 Notion 数据库中创建 `Collection` 字段（select 类型），使引擎优先使用 collection 驱动路由。

### 运行要点

- 本地先设置 `NOTION_TOKEN` 环境变量
- 再执行 `doctor` 和 `build`

详细见：[06-内容-Notion](./06-内容-Notion.md)。

## Recipe 5：多站点（同仓库维护 main + blog）

### 目标

- 仓库根目录是主站
- `sites/blog.yaml` 是 blog 站

### 操作

1. 在 `sites/blog.yaml` 写 blog 配置（可参考 `examples/starter/sites/blog.yaml`）
2. 构建 blog：

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --site blog --clean --site-url https://example.com
```

## Recipe 6：GitHub Pages 项目仓库部署（解决资源 404）

### 目标

- 部署到 `https://<owner>.github.io/<repo>/`
- 页面和资源都正常加载

### 关键点

构建时必须传：

```bash
--base-url /<repo> --site-url https://<owner>.github.io/<repo>
```

完整说明见：[13-部署-GitHub-Pages](./13-部署-GitHub-Pages.md)。
