# 路由系统（Collections 主路径与兼容规则）

路由系统负责把 `ContentItem` 映射为 `RouteInfo(url, outputPath, template)`，供渲染阶段使用。

实现参考：`src/Bukit.Routing/RouteGenerator.cs`、`src/Bukit.Routing/RoutePathBuilder.cs`、`src/Bukit.Engine/RouteInventoryValidator.cs`

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

## Permalink 模式

`site.permalinks` 可作为显式路由输入；新项目推荐使用 `site.collections`。

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
1. 全量路由覆盖（url + outputPath + template）← 最高
2. 部分路由覆盖（仅 url 或 url + template）
3. Collection 规则 — `site.collections`
4. Permalink 模式 — `site.permalinks`
5. 无匹配规则时抛出配置错误，提示补充 collection、route 或模板配置

实现参考：`RouteGenerator.ExpandPermalinkPattern` / `RouteGenerator.BuildFromPermalink`

## 路由覆盖（Route Override）

### 全量覆盖

当 ContentItem 的 Meta 中同时存在 `url`、`outputPath`、`template` 三个字段时，会完全覆盖默认路由：

```yaml
route:
  url: /custom/
  outputPath: custom/index.html
  template: pages/page.html
```

或者同级扁平字段：`url:`、`outputPath:`、`template:`。

### 部分覆盖（仅 url）

当仅提供 `url` 时，Bukit 会进入部分覆盖模式：
- `outputPath`：从 URL 自动推导（通过 `RoutePathBuilder.BuildOutputPathFromUrl`）
- `template`：沿用 collection/permalinks/default 规则

```yaml
url: /my-slug/
# outputPath → my-slug/index.html（自动推导）
# template   → 沿用 collection 规则
```

规则：
- `url` 必须提供（自动补齐前后斜杠）
- `outputPath` 自动推导，手动提供的值会被忽略
- `template` 可选，省略时继承 collection/permalinks/default
- 仅提供 `outputPath` 的半覆盖**不支持**

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

此设置对内容页和派生页（分页、归档、分类页）均生效。

建议：如果希望稳定跨平台，优先用 `slug`；如果希望保留中文可读性，用 `urlencode`；如果希望保留中文且只处理危险字符，用 `sanitize`。

## RoutePathBuilder 公共工具

所有路由逻辑共享 `RoutePathBuilder`（`src/Bukit.Routing/RoutePathBuilder.cs`）：

| 方法 | 用途 |
|--------|---------|
| `NormalizeUrl(url)` | 确保前后斜杠 |
| `NormalizeListRoute(url)` | 列表路由规范化（默认 `/`） |
| `BuildOutputPathFromUrl(url, encoding)` | URL → 输出路径（含 `index.html`） |
| `NormalizeOutputPath(path, encoding)` | 对路径段应用编码策略 |

使用者：`RouteGenerator`、`PaginationPlugin`、`ArchivePlugin`、`TaxonomyPlugin`、`PageRenderDispatcher`、`SeoAlternatesService.BuildListRoutesCore`、`I18nOutputMerger`。

## 路由冲突检测

`RouteInventoryValidator`（`src/Bukit.Engine/RouteInventoryValidator.cs`）在两个阶段校验路由唯一性：

1. **内容路由生成后** — `ValidateContentRoutes` 检查内容页之间的 URL/outputPath 冲突。重复时抛出 `ConfigException`。
2. **渲染前最终校验** — `ValidateFinalRoutes` 检查完整清单（内容 + 派生 + 列表路由）。

`bukit doctor` 也会运行内容路由校验，无需完整 build 即可提前发现冲突。

### 派生页冲突

由 `site.deriveConflictPolicy` 控制：`fail`（默认，抛出 `InvalidOperationException`）、`warn`（跳过 + 记录告警）、`last-wins`（接受派生页覆盖）。检测分两步：`PluginRunner.ApplyDeriveConflictPolicy`（逐插件检测）→ `ValidateFinalRoutes`（最终清单校验）。

内容页之间的冲突始终报错 — `deriveConflictPolicy` 不影响内容页之间的冲突。

## 标准化规则（Normalization）

覆盖字段会被标准化：

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
- 建议在文档/主题中约定少量"公共路由覆盖模式"，避免每页随意定制导致站点结构不可预期

引擎还会生成一些不依赖内容的固定聚合页（`/`、`/blog/`、`/pages/`），见[引擎固定产物](./engine-outputs.zh-CN.md)。
