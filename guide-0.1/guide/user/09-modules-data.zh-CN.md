# 09 Modules（结构化数据）：用 data 模块驱动企业官网/落地页

Modules 的定位是：**把“页面里的一块块结构化内容”从模板里抽出来，变成可配置的数据**。

典型的企业官网往往不是“很多独立页面”，而是“一个首页 + 若干栏目页”，每个页面由 banner、导航、features、faq、pricing、footer 等模块拼起来。Modules 就是为这个需求设计的。

对照可运行示例：

- 配置：`examples/starter/site.modules.yaml`
- 模拟数据：`examples/starter/data/*.md`

## 你将获得什么

- 如何配置 `mode: data`，让 modules 注入到模板变量 `site.modules`
- 推荐的模块字段与建模方式（适配 Markdown 与 Notion）
- 多语言 modules 的写法（locale）
- 3 个可直接复制的模块示例（banner/nav/faq）

## 第一步：在 sources 里开启 mode=data

```yaml
content:
  sources:
    - type: markdown
      name: content
      mode: content
      collection: page
      markdown:
        dir: content
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
```

这会带来一个关键行为：

- `mode: data` 的内容项 **不生成路由**（不会有 `/pages/...`）
- 它们会按 `type` 分组注入到 `site.modules.<type>[]`

例如 `type: banner` 的模块会出现在 `site.modules.banner`。

## 第二步：写模块数据（Markdown 模式）

模块数据也是 Markdown 文件，只是它的 `type` 代表“模块类型”而不是“页面/文章”。

### 示例 1：banner

文件：`data/banner-1.md`

```markdown
---
type: banner
title: Banner 1
order: 1
locale: zh-CN
image: https://example.com/banner-1.png
link: https://example.com/
---

Banner 1 body
```

对照可运行示例：`examples/starter/data/banner-1.md`。

### 示例 2：导航（nav）

文件：`data/nav-home.md`

```markdown
---
type: nav
title: Home Nav
order: 10
locale: zh-CN
items:
  - text: 首页
    href: /
  - text: 博客
    href: /blog/
  - text: 关于
    href: /pages/about/
---
```

说明：

- `items` 这种“列表结构”会进入 `page.fields.items.value`（主题如何取用取决于字段类型映射；推荐在主题里做容错）
- 如果你希望模板更稳定，建议把“可枚举结构”保持扁平（例如多个字段：`nav_1_text/nav_1_href`），或在 Notion 用数据库结构表达

### 示例 3：FAQ

文件：`data/faq-main.md`

```markdown
---
type: faq
title: FAQ
order: 30
locale: zh-CN
q1: Bukit 是什么？
a1: 一个静态站点引擎，支持 Markdown/Notion。
q2: 我需要写代码吗？
a2: 不需要，但你可以通过主题模板做深度定制。
---
```

## 推荐的“模块字段”约定（强烈建议统一）

Modules 没有强制 schema（它由你的主题决定），但为了可维护，建议所有模块都尽量包含以下通用字段：

| 字段 | 作用 | 备注 |
|---|---|---|
| `type` | 模块类型（分组键） | 必填；决定注入到哪个 `site.modules.<type>` |
| `title` | 模块标题 | 可选但推荐 |
| `order` | 排序 | 推荐数字，越小越靠前 |
| `locale` | 语言（多语言站点） | 例如 `zh-CN`/`en-US` |
| `enabled` | 开关（可选） | 用于快速下线某块内容 |

建议你的主题按 `order` 排序、按 `locale` 过滤，并对 `enabled=false` 的模块忽略。

## 在模板中使用 Modules（Scriban 示例）

### 1）渲染 banner 列表

```scriban
{{ for b in site.modules.banner }}
  <section class="banner">
    {{ if b.fields.image }}<img src="{{ b.fields.image.value }}" />{{ end }}
    <h2>{{ b.title }}</h2>
    {{ if b.fields.link }}<a href="{{ b.fields.link.value }}">立即查看</a>{{ end }}
  </section>
{{ end }}
```

### 2）按 locale 过滤（伪代码风格，具体以主题工具函数为准）

如果你的主题没有封装过滤工具，最简单的方式是在模板里做 `if`：

```scriban
{{ for m in site.modules.faq }}
  {{ if m.meta.locale == site.language }}
    ...
  {{ end }}
{{ end }}
```

你也可以把 locale 存在 fields 中（例如 `fields.locale.value`），按你主题的数据约定来。

## Notion 模式下的 Modules 建模建议（示例）

如果你希望运营同学在 Notion 中管理 modules，可以建一个 Modules 数据库（配合 sources 的 `mode: data`）：

推荐字段（示意）：

| 字段名 | 类型 | 说明 |
|---|---|---|
| `Enabled` | checkbox | 是否启用 |
| `Title` | title | 模块标题 |
| `Type` | select | banner/nav/faq/pricing...（模块类型） |
| `order` | number | 排序 |
| `locale` | select | zh-CN/en-US |
| `image` | files/url | banner 图等 |
| `link` | url | 跳转链接 |

配套 sources 配置示例：

```yaml
content:
  sources:
    - type: notion
      name: modules
      mode: data
      notion:
        databaseId: "db_modules"
        filterProperty: Enabled
        filterType: checkbox_true
        fieldPolicy: { mode: all }
```

## 常见问题

### 1）为什么 modules 没有出现在输出目录？

正常：modules 不会生成路由，所以不会出现 `dist/pages/...`。它们只会在模板渲染时影响页面 HTML。

### 2）为什么模板里 `site.modules.banner` 是空的？

检查：

- sources 里 modules 的 `mode` 是否为 `data`
- 模块数据里是否写了 `type: banner`
- 多语言站点是否被 locale 过滤掉（例如站点语言是 en-US，但你只录入了 zh-CN 的 modules）

### 3）配置多个 `mode: data` 的 sources，会如何合并？

可以配置多个 `mode: data` 的 sources。引擎会把这些 sources 的内容项全部加载出来，然后统一作为 modules 注入到 `site.modules`：

- 所有 `mode: data` 的内容项都不会生成路由页面，只影响模板渲染
- modules 按每个内容项的 `type` 分组（来自 front matter / Notion properties），不同 sources 的同名 type 会合并到同一个 `site.modules.<type>[]`
- 多 source 模式下，每个内容项的 `id` 会自动加前缀：`<sourceKey>:<sourceId>`（避免不同 source 的 id 冲突）

示例：3 个 data sources（2 个 markdown data + 1 个 notion data）

```yaml
content:
  sources:
    - type: markdown
      name: modules_marketing
      mode: data
      markdown: { dir: data/marketing, defaultType: module }
    - type: markdown
      name: modules_product
      mode: data
      markdown: { dir: data/product, defaultType: module }
    - type: notion
      name: modules_ops
      mode: data
      notion:
        databaseId: "db_modules_ops"
        filterProperty: Enabled
        filterType: checkbox_true
        fieldPolicy: { mode: all }
```

模板读取方式不变：

```scriban
{{ for b in site.modules.banner }}
  <h2>{{ b.title }}</h2>
{{ end }}
```
