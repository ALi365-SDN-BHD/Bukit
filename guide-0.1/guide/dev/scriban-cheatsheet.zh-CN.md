# Scriban 模板速查（Bukit 主题开发）

## 基础语法

代码块：`{{ expression }}`，多行语句用 `{{ ... end }}`。

```scriban
{{ site.title }}
{{ "hello" | string.upcase }}
```

赋值：`{{ x = "value" }}`

## 条件判断

```scriban
{{ if page.summary }}
  <p>{{ page.summary }}</p>
{{ else if page.content }}
  <p>{{ page.content | string.truncate 200 }}</p>
{{ else }}
  <p>无内容</p>
{{ end }}
```

Truthy/Falsy：`null`、`false`、`0`、空字符串 `""` 为 falsy。

空值合并：`{{ page.summary ?? "默认摘要" }}`

## 循环

```scriban
{{ for item in pages }}
  <a href="{{ item.url }}">{{ item.title }}</a>
  {{ if !for.last }}<hr>{{ end }}
{{ end }}
```

循环变量：`for.index`（0 起）、`for.first`、`for.last`、`for.even`、`for.odd`

参数：`{{ for item in pages limit:10 offset:2 }}`

## Layout 与 Include

layout 指令必须在模板第一行（非空行）：

```scriban
{{ layout "layouts/base.html" }}
<h1>{{ page.title }}</h1>
{{ page.content }}
```

base.html 中用 `{{ content }}` 接收子模板输出：

```html
<!doctype html>
<html lang="{{ site.language }}">
<head>
  <title>{{ page.title }} - {{ site.title }}</title>
  <link rel="stylesheet" href="{{ site.base_url }}/assets/style.css" />
</head>
<body>
  {{ include "partials/header.html" }}
  <main>{{ content }}</main>
  {{ include "partials/footer.html" }}
</body>
</html>
```

include 路径相对于 layouts 目录。

## Bukit 模板变量

### site 对象

| 变量 | 说明 |
|---|---|
| `site.name` | 站点名称 |
| `site.title` | 站点标题 |
| `site.url` | 站点绝对 URL（可为空） |
| `site.description` | 站点描述（可为空） |
| `site.base_url` | baseUrl（`/` 时为空字符串） |
| `site.language` | 当前语言 |
| `site.params` | theme.params 注入的参数 |
| `site.modules` | data 模块分组 |

### page 对象

| 变量 | 说明 |
|---|---|
| `page.title` | 页面标题 |
| `page.url` | 页面 URL |
| `page.content` | HTML 正文 |
| `page.summary` | 摘要 |
| `page.publish_date` | 发布时间（DateTime，可为空） |
| `page.fields` | 自定义字段，访问方式 `page.fields.<key>.type/value` |

### pages 数组（列表页专用）

在 index.html 和 list.html 中可用，每项结构同 page。

```scriban
{{ for post in pages }}
  <article>
    <h2><a href="{{ post.url }}">{{ post.title }}</a></h2>
    <time>{{ post.publish_date | date.to_string "%Y-%m-%d" }}</time>
    {{ if post.summary }}<p>{{ post.summary }}</p>{{ end }}
  </article>
{{ end }}
```

### site.modules（数据模块）

```scriban
{{ if site.modules && site.modules.navigation }}
  {{ for item in site.modules.navigation }}
    <a href="{{ item.fields.link.value }}">{{ item.title }}</a>
  {{ end }}
{{ end }}
```

### site.data（插件注入数据）

由插件（如 pages-index）在构建时注入的全局数据，可在所有模板中使用。

| 变量 | 来源 | 说明 |
|---|---|---|
| `site.data.pages_by_id` | pages-index 插件 | 所有页面按 ID 索引的字典，可用于交叉引用 |

```scriban
{{ if site.data && site.data.pages_by_id }}
  {{ related = site.data.pages_by_id[related_id] }}
  {{ if related }}
    <a href="{{ related.url }}">{{ related.title }}</a>
  {{ end }}
{{ end }}
```

注意：`site.data` 仅在插件向 PluginContext.Data 写入数据时才存在，非内置插件启用时此变量为空。

## 常用内置函数

### 字符串

- `string.upcase` / `string.downcase` -- 大小写
- `string.truncate 200` -- 截断
- `string.strip` -- 去空白
- `string.replace "a" "b"` -- 替换
- `string.contains "keyword"` -- 包含判断
- `string.split ","` -- 分割为数组
- `string.starts_with "http"` -- 前缀判断

### 日期

- `date.to_string "%Y-%m-%d"` -- 格式化
- `date.now` -- 当前时间
- `date.add_days 7` -- 日期加减

### 数组

- `array.size` -- 数组长度
- `array.first` / `array.last` -- 首尾元素
- `array.sort_by "publish_date"` -- 按字段排序
- `array.reverse` -- 反转
- `array.map "title"` -- 提取字段

### 管道操作

```scriban
{{ pages | array.sort_by "publish_date" | array.reverse | array.first }}
{{ page.title | string.truncate 50 }}
```

## 必备模板清单

| 模板路径 | 用途 | 可用变量 |
|---------|------|---------|
| `pages/index.html` | 站点首页 | site, pages |
| `pages/list.html` | blog/pages 聚合页 | site, pages |
| `pages/post.html` | type=post 内容页 | site, page |
| `pages/page.html` | type=page 内容页 | site, page |
| `layouts/base.html` | 布局模板 | site, page/pages, content |

## 资源路径

模板中引用资源必须拼接 `site.base_url`（兼容 GitHub Pages 子路径部署）：

```html
<link rel="stylesheet" href="{{ site.base_url }}/assets/style.css" />
<script src="{{ site.base_url }}/assets/script.js"></script>
<img src="{{ site.base_url }}/assets/logo.png" />
```
