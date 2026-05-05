---
name: bukit-routing
description: Use when customizing URL structures, URLs are not generated as expected, configuring permalink patterns, setting up collection routes, or troubleshooting 404 errors on deployed sites
---

# Bukit URL 路由与永久链接

## Overview

Bukit 通过 **permalink 模式** 和 **集合路由规则** 生成每条内容的 URL 和输出路径。路由优先级：内容元数据覆盖 > 集合配置 > 全局 permalinks > 默认规则（post→`/blog/{slug}/`，page→`/pages/{slug}/`）。

**REQUIRED BACKGROUND:** 路由配置依赖于 site.yaml 中的 `site.collections` 和 `site.permalinks`，必须先理解 bukit-config 中的集合配置模型。
**REQUIRED SUB-SKILL:** 用 `bukit build` 验证路由输出。CLI 命令参考 bukit-cli-reference。

## 路由优先级

```
1. 内容元数据中 route.url + route.outputPath + route.template  ← 最高
2. site.collections 匹配（按 collection 字段或 type 字段匹配）
3. site.permalinks 全局规则
4. 内置默认规则: post → /blog/{slug}/, page → /pages/{slug}/
```

## Permalink 模式

| 占位符 | 替换为 | 示例（slug=hello-world, date=2026-05-05） |
|--------|--------|------------------------------------------|
| `{slug}` | 内容 slug | `hello-world` |
| `{year}` | 发布年份（4位） | `2026` |
| `{month}` | 发布月份（2位） | `05` |
| `{day}` | 发布日期（2位） | `05` |
| `{type}` | 内容类型（post/page/集合名） | `post` |
| `{title}` | 等同于 `{slug}` | `hello-world` |

所有 URL 自动加前后斜杠（`/blog/hello-world/`），输出路径自动后缀 `index.html`（`blog/hello-world/index.html`）。

## 集合路由

每个集合可定义独立的 permalink 和 template：

```yaml
site:
  collections:
    article:
      permalink: /articles/{year}/{month}/{slug}/
      template: pages/post.html
      listRoute: /articles/
      pagination:
        enabled: true
        pageSize: 20
      output:
        rss: true
        sitemap: true
    page:
      permalink: /{slug}/
      template: pages/page.html
```

### 集合匹配规则

- 内容的 `collection` 元数据字段 → 匹配 `site.collections.<key>`
- 若 collection 为空，fallback 到 `type` 字段 → 匹配 `site.collections.<type>`
- 若都没有匹配 → 使用全局 permalinks 或无规则时的内置默认

### 列表路由 (listRoute)

当集合定义了 `listRoute`，会生成列表页（使用集合的 template + `pages` 变量）。必须以 `/` 开头。

## URL 编码策略

`site.outputPathEncoding` 控制输出目录名的编码方式：

| 模式 | 行为 | 适用场景 |
|------|------|---------|
| `none` | 不做处理，保持原样 | 英文 slug，默认 |
| `slug` | 转为小写 ASCII slug | 多语言 slug 转 ASCII |
| `urlencode` | `Uri.EscapeDataString` | 特殊字符 URL 编码 |
| `sanitize` | 移除 Windows 不允许的字符（`<>:"|?*`），空格转 `-` | Windows 开发环境 |

## 路由覆盖

在内容的元数据中设置 `route` 字段可完全自定义路由。Markdown frontmatter：

```yaml
---
route:
  url: /custom/path/
  outputPath: custom/path/index.html
  template: pages/special.html
---

# My Page
```

或分离字段：

```yaml
---
url: /custom/path/
outputPath: custom/path/index.html
template: pages/special.html
---
```

三个字段缺一不可，否则回到集合路由。

## 输出路径规则

- URL `/{slug}/` → 输出路径 `{slug}/index.html`
- URL `/{year}/{month}/{slug}/` → `{year}/{month}/{slug}/index.html`
- 路径分隔符在 Windows 上自动转换为 `\`

## 常见错误

| 错误 | 原因 | 修复 |
|------|------|------|
| 路由冲突（doctor 报错） | 多个内容生成了相同 URL | 检查 slug 唯一性或 permalink 模式 |
| permalink 生成 URL 非预期 | 占位符拼写错误 | 确认使用 `{slug}` 而非 `{Slug}` 或 `{SLUG}` |
| `listRoute must start with '/'` | listRoute 不以 `/` 开头 | 改为 `/articles/` |
| `permalink must include {slug}` | permalink 缺少 {slug} 占位符 | 在模式中加入 `{slug}` |
| URL 中出现中文被 Windows 截断 | 输出路径编码未设置 | 设 `site.outputPathEncoding: slug` 或 `sanitize` |
| 集合匹配到错误的集合 | collection 或 type 字段名不匹配 | 检查内容元数据和 site.collections 键名一致性 |
| 路由覆盖部分字段被忽略 | 三个字段未全部提供 | 确保 route 包含 url、outputPath 和 template |
