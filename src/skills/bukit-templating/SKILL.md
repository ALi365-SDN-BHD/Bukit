---
name: bukit-templating
description: Use when writing or modifying Scriban templates, encountering template rendering errors, needing to access page/site/data in templates, using layout inheritance, or working with list pages, pagination, or multi-language conditional rendering
---

# Bukit Scriban 模板开发

## Overview

Bukit 使用 [Scriban](https://github.com/scriban/scriban) 模板引擎，支持 `{% layout "path" %}` 继承、`{{ include "path" }}` 局部模板、完整的变量和数据访问。模板文件位于 `themes/<name>/layouts/` 目录下。**本技能只讲 Scriban 语法和模板编写，目录结构和静态资源请参考 `bukit-theme`。**

## 数据模型

模板中可用的三大数据对象：

### `site` — 站点全局信息

| 变量 | 类型 | 说明 |
|------|------|------|
| `site.name` | string | 站点名 |
| `site.title` | string | 站点标题 |
| `site.url` | string/null | 站点完整 URL |
| `site.description` | string/null | 站点描述 |
| `site.base_url` | string | 根路径。为 `/` 时为空字符串，否则为 `/subpath/` 格式 |
| `site.language` | string | 当前语言 |
| `site.params` | object | 主题参数 `theme.params` 的映射 |
| `site.modules` | object | 数据模块（`mode: data` 的内容） |
| `site.data` | object | 通过 `sources[].mode: data` 或数据模块构建的内容数据 |

### `page` — 当前页面信息

| 变量 | 类型 | 说明 |
|------|------|------|
| `page.title` | string | 页面标题 |
| `page.url` | string | 页面 URL（相对路径，base_url 不包含此值） |
| `page.content` | string | 页面 HTML 内容 |
| `page.summary` | string/null | 页面摘要 |
| `page.publish_date` | DateTime/null | 发布日期 |
| `page.fields` | object | 元数据字段，如 `page.fields.tags`、`page.fields.author` |

每个 field 是 `{type: string, value: ...}` 结构：
```html
{{ page.fields.tags.value }}         ← 直接值
{{ for tag in page.fields.tags.value }}  ← 如果是数组
```

### `pages` — 页面列表（仅列表页）

仅在 index.html 和 list.html 模板中可用，是 `PageInfo` 对象数组。每个元素有 `title`、`url`、`content`、`summary`、`publish_date`、`fields`。

## Layout 继承

Bukit 支持自定义 `{% layout %}` 指令（第一行非空行）：

```html
{% layout "layouts/base.html" %}

<article>
  <h1>{{ page.title }}</h1>
  <div>{{ page.content }}</div>
</article>
```

- `{% layout %}` 必须是**第一行非空行**
- 布局模板中 `{{ content }}` 会被替换为子模板的 body
- 支持嵌套继承（子模板继承父布局，父布局再继承祖布局）
- 路径相对于 `layouts/` 目录
- 支持单引号和双引号：`{% layout 'layouts/base.html' %}`
- `{{ layout "..." }}` 语法同效

### base.html 典型写法

```html
<!DOCTYPE html>
<html lang="{{ site.language }}">
<head>
  <meta charset="utf-8" />
  <title>{{ page.title }} - {{ site.title }}</title>
  <link href="{{ site.base_url }}/assets/style.css" rel="stylesheet">
</head>
<body>
  {{ include "partials/header.html" }}
  <main>
    {{ content }}         ← 子模板内容注入此处
  </main>
  {{ include "partials/footer.html" }}
</body>
</html>
```

## 常用模式

### 单页模板 (pages/page.html)

```html
{% layout "layouts/base.html" %}

<article>
  <h1>{{ page.title }}</h1>
  <div class="content">
    {{ page.content }}
  </div>
</article>
```

### 文章模板 (pages/post.html)

```html
{% layout "layouts/base.html" %}

<article>
  <h1>{{ page.title }}</h1>
  {{ if page.publish_date }}
    <time>{{ page.publish_date | date.to_string "%Y-%m-%d" }}</time>
  {{ end }}
  <div class="content">{{ page.content }}</div>
</article>
```

### 首页模板 (pages/index.html)

```html
{% layout "layouts/base.html" %}

<h1>{{ site.title }}</h1>

{{ for p in pages }}
  <article>
    <h2><a href="{{ site.base_url }}{{ p.url }}">{{ p.title }}</a></h2>
    {{ if p.publish_date }}
      <small>{{ p.publish_date | date.to_string "%Y-%m-%d" }}</small>
    {{ end }}
    {{ if p.summary }}
      <p>{{ p.summary }}</p>
    {{ end }}
  </article>
{{ end }}
```

`pages` 数组已按发布时间倒序排列。

### 列表页模板 (pages/list.html)

```html
{% layout "layouts/base.html" %}

<ul>
{{ for p in pages }}
  <li>
    <a href="{{ site.base_url }}{{ p.url }}">{{ p.title }}</a>
  </li>
{{ end }}
</ul>
```

### 分页处理

分类页、列表页启用分页后，`pages` 仅包含当前页的条目。分页信息通过页面元数据传递，在模板中按需取用。

### 访问自定义字段

```html
<!-- 单值字段 -->
{{ page.fields.author.value }}

<!-- 多选/数组 -->
{{ for tag in page.fields.tags.value }}
  <span class="tag">{{ tag }}</span>
{{ end }}

<!-- 嵌套对象字段 -->
{{ page.fields.seo.value.title }}
```

### 条件渲染

```html
{{ if page.fields.cover.value }}
  <img src="{{ page.fields.cover.value }}" alt="{{ page.title }}">
{{ else }}
  <img src="{{ site.base_url }}/assets/default-cover.jpg">
{{ end }}

{{ if page.publish_date > date.parse "2024-01-01" }}
  <span class="badge">新</span>
{{ end }}
```

### Include 局部模板

```html
{{ include "partials/header.html" }}
{{ include "partials/card.html" }}
```

### 多语言条件渲染

```html
{{ if site.language == "en" }}
  <a href="/en/about/">About</a>
{{ else }}
  <a href="/zh-CN/about/">关于</a>
{{ end }}
```

## 内置函数

Bukit 复用 Scriban 的内置函数，包括：

| 类别 | 函数 |
|------|------|
| 日期 | `date.now`, `date.parse`, `date.to_string` |
| 字符串 | `string.downcase`, `string.upcase`, `string.slice` |
| 数组 | `array.size`, `array.limit`, `array.offset` |
| 数学 | `math.round`, `math.ceil`, `math.floor` |
| 类型转换 | `to_string`, `to_int` |

Bukit 的 Scriban 上下文启用了 `EnableRelaxedMemberAccess`、`EnableRelaxedTargetAccess`、`EnableNullIndexer`，访问不存在的属性返回 null 而不抛错。

## 模板文件布局约定

```
layouts/
  layouts/      ← 布局模板（base.html, 也可自定义更多）
  pages/        ← 页面模板（page.html, post.html, index.html, list.html）
  partials/     ← 局部模板（header.html, footer.html, ...）
```

模板路径在 site.yaml 集合配置中引用时不带 `layouts/` 前缀。例如 `template: pages/post.html` 解析为 `layouts/pages/post.html`。

## 常见错误

| 错误现象 | 原因 | 修复 |
|---------|------|------|
| `Template not found: xxx` | 模板路径错误 | 检查 site.yaml 中 template 和 site.collections 模板路径 |
| `Template parse error` | Scriban 语法错误 | 检查 `{{` `}}` 匹配、表达式语法 |
| `Render failed` | 渲染时变量访问出错 | 使用 `{{ if xxx }}{{ end }}` 先检查变量存在性 |
| layout 不生效 | `{% layout %}` 不是第一行非空行 | 确保第一行（不含空白行）就是 `{% layout %}` |
| `page.content` 为空 | 内容未渲染或 body key 不匹配 | 检查内容源配置 |
| `site.data` 为空 | 数据模块未正确配置 | 确认 `sources[].mode: data`，检查 `bukit doctor` |
| `pages` 在非列表页模板中不可用 | `pages` 仅传递给 list/index 模板 | 单页模板用 `page` |
| 变量输出 HTML 转义 | Scriban 默认转义 | 用 `{{ variable | html.raw }}` |
| 中文字符乱码 | 模板文件编码问题 | 确保模板文件为 UTF-8（无 BOM） |
| base_url 路径拼接多余斜杠 | `base_url` 以 `/` 结尾时 URL 出现 `//` | `site.base_url` 为 `/` 时值为空字符串，可直接 `{{ site.base_url }}/xxx` |
