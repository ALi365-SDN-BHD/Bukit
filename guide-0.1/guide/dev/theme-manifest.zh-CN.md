# theme.yaml 字段参考

> **语言说明**：本页目前仅有中文版本。English version pending. Versi Bahasa Melayu belum tersedia.

`theme.yaml` 是组件化主题的入口清单文件，由 `ThemeManifestLoader.Load()` 加载，反序列化为 `ThemeManifestV2`。

实现参考：
- `src/Bukit.Theme/Models/ThemeManifestV2.cs`
- `src/Bukit.Theme/ThemeManifestLoader.cs`

## 完整字段列表

### 基础元数据

| 字段 | 类型 | 必需 | 说明 |
|---|---|---|---|
| `name` | string | 是 | 主题唯一标识，用于 `theme.name` 和 `extends` |
| `display_name` | string | 否 | 人类可读的显示名称 |
| `version` | string | 否 | 语义化版本号，推荐格式 `1.0.0` |
| `engine` | string | 否 | 目标引擎，当前应设为 `bukit` |
| `min_engine_version` | string | 否 | 最低引擎版本要求，如 `0.3.0` |
| `description` | string | 否 | 主题简要描述 |
| `extends` | string | 否 | 父主题名称，用于主题继承 |
| `tokens` | string | 否 | tokens.yaml 路径，默认 `tokens.yaml` |

### capabilities（能力声明）

```yaml
capabilities:
  i18n: false
  seo: true
  geo: false
  dark_mode: false
  search: false
  taxonomy: false
```

全部为 `bool` 类型，影响引擎的渲染行为（如 i18n 决定是否生成多语言页面）。

### layouts（布局模板）

```yaml
layouts:
  default: layouts/base.html
  landing: layouts/landing.html
  clean: layouts/clean.html
```

`default` 是默认布局，当页面模板未显式指定 layout 时回退到 `default`。

### page_templates（页面模板）

```yaml
page_templates:
  home:
    template: pages/home.html
    label: Home Page
    accepts:
      type: page
  post:
    template: pages/post.html
    label: Blog Post
    accepts:
      type: post
      collection: blog
    required_fields:
      - featured_image
```

| 子字段 | 说明 |
|---|---|
| `template` | 模板文件路径（相对于 layouts 目录） |
| `label` | 人类可读标签 |
| `accepts.type` | 内容类型过滤（page/post/custom） |
| `accepts.collection` | 集合过滤 |
| `required_fields` | 必需的前置字段列表 |

### sections（区块定义）

```yaml
sections:
  hero:
    template: layouts/sections/hero/hero.html
    schema: sections/hero/schema.json
    preview: sections/hero/preview.png
    description: Main hero section with headline and CTA
    data:
      source: posts
      limit: 5
      sort: "-publish_date"
    variants:
      centered:
        template: layouts/sections/hero/hero-centered.html
        label: Centered Hero
      split:
        template: layouts/sections/hero/hero-split.html
        label: Split Layout Hero
```

| 子字段 | 说明 |
|---|---|
| `template` | section 默认模板路径 |
| `schema` | JSON schema 文件路径（用于 prop 校验） |
| `preview` | 预览图路径 |
| `description` | 描述文本 |
| `data` | 数据绑定默认值（source/filters/limit/sort/mode） |
| `variants` | 变体定义，key 为变体名 |

### components（组件定义）

```yaml
components:
  insightCard:
    template: layouts/components/cards/insight-card.html
    props:
      title: string
      summary: string
      url: string
      image: string
```

| 子字段 | 说明 |
|---|---|
| `template` | 组件模板路径 |
| `props` | 属性名到类型的映射（用于 Scriban 渲染时注入变量） |

### assets（资源声明）

```yaml
assets:
  css:
    - assets/css/main.css
    - assets/css/extra.css
  js:
    - assets/js/main.js
```

声明主题需要额外加载的 CSS/JS 文件，doctor 命令会检查这些文件是否存在。

## 完整示例 theme.yaml

```yaml
name: my-blog-theme
display_name: My Blog Theme
version: 2.0.0
engine: bukit
min_engine_version: 0.3.0
description: A clean, fast blog theme with hero and card grid sections.
extends: starter

capabilities:
  i18n: false
  seo: true
  geo: false
  dark_mode: true
  search: true
  taxonomy: true

layouts:
  default: layouts/base.html
  landing: layouts/landing.html

page_templates:
  home:
    template: pages/home.html
    label: Home Page
    accepts:
      type: page
  post:
    template: pages/post.html
    label: Blog Post
    accepts:
      type: post

sections:
  hero:
    template: layouts/sections/hero/hero.html
    schema: sections/hero/schema.json
    preview: sections/hero/preview.png
    description: Main hero section
    variants:
      minimal:
        template: layouts/sections/hero/hero-minimal.html
        label: Minimal Hero
  cardGrid:
    template: layouts/sections/card-grid/card-grid.html
    schema: sections/card-grid/schema.json
    description: Card grid for latest posts
    data:
      source: posts
      limit: 6
      sort: "-publish_date"

components:
  insightCard:
    template: layouts/components/cards/insight-card.html
    props:
      title: string
      summary: string
      url: string
      date: string

assets:
  css:
    - assets/css/main.css
  js:
    - assets/js/main.js

tokens: tokens.yaml
```
