# PageComposer 使用指南

`PageComposer` 是 `src/Bukit.Theme/PageComposer.cs` 中的一个静态工具类，负责解析页面的 section JSON 并与主题的 section 定义合并（compose）。

## 核心职责

1. **解析**：将 `page.fields.sections` 中的 JSON 字符串反序列化为 `List<PageSectionDefinition>`
2. **合并**：将页面声明的 section 与 `theme.yaml` 中定义的 `ThemeSectionDefinition` 合并，页面值覆盖主题默认值
3. **数据绑定合并**：将页面级别的 `source`/`filter`/`limit`/`sort` 与主题的 `data` 绑定合并

## 重要说明

`PageComposer` 类已接入渲染管线。当 `page.fields.sections` 包含 JSON 字符串时，模板中可直接使用 `{{ render_section page.fields.sections.value }}` 渲染整个 sections 数组。`render_section` 会自动调用 `PageComposer.ParseSections()` → `Compose()` 完成 JSON 解析和主题默认值合并。

如需在构建阶段预合并 section 数据（例如做 SEO 分析），可以在自定义插件中调用 `PageComposer.Compose()`。

## JSON 格式（page.fields.sections）

页面内容中通过 `fields.sections` 字段声明页面使用了哪些 section：

```json
[
  {
    "type": "hero",
    "variant": "centered",
    "props": {
      "headline": "Welcome to My Site",
      "subheadline": "Built with Bukit",
      "ctaText": "Get Started",
      "ctaUrl": "/about"
    }
  },
  {
    "type": "cardGrid",
    "source": "posts",
    "filter": {
      "featured": true
    },
    "limit": 6,
    "sort": "-publish_date"
  }
]
```

### PageSectionDefinition 字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `type` | string | section 类型，对应 theme.yaml 中 sections 的 key |
| `variant` | string? | 变体名称，对应 section 的 variants 中的 key |
| `props` | dict? | 传递给 section 模板的属性 |
| `source` | string? | 数据源名称（如 posts/pages） |
| `filter` | dict? | 数据过滤条件 |
| `limit` | int? | 数据条数限制 |
| `sort` | string? | 排序规则（如 `-publish_date` 为降序） |

## 合并规则

`PageComposer.Compose()` 的合并逻辑：

1. 遍历页面声明的每个 section
2. 在 `theme.yaml` 的 `sections` 中查找同名定义
3. 如果找不到对应主题定义，原样保留页面 section
4. 如果找到，合并 props：页面 props 覆盖同名属性
5. 合并 data binding：页面级 `source`/`filter`/`limit`/`sort` 优先于主题默认值

## 数据绑定

当 section 声明中包含 `source` 字段时，`render_section` 会自动调用 `SectionDataResolver.Resolve()` 填充 `section.items`。数据绑定管线已端到端连接，无需手动在模板中实现过滤逻辑。

**数据绑定字段**（section JSON 或 theme.yaml `data` 中均可用）：

| 字段 | 类型 | 说明 |
|------|------|------|
| `source` | string | 数据源（`"posts"`、`"type:post"`、`"collection:blog"`、`"*"`） |
| `filter` | dict | 过滤条件（如 `{"featured": true}`） |
| `limit` | int | 最大条目数 |
| `sort` | string | 排序（如 `"publishAt desc"`、`"title"`） |

**示例**——section 自动解析 "posts" 并注入 `section.items`：

```json
{ "type": "cardGrid", "source": "posts", "limit": 6, "sort": "publishAt desc" }
```

Section 模板：
```scriban
{{ for item in items }}
  {{ render_component "insightCard" item }}
{{ end }}
```

Section 的数据绑定支持两级声明：

**主题级默认（theme.yaml）**：

```yaml
sections:
  cardGrid:
    data:
      source: posts
      limit: 6
      sort: "-publish_date"
```

**页面级覆盖（page.fields.sections JSON）**：

```json
{
  "type": "cardGrid",
  "source": "featured_posts",
  "limit": 3
}
```

合并结果会优先使用页面级的值，缺少的字段回退到主题默认值。

## 示例：首页 hero + cardGrid

**theme.yaml**：

```yaml
sections:
  hero:
    template: layouts/sections/hero/hero.html
    schema: sections/hero/schema.json
  cardGrid:
    template: layouts/sections/card-grid/card-grid.html
    schema: sections/card-grid/schema.json
    data:
      source: posts
      limit: 6
```

**page.fields.sections JSON**：

```json
[
  {
    "type": "hero",
    "props": {
      "headline": "Latest Insights",
      "subheadline": "Thoughts on technology and design"
    }
  },
  {
    "type": "cardGrid",
    "limit": 3
  }
]
```

**页面模板中使用 render_section**：

```scriban
{{ layout "layouts/base.html" }}

{{ for section in page.fields.sections }}
  {{ render_section section }}
{{ end }}
```

`render_section` 是 Scriban 渲染器注入的全局函数，它会：
1. 根据 `section.type` 查找 section 模板
2. 将 `section.props` 注入为 `{{ props.xxx }}`
3. 将 `section.items`（如已绑定数据）注入为 `{{ items }}`
4. 执行 schema 校验（取决于 `componentValidation` 配置）

### 手动循环渲染 section

也可以手动遍历 section 而不使用 `render_section`：

```scriban
{{ for section in page.fields.sections }}
  {{ if section.type == "hero" }}
    {{ render_section section }}
  {{ else if section.type == "cardGrid" }}
    <section class="card-grid">
      <h2>Latest Posts</h2>
      {{ for item in section.items }}
        {{ comp.render "insightCard" item }}
      {{ end }}
    </section>
  {{ end }}
{{ end }}
```

## 完整渲染管线

从 JSON 到 HTML 的完整数据流：

```
page.fields.sections (JSON 字符串)
  → render_section 函数调用
    → PageComposer.ParseSections()      解析 JSON → List<PageSectionDefinition>
    → PageComposer.Compose()             合并 theme.yaml 默认值 (props + data)
    → SectionDataResolver.Resolve()      自动查询 source/filter/limit/sort
    → SectionSchemaValidator.Validate()  校验 props (warn/strict/off)
    → Section Plugin BeforeRender hook   (可选) 插件修改 props
    → Scriban 模板渲染
    → Section Plugin AfterRender hook    (可选) 插件后处理 HTML
    → 输出 HTML
```

### 相关文档

- [Section 插件系统](section-plugin.zh-CN.md) — ISectionPlugin 接口及 BeforeRender/AfterRender hook
- [Section Schema 参考](section-schema.zh-CN.md) — schema.json 格式及校验模式
- [组件工具函数](component-utilities.zh-CN.md) — util.format_date / truncate / titleize / slugify
