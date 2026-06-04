# Bukit `site.yaml` 标准配置 Profiles

## 1. 目标

本文件定义 AI 可以直接选用的 `site.yaml` 标准 Profile。

AI 不应自由组合 `site.yaml`，而应从以下 Profile 中选择最合适的配置，然后只替换必要值。

---

# Profile A：Markdown 本地预览模式

## 适用场景

用于 Demo 转 Bukit 后的首次验证。

特点：

```text
生成 notion-seed
同时生成 content/*.md
本地 build 不依赖 Notion
适合用户预览与调试
```

## 推荐命令

```bash
bukit import html-demo ./demo \
  --theme <theme-name> \
  --content-source notion \
  --build-source markdown \
  --route-map demo.routes.yaml \
  --strict warn \
  --force \
  --verify
```

## site.yaml

```yaml
site:
  title: <site-title>
  baseUrl: https://example.com
  language: zh

content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page

build:
  output: dist
  clean: true

theme:
  name: <theme-name>
```

## AI 替换项

```text
<site-title>
<theme-name>
baseUrl
language
```

---

# Profile B：Notion 单数据库模式

## 适用场景

适合小型站点或内容量较少的网站。

特点：

```text
所有内容进入一个 Notion database
配置简单
适合早期 CMS 化
```

## 推荐命令

```bash
bukit import html-demo ./demo \
  --theme <theme-name> \
  --content-source notion \
  --build-source notion \
  --notion-database-id <database-id> \
  --route-map demo.routes.yaml \
  --force
```

## site.yaml

```yaml
site:
  title: <site-title>
  baseUrl: https://example.com
  language: zh

content:
  provider: notion
  notion:
    databaseId: ${NOTION_DATABASE_ID}
    tokenEnv: NOTION_TOKEN
    filterProperty: Published
    filterType: checkbox_true
    sortProperty: Title
    sortDirection: ascending

build:
  output: dist
  clean: true

theme:
  name: <theme-name>
```

---

# Profile C：Notion 多数据库模式

## 适用场景

适合正式 CMS 化网站。

特点：

```text
pages/posts/companies/services 分库管理
更适合长期维护
更适合企业目录、资讯站、服务站
```

## 推荐命令

```bash
bukit import html-demo ./demo \
  --theme <theme-name> \
  --content-source notion \
  --build-source notion \
  --route-map demo.routes.yaml \
  --force
```

或推送 Notion：

```bash
bukit notion push \
  --input sites/<site-name>/notion-seed \
  --database-map sites/<site-name>/notion-seed/notion-database-map.yaml \
  --create-missing-databases \
  --parent-page-id <notion-parent-page-id> \
  --mode upsert \
  --update-content replace
```

## site.yaml

```yaml
site:
  title: <site-title>
  baseUrl: https://example.com
  language: zh

content:
  sources:
    - type: notion
      name: pages
      mode: content
      collection: page
      notion:
        databaseId: ${NOTION_PAGES_DATABASE_ID}
        tokenEnv: NOTION_TOKEN
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: Title
        sortDirection: ascending

    - type: notion
      name: posts
      mode: content
      collection: post
      notion:
        databaseId: ${NOTION_POSTS_DATABASE_ID}
        tokenEnv: NOTION_TOKEN
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: Title
        sortDirection: ascending

    - type: notion
      name: companies
      mode: content
      collection: company
      notion:
        databaseId: ${NOTION_COMPANIES_DATABASE_ID}
        tokenEnv: NOTION_TOKEN
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: Title
        sortDirection: ascending

    - type: notion
      name: services
      mode: content
      collection: service
      notion:
        databaseId: ${NOTION_SERVICES_DATABASE_ID}
        tokenEnv: NOTION_TOKEN
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: Title
        sortDirection: ascending

build:
  output: dist
  clean: true

theme:
  name: <theme-name>
```

---

# Profile D：JSON / YAML seed + Markdown 构建模式

## 适用场景

当用户不使用 Notion，只希望生成 JSON/YAML 数据用于人工处理或未来导入。

## 推荐命令

```bash
bukit import html-demo ./demo \
  --theme <theme-name> \
  --content-source json \
  --build-source markdown \
  --route-map demo.routes.yaml \
  --force
```

## site.yaml

```yaml
site:
  title: <site-title>
  baseUrl: https://example.com
  language: zh

content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page

build:
  output: dist
  clean: true

theme:
  name: <theme-name>
```

---

# Profile 选择规则

| 用户需求 | 应选 Profile |
|---|---|
| 先看效果、本地预览 | Profile A |
| 小站点、单 Notion database | Profile B |
| 正式 CMS、多内容集合 | Profile C |
| 不使用 Notion，只导出数据 | Profile D |

---

# AI 输出 site.yaml 前必须声明

AI 在输出 `site.yaml` 前必须说明：

```text
选择的 Profile：
原因：
content-source：
build-source：
是否使用 Notion：
是否使用多数据库：
预计生成的配置类型：
```

示例：

```text
选择 Profile A：Markdown 本地预览模式。
原因：当前仍处于 Demo 确认后的本地验证阶段，不应依赖 Notion token。
content-source: notion
build-source: markdown
生成 site.yaml: content.provider = markdown
```
