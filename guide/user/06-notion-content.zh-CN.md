# 06 内容（Notion）：把 Notion 当 CMS 的完整配置与示例

如果你希望“写作与编辑”发生在 Notion，而不是仓库里，那么 Notion 模式可以让你把 Notion 数据库当作 CMS，构建时自动拉取并渲染。

本页会讲清楚：需要哪些 Notion 字段、如何过滤/排序、如何把自定义字段传到模板、以及常见的 token/权限问题。

深入的字段归一化规则与开发者契约见：[guide/dev/content](../dev/content.md) 与 `dosc/notion_schema.md`。

## 你将获得什么

- Notion 数据库推荐字段（可直接照着建）
- 一份可复制的 `site.yaml`（Notion 模式）
- 一张“模拟数据库数据表”（方便理解每列的含义）
- 常见报错与修复（token、databaseId、字段类型不匹配）

## 前置条件与安全要求

### 1）必须设置环境变量 NOTION_TOKEN

Notion token **只能通过环境变量注入**，不能写进 `site.yaml`（也不应写进仓库任何文件）。

Windows PowerShell（当前会话）示例：

```powershell
$env:NOTION_TOKEN="secret_xxx"
```

GitHub Actions 里建议用仓库 Secrets（见：[13-部署-GitHub-Pages](./13-部署-GitHub-Pages.md)）。

### 2）Notion 集成（Integration）需要访问你的数据库

你需要在 Notion 创建一个 Integration，并把目标数据库分享给该 Integration，否则会出现“无权限/找不到数据库”等错误。

## 最小配置（Notion provider）

```yaml
content:
  provider: notion
  notion:
    databaseId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
```

建议你从“最小配置”开始跑通，再逐步增加 filter/sort/fieldPolicy。

## 媒体配置变更（Breaking Change）

从当前版本开始，图片本地化配置统一放在 `content.media`，不再读取 Notion 专属媒体字段。

已移除（且无兼容）：
- `content.notion.downloadImagesToLocal`
- `content.notion.imageDownloadDir`
- `content.notion.imageUrlBase`
- `content.notion.defaultImageUrl`

请改用：

```yaml
content:
  provider: notion
  notion:
    databaseId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
  media:
    downloadToLocal: true
    downloadDir: assets/uploads
    urlBase: /assets/uploads
    defaultImageUrl: /assets/images/noneimg-news.jpg
    fieldKeys: [cover, image, thumbnail, og_image]
    maxConcurrency: 4
    maxRetries: 3
    timeoutMs: 10000
```

## 推荐的数据库字段（建议按这个建）

以下字段名以 Notion UI 显示名为准，大小写敏感（建议直接复制粘贴字段名）。

### 引擎决策字段（强建议具备）

| 字段名 | 类型 | 作用 |
|---|---|---|
| `Published` | checkbox | 是否发布（建议只渲染已发布内容） |
| `Title` | title | 内容标题 |
| `Slug` | rich_text 或 formula(string) | URL slug（缺省可由 Title 生成，但建议显式稳定） |
| `Type` | select 或 multi_select | `page`/`post`（兼容层用途；推荐额外建 `Collection` 字段对应 site.collections key） |
| `PublishAt` | date | 发布时间（缺省可用当前时间，但建议显式） |

### 多语言相关字段（可选，但建议）

| 字段名 | 类型 | 作用 |
|---|---|---|
| `language` | rich_text / select | 内容语言（例如 `zh-CN`/`en-US`） |
| `i18n_key` | rich_text | 跨语言内容关联的稳定 key（例如 `about`, `pricing`） |

### 模板自定义字段（按需）

你可以添加任意字段作为“模板字段”，例如：

| 字段名 | 类型 | 模板用途 |
|---|---|---|
| `SEO Title` | rich_text | `page.fields.seo_title.value` |
| `SEO Desc` | rich_text | `page.fields.seo_desc.value` |
| `cover` | files / url | 封面图（`page.fields.cover.value`） |
| `My Link` | url | 链接（`page.fields.my_link.value`） |
| `reading_time` | number | 阅读时长 |

## 模拟数据（示例数据库表）

下面是一份“模拟数据”，帮助你理解一条 Notion 页面会如何变成站点内容（你可以在 Notion 里照着录入几条测试）。

| Published | Title | Slug | Type | PublishAt | language | i18n_key | SEO Title | tags | categories |
|---|---|---|---|---|---|---|---|---|---|
| ✅ | 关于我们 | about | page | 2026-01-01 | zh-CN | about | 关于我们 - My Site | company,intro | docs |
| ✅ | About | about | page | 2026-01-01 | en-US | about | About - My Site | company,intro | docs |
| ✅ | 第一篇博客 | first-post | post | 2026-01-10 | zh-CN | blog_first | 第一篇博客 - My Site | release,roadmap | updates |
| ⬜ | 未发布草稿 | draft-1 | post | 2026-01-20 | zh-CN | draft_1 | 草稿 - My Site | draft | draft |

说明：

- `Published` 用于构建过滤，避免草稿上站
- `language + i18n_key` 用于多语言站点内容关联（可选）
- `SEO Title` 等自定义字段需要 `fieldPolicy` 允许进入模板（见下一节）

> **推荐：使用 site.collections 替代 type 默认路由。** 如果你在 Notion 数据库中新增一个 `Collection` 字段（select 类型，值如 `blog`、`docs`），并在 site.yaml 的 site.collections 中声明对应的集合规则，引擎将优先使用 collection 驱动路由，而不是 type 兼容回退。

## 过滤与排序（filter / sort）

### 只渲染已发布内容

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    filterProperty: Published
    filterType: checkbox_true
```

### 按发布时间倒序

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    sortProperty: PublishAt
    sortDirection: descending
```

## 限额、指定拉取与缓存（大库/减少 Notion 请求）

### 1）maxItems：限制最多拉取条数

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    maxItems: 5000
```

### 2）includeSlugs：只拉取指定 slug 的页面

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    includeSlugProperty: Slug
    includeSlugs: [about, first-post]
```

说明：

- `includeSlugs` 会在 Notion 数据库 query 阶段做过滤（不是本地过滤），适合“只调试几篇文章/只构建部分站点”。
- 当前过滤使用 `rich_text.equals`，因此 `includeSlugProperty` 对应字段应为 rich_text 类型；如果你的 Slug 用的是 formula/string，请新增一个 rich_text 字段用于过滤。

### 3）cacheMode/cacheDir：缓存正文渲染结果

当 `renderContent=true` 时，引擎会读取 Notion block 并渲染 HTML。对大库或 CI 场景，可以启用磁盘缓存：

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    cacheMode: readwrite   # off | readwrite | readonly
    cacheDir: .cache/notion
```

行为说明：

- `off`：不使用缓存（默认）
- `readwrite`：缓存命中则复用；未命中会请求 Notion 并写入缓存
- `readonly`：只读缓存；未命中会报错（适合“强制离线/强制不打 Notion API”的 CI）

### 4）renderConcurrency/maxRps/maxRetries：并发渲染与限流（首轮构建提速）

当页面很多、且需要渲染正文时（blocks API 调用会很密集），推荐开启“受控并发 + 全局限流”来把吞吐稳定在 Notion 的 request limit 附近，并减少 429：

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    renderConcurrency: 4
    maxRps: 3
    maxRetries: 5
```

说明：

- `renderConcurrency`：同时渲染多少个页面的正文（越大越能隐藏网络 RTT，但 CPU/内存占用也会升高）。
- `maxRps`：对本内容源的所有 Notion HTTP 请求做全局限速（包括数据库 query + blocks children），默认建议 3。
- `maxRetries`：遇到 429 时的最大重试次数，会遵循 `Retry-After` 退避等待。

### 5）notion.stats：构建时的请求/节流统计日志

当你开启了 `maxRps`（或触发 429 重试）后，建议关注 Notion 内容源在构建结束时输出的一行统计日志：

```
event=notion.stats requests=1234 throttle_wait_count=56 throttle_wait_ms=7890
```

字段含义：

- `requests`：Notion HTTP 请求总数（包含数据库 query、blocks children、以及 429 重试产生的额外请求）
- `throttle_wait_count`：因 `maxRps` 限流而发生的等待次数
- `throttle_wait_ms`：因 `maxRps` 限流而发生的累计等待时长（毫秒）

## fieldPolicy：哪些 Notion 字段会进入 page.fields

Notion 的 properties 很多时候并不都需要进入模板。你可以用 `fieldPolicy` 控制：

## 支持的 Notion 字段类型（进入 page.fields）

当字段被 `fieldPolicy` 允许后，会按 Notion 字段类型映射到 `page.fields.<key>.type/value`：

| Notion 字段类型 | 模板字段类型（page.fields.<key>.type） | value 形态 |
|---|---|---|
| `title` | `text` | string |
| `rich_text` | `text` | string |
| `url` | `text` | string（URL） |
| `email` | `text` | string |
| `phone_number` | `text` | string |
| `number` | `number` | number |
| `checkbox` | `bool` | bool |
| `date` | `date` | date |
| `created_time` | `date` | date |
| `last_edited_time` | `date` | date |
| `created_by` | `text` | string（用户名或id） |
| `last_edited_by` | `text` | string（用户名或id） |
| `select` | `text` | string |
| `status` | `text` | string |
| `multi_select` | `list` | string[] |
| `people` | `list` | string[]（用户名或id） |
| `relation` | `list` | string[]（关联页面id） |
| `files` | `file` | string（文件URL） |
| `formula` | `text/number/bool/date` | 取决于公式类型 |
| `rollup` | `number/date/list` | 取决于 rollup 类型 |
| `unique_id` | `text` | string（prefix-number） |
| `verification` | `text` | string（state） |

提示：如果你只是想在模板里读取一个链接，建议不要把字段名命名为 `Url`（归一化后为 `url`），避免与“路由覆盖字段”混淆。

#### relation 的 *_links 派生字段（用于输出 title + url）

对于 Notion 的 `relation` 字段，除了原始的 `page.fields.<key>.value`（关联页面 id 列表）之外，引擎会额外生成一个派生字段：

- `page.fields.<key>_links.type == "list"`
- `page.fields.<key>_links.value == [{ id, title, url, slug, type }, ...]`

其中：
- `id/title/slug/type`：当关联页面也在本次拉取结果里时可填充；否则只有 `id`，其他为 null
- `url`：优先取关联页面的 `Url` 属性（Notion url 类型字段，归一化 key 为 `url`）并提升到 meta 后得到的外链；未设置则为 null

模板示例（生成类似 `visa (https://...)` 的结构）：

```scriban
{{ for x in page.fields.payments_links.value }}
  {{ if x.url }}
    <a href="{{ x.url }}">{{ x.title }}</a>
  {{ else }}
    {{ x.title }}
  {{ end }}
{{ end }}
```

#### 用 pageId 获取关联页面详情（site.data.pages_by_id）

当你在模板里拿到 Notion 的 pageId（通常来自 `relation` 字段的 `page.fields.<key>.value[]`），你可以通过内置 `pages-index` 插件提供的全站索引获取该页面的详情：

- `site.data.pages_by_id[pageId]` → `{ id, title, url, slug, type, publish_date, summary, fields }`
- pages-index 与内容源无关：Markdown/Notion/多源 sources 都可使用该索引
- 该索引在构建阶段生成，模板读取不会触发 API 请求；如需补全“不在本站输出范围内”的页面，需要开启 pages-index 的 Notion 补全能力（支持缓存；仅 Notion 内容源下生效）

补充：对于“不在本站输出范围内”的 Notion 页面（例如 relation 指向另一个数据库的页面），如果你开启了 pages-index 的 Notion 补全能力，会写入：

- `url`：空字符串（因为它不是本站路由）
- `external_url`：Notion 页面 URL（可用于直接跳转）

示例（从 relation 的 id 列表映射到标题与链接）：

```scriban
{{ for pid in page.fields.related_posts.value }}
  {{ p = site.data.pages_by_id[pid] }}
  {{ if p }}
    {{ if p.url }}
      <a href="{{ site.base_url }}{{ p.url }}">{{ p.title }}</a>
    {{ else }}
      <a href="{{ p.external_url }}">{{ p.title }}</a>
    {{ end }}
  {{ end }}
{{ end }}
```

#### relation 用作 tags/categories（taxonomy term 生成规则）

taxonomy 插件只读取 `meta.tags` / `meta.categories` 来生成 `/tags/` 与 `/categories/` 派生页。Notion provider 会把 `tags/categories` 从 fields 提升到 meta：

- 如果 `tags/categories` 是普通的 `multi_select`（推荐），则 term 直接是你选择的字符串
- 如果 `tags/categories` 是 `relation`，则会优先使用 `tags_links/categories_links` 里的 `title` 生成 term（回退 `slug`，再回退 `id`）

当 relation 的目标页面不在当前 `databaseId` 这次 query 的结果里时，引擎会额外请求 Notion API 拉取这些目标页面的基础信息来补齐 `title/slug`，以便生成可读的 term（最多补拉 200 个目标页；超过会截断，避免构建时请求爆炸）。

### 白名单（推荐：可控、安全、模板更稳定）

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    fieldPolicy:
      mode: whitelist
      allowed:
        - seo_title
        - seo_desc
        - cover
        - reading_time
        - my_link
```

### 全量（调试方便，但字段变化更容易影响模板）

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    fieldPolicy:
      mode: all
```

字段名会做归一化（例如 `SEO Title` → `seo_title`），所以建议：

- Notion 侧字段名尽量稳定
- 模板侧统一用“下划线小写”访问（`page.fields.seo_title`）

注意：

- Notion 的 `url` 类型字段会进入 `page.fields.<key>.value`，值是字符串 URL。
- 如果你把字段名命名为 `Url`（归一化后为 `url`），它会同时被用作“路由覆盖字段”（影响该页面的最终 URL）。如果你只是想在模板里拿一个链接，建议用别的名字（例如 `My Link`）。

## 常见错误与修复

### 1）报错：缺少 NOTION_TOKEN

现象：`doctor` 或 `build` 在配置校验阶段直接失败。

修复：

- 本地：设置环境变量 `NOTION_TOKEN`
- CI：在 GitHub Actions Secrets 中添加 `NOTION_TOKEN`，并注入到工作流环境

### 2）报错：databaseId 无效 / 无权限

现象：构建时拉取失败、提示 Notion API 相关错误。

修复清单：

- databaseId 是否是“数据库”的 ID，不是页面 URL
- Integration 是否被分享到了这个数据库（Notion 数据库右上角 Share）
- token 是否属于同一个 workspace/有权限

### 3）报错：字段类型不匹配

例如你把 `PublishAt` 建成了文本，而配置里用它排序。

修复：

- 按本页推荐字段类型创建（date/checkbox/select 等）
- 或调整 `filterProperty/sortProperty` 指向真实类型一致的字段
