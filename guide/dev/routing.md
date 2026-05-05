# 路由系统（collections 主路径与兼容规则）

路由系统负责把 `ContentItem` 映射为 `RouteInfo(url, outputPath, template)`，供渲染阶段使用。

实现参考：`src/Bukit.Routing/RouteGenerator.cs`

## Collection 驱动路由（主模型）

路由优先由 `site.collections` 决定，集合键通常来自 `meta.collection`（缺失时回退 `meta.type`）：

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
      listRoute: /pages/
```

每个集合最少需要：

- `permalink`（必须包含 `{slug}`）
- `template`

## Permalink 模式（兼容层）

`site.permalinks` 仍可作为兼容输入，但推荐迁移到 `site.collections`。

```yaml
site:
  permalinks:
    post: "/{year}/{month}/{slug}/"
    page: "/docs/{slug}/"
```

支持的占位符：

| 占位符 | 来源 | 示例 |
|---|---|---|
| `{slug}` | ContentItem.Slug | `my-post` |
| `{title}` | ContentItem.Slug（回退） | `my-post` |
| `{year}` | ContentItem.PublishAt 年（4 位） | `2025` |
| `{month}` | ContentItem.PublishAt 月（2 位） | `03` |
| `{day}` | ContentItem.PublishAt 日（2 位） | `15` |
| `{type}` | meta.type | `post` |

示例效果：

| 配置 | slug=`my-post`, 发布=2025-03-15 | 生成 URL |
|---|---|---|
| `/{year}/{month}/{slug}/` | post 类型 | `/2025/03/my-post/` |
| `/{year}/{month}/{day}/{slug}/` | post 类型 | `/2025/03/15/my-post/` |
| `/docs/{slug}/` | page 类型 | `/docs/my-post/` |

优先级（从高到低）：
1. 路由覆盖（Route Override）—— meta 中显式指定 url/outputPath/template
2. Collection 规则 —— `site.collections`
3. Permalink 模式 —— `site.permalinks`（兼容层）
4. 默认路由规则（兼容层）

实现参考：`RouteGenerator.ExpandPermalinkPattern` / `RouteGenerator.BuildFromPermalink`

## 路由覆盖（Route Override）

当 ContentItem 的 Meta 中存在以下字段时，会覆盖默认路由：

1. `route` 映射对象：

```yaml
route:
  url: /custom/
  outputPath: custom/index.html
  template: pages/page.html
```

2. 或者同级扁平字段：

```yaml
url: /custom/
outputPath: custom/index.html
template: pages/page.html
```

覆盖生效条件：
- `url`、`outputPath`、`template` 三者都非空才会生效（缺一则回退默认路由）

## Notion 内容如何覆盖路由

Notion 内容通过数据库属性映射到 `fields`，引擎会把以下字段提升到 `meta` 以支持路由覆盖：

- `url`（文本）
- `outputPath`（文本）
- `template`（文本）

填写示例：

```
url: /asdfasdf/
outputPath: asdfasdf/index.html
template: pages/page.html
```

注意：Notion 属性名会被标准化（忽略大小写、空格、符号），例如 `Output Path` 会识别为 `outputpath`。
补充：Notion 的 `formula` 字段也会被解析为文本/数值/布尔/日期，可用于路由覆盖。

## outputPath 编码策略（处理中文与符号）

当 `outputPath` 含中文或符号时，可在 `site.yaml` 使用：

```yaml
site:
  outputPathEncoding: none|slug|urlencode|sanitize
```

策略说明：
- `none`：不做任何编码（默认）
- `slug`：对每个路径段做 slugify（中文会被转成空，最终回退为 `page`）
- `urlencode`：对每个路径段做 URL 编码（保留中文语义但会变成 `%E4%...`）
- `sanitize`：空格替换为 `-`，移除 `<>:"|?*` 和控制字符，连续 `-` 压缩，段末 `.`/空格移除

建议：如果希望稳定跨平台，优先用 `slug`；如果希望保留中文可读性，用 `urlencode`；如果希望保留中文且只处理危险字符，用 `sanitize`。

## 归一化规则（Normalization）

覆盖字段会被归一化：

- url：
  - 自动补齐前导 `/`
  - 自动补齐尾随 `/`
  - 例如：`custom` → `/custom/`
- outputPath：
  - 去掉前导 `/` 或 `\\`
  - 统一为 `/` 分隔
  - 例如：`/a\\b\\index.html` → `a/b/index.html`

## 维护建议

- 路由覆盖是稳定契约：内容生产侧（Markdown/Notion/AI intent）可能依赖它，修改规则需考虑兼容性
- 建议在文档/主题中约定少量“公共路由覆盖模式”，避免每页随意定制导致站点结构不可预期

引擎还会生成一些不依赖内容的固定聚合页（`/`、`/blog/`、`/pages/`），见 [引擎固定产物](./engine-outputs.md)。
