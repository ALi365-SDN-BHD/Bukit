---
name: bukit-i18n
description: Use when creating a multilingual Bukit site, language switching does not work, multilingual content is not correctly separated, or encountering sitemap/RSS/search index merging issues
---

# Bukit 多语言站点

## Overview

Bukit 通过**语言检测 → 独立变体构建 → 输出合并**三步实现多语言站点。每种语言独立构建一套完整的静态页面，再在根级别合并 Sitemap、RSS 和搜索索引。

**REQUIRED BACKGROUND:** 多语言配置依赖于 site.yaml 中的 `site.languages`、`site.sitemapMode` 等字段，必须先理解 bukit-config。
**REQUIRED SUB-SKILL:** 用 `bukit build` 构建多语言站点。CLI 命令参考 bukit-cli-reference。

## 配置模型

```yaml
site:
  language: zh-CN              # 单语言时的默认语言
  languages: [zh-CN, en]       # 多语言列表
  defaultLanguage: zh-CN       # 默认语言（未标记语言的内容归属）
  sitemapMode: merged          # merged | split | index
  rssMode: merged              # merged | split
  searchMode: merged           # merged | split | index
```

| 字段 | 说明 |
|------|------|
| `languages` | 需要构建的语言列表，至少 1 个，无重复 |
| `defaultLanguage` | 默认语言，必须在 languages 中。未标 `language` 元数据的内容归入此语言 |
| `sitemapMode` | `merged`=合并 Sitemap（含 hreflang）; `split`=每种语言独立; `index`=生成索引 Sitemap |
| `rssMode` | `merged`=合并 RSS; `split`=每种语言独立 |
| `searchMode` | `merged`=合并搜索索引; `split`=每种语言独立; `index`=生成索引 |

## 内容组织

### 在 Markdown 中标记语言

```markdown
---
title: 关于我们
language: zh-CN
---

# 关于我们
```

```markdown
---
title: About Us
language: en
---

# About Us
```

不含 `language` 元数据的内容自动归入 `defaultLanguage`。

### 在 Notion 中标记语言

在 Notion 数据库中添加 `language` 属性（类型：select），值为 `zh-CN`、`en` 等。无值的页面归入 `defaultLanguage`。

### i18n 关联键

使用 `i18n_key` 元数据将不同语言版本的同一内容关联起来。Sitemap 合并时，相同 `i18n_key` 的页面会生成 `hreflang` 交替链接：

```markdown
---
title: 关于我们
language: zh-CN
i18n_key: about
---

---
title: About Us
language: en
i18n_key: about
---
```

## 构建流程

```
1. 加载内容 → 解析 items
2. 获取语言列表 → languages = [zh-CN, en]
3. 对每种语言:
   a. FilterItemsByLanguage: 筛选该语言的内容
      - 有 language 元数据 → 匹配
      - 无 language 元数据 → 归入 defaultLanguage
   b. baseUrl 组合: / 变为 /zh-CN/ 或 /en/
   c. BuildVariantAsync: 完整构建该语言的静态站点
      → 输出到 dist/zh-CN/ 和 dist/en/
4. 根级别合并:
   - Sitemap: 按 sitemapMode 策略
   - RSS: 按 rssMode 策略
   - 搜索索引: 按 searchMode 策略
```

## 输出结构

```
dist/
  zh-CN/
    index.html
    blog/
      hello-world/
        index.html
    assets/
      style.css
    sitemap.xml
  en/
    index.html
    blog/
      hello-world/
        index.html
    assets/
      style.css
    sitemap.xml
  sitemap.xml       ← merged 模式才生成
  rss.xml           ← merged 模式才生成
  search.json       ← merged 模式才生成
```

## 合并机制

### Sitemap 合并

- `merged`: 在 `dist/sitemap.xml` 生成合并 Sitemap，每对 `i18n_key` 相同的内容自动添加 `<xhtml:link rel="alternate" hreflang="..."/>`
- `split`: 每种语言独立 `dist/<lang>/sitemap.xml`
- `index`: 生成 `dist/sitemap.xml` 作为索引，指向各语言的 sitemap

### RSS 合并

- `merged`: 在 `dist/rss.xml` 生成合并 RSS
- `split`: 每种语言独立 RSS（当前未实现独立输出）

### 搜索索引合并

- `merged`: 生成统一的 `dist/search.json`
- `split`: 每种语言独立 `dist/<lang>/search.json`
- `index`: 生成索引指向各语言索引

## 模板适配

### 语言切换器

```html
<nav>
  {{ if site.language == "zh-CN" }}
    <a href="{{ site.base_url }}/../en/{{ page.url }}">English</a>
  {{ else }}
    <a href="{{ site.base_url }}/../zh-CN/{{ page.url }}">中文</a>
  {{ end }}
</nav>
```

### 条件渲染

```html
{{ if site.language == "zh-CN" }}
  <time>{{ page.publish_date | date.to_string "%Y年%m月%d日" }}</time>
{{ else }}
  <time>{{ page.publish_date | date.to_string "%B %d, %Y" }}</time>
{{ end }}
```

### 根页面语言重定向

单语言时不需处理。多语言时需创建根 `index.html` 做语言检测重定向（手动添加到 `static/` 或通过自定义模板）。

## 常见问题

| 问题 | 原因 | 解决方案 |
|------|------|------|
| 语言切换不生效 | 内容未标记 language 元数据 | 在内容 frontmatter 中添加 `language` |
| 某语言内容为空 | 该语言没有匹配的内容 | 确认有标记该 language 的内容存在 |
| 多语言内容混在同一个页面 | language 元数据值不精确匹配 languages 列表 | 确保元数据值与 site.yaml 中一致（如 `zh-CN` 不是 `zh_CN`） |
| Sitemap hreflang 不出现 | i18n_key 未设置 | 为跨语言对应内容设置相同的 `i18n_key` |
| `defaultLanguage must be included in site.languages` | 配置错误 | 将 defaultLanguage 加入 languages |
| 搜索索引只含一种语言 | searchMode 为 split | 改为 `merged` 或 `index` |
| 合并 RSS 内容重复 | 语言版本共用相同 i18n_key 但内容不同 | 正常行为，RSS 包含所有语言的文章 |
