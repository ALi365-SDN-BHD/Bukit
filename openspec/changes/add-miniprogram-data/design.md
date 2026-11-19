## Context

动机和范围见 proposal.md。现状：

- 内容文档经 `ContentPipeline` 加载，经 `ImageLocalizeStage` 处理图片：
  `ContentImageRewritePipeline` 会下载 `file`/`files` 类型的字段和 `content.media.fieldKeys`
  里列出的字段，并改写成本地路径。data 模式的文档和内容文档在同一条流水线里加载，
  **预期**也会被本地化，但目前没有测试覆盖这一点（任务 0.3）。
- `mode: data` 的文档不进入 `RoutePipeline`，由 `DataModuleBuilder` 按 `type` 分组成
  `site.modules`；`dataIndex` 产出 `site.data_index`。两者都没有公开的文件输出。
- 导航只来自 `site.yaml` 的 `menus`（`MenuPlugin`）。
- 每篇内容的 JSON 由 `JsonContentDocumentProjection` 写出，末尾是 `Environment.NewLine`，
  所以 Windows 和 unix 构建出来的字节不同。小程序数据**不复用**这份文件。
- SHA-256 工具在 `BuildReportHashing`（`BuildReportFileHelpers.cs`）。
- `agent-manifest.json` 的 `generatedAt` 取内容的最大发布时间，不取当前时间。小程序
  目录沿用这个做法。

## Goals / Non-Goals

**Goals:**
- 小程序冷启动只需一个很小的请求就能判断"没有任何变化"。
- 内容不变时重新构建，输出字节完全一致（跨操作系统也一样）。
- 编辑只在 Notion 里改，不用碰 YAML 或仓库，就能更新小程序里的五类数据。
- 坏数据在构建时就失败，不会发到小程序。

**Non-Goals:**
- 见 proposal.md。另外：不做服务端分页，不做按用户区分的数据，不做数据加密。

## 配置示意

```yaml
miniprogram:
  enabled: true
  outputDir: mp                 # 默认 mp
  datasets:
    - name: articles
      kind: collection
      source: notion-blog       # content.sources[].name
      listFields: [title, summary, cover, publishedAt, tags]
      detailFields: [title, body, cover, publishedAt, updatedAt, author, tags, media]
      sort: { field: publishedAt, order: desc }
    - name: banners
      kind: module
      source: notion-modules
      module: banner            # 对应 type 属性
      fields: [title, image, link, order]
      sort: { field: order }
      required: [title, image]
    - name: nav
      kind: module
      source: notion-modules
      module: nav
      fields: [title, link, order, parent]
      sort: { field: order }
    - name: site-nav            # 或者直接复用网站的 menus
      kind: menu
      menu: main
    - name: settings
      kind: dataIndex
      source: notion-settings
      scope: app
    - name: stores
      kind: records
      source: notion-stores
      fields: [name, address, phone, lat, lng, photo, order]
      sort: { field: order }
      types: { lat: number, lng: number, photo: image }
    - name: tags
      kind: taxonomy
      taxonomy: tags
```

数据集名就是输出文件名的前缀，也是小程序端的缓存 key，只允许 `[a-z0-9-]`。

## 输出布局

```
dist/mp/zh-CN/
  catalog.json                              # 唯一不带哈希、需要禁用缓存的文件
  site.<h16>.json                           # 站点信息（自动生成，不用声明）
  banners.<h16>.json
  nav.<h16>.json
  settings.<h16>.json
  stores.<h16>.json
  tags.<h16>.json
  articles.list.<h16>.json                  # 只含 listFields，不含正文
  articles.details.<h16>.json               # 二级目录
  articles/<id>.<h16>.json                  # 每篇详情
```

`<h16>` 是该文件 SHA-256 十六进制的前 16 位；目录里记录完整哈希。单语言站点也用
`mp/<site.language>/`，路径规则不因语言数量而变。

### catalog.json

```json
{
  "schema": "https://bukit.dev/schemas/miniprogram-catalog.v1.json",
  "schemaVersion": "1.0",
  "minClientSchema": "1.0",
  "language": "zh-CN",
  "generatedAt": "2026-11-18T08:00:00+00:00",
  "catalogHash": "sha256:…",
  "datasets": {
    "articles": {
      "kind": "collection",
      "hash": "sha256:…",
      "url": "https://example.com/mp/zh-CN/articles.list.9c1d….json",
      "size": 18342,
      "detailCatalog": {
        "hash": "sha256:…",
        "url": "https://example.com/mp/zh-CN/articles.details.77ab….json"
      }
    },
    "banners": { "kind": "module", "hash": "sha256:…", "url": "…", "size": 812 }
  }
}
```

- `catalogHash` = 按数据集名排序后，把 `name`、`hash` 和 `detailCatalog.hash`（如果有）
  依次拼起来再算 SHA-256。目录本身其他字段的变化不影响它。
- `generatedAt` = 所有被输出记录的 `updatedAt ?? publishedAt` 的最大值；没有记录时为 Unix
  纪元。不取当前时间。

### 二级目录 `articles.details.<h16>.json`

```json
{ "dataset": "articles", "items": [ { "id": "…", "hash": "sha256:…", "url": "…", "size": 5120, "updatedAt": "…" } ] }
```

按 `id` 排序。

## Decisions

决定 1–7 已于 2026-11-19 逐项确认。

1. **已确认：实现位置是 Core 内置插件，默认关闭。** 放在 `Bukit.Engine/Plugins/BuiltIn/`
   下，作为一个新的内置插件，拿得到内容文档、data 文档、`dataIndex`、taxonomy、已本地化的
   媒体，并且挂在投影计划、构建清单、增量和过期清理上。
   备选 A：外部插件（WechatSync 模式），读不到 data 文档，需要自己再调 Notion，不选。
   备选 B：Core 新增公开的 data 输出 + 外部插件，会把原本不公开的数据模块发布出去，
   而且仍然要改 Core，不选。

2. **已确认：数据集白名单。** 只有声明过的数据集和字段会输出。站点改了小程序不用的字段，
   哈希不变。

3. **已确认：目录分两层。** 小程序先比 `catalogHash`；内容数据集的详情放在二级目录里。
   一千篇文章时，单层目录大约 150KB，两层的一级目录只有几 KB。备选：单层目录，实现简单，
   但只适合小站点。

4. **已确认：数据文件按内容哈希命名。** 文件内容不会变，CDN 可以永久缓存；
   新目录引用的文件在部署后一定存在，部署天然原子。备选：固定文件名加 `?v=hash`，
   URL 好记，但依赖 CDN 正确处理查询参数，部署期间可能读到不一致的数据，不推荐。

5. **已确认：列表不做服务端分页。** 每个 collection 输出一个只含 `listFields`
   的完整索引，由小程序在本地分页。如果按"最新在前"分页，新增一篇就会让所有分页的
   哈希都变。数据量超过 `maxListItems`（默认 2000）时构建失败，提示拆分数据集。
   备选：从最旧开始编号的稳定分页，复杂度更高，等确有需要再加。

6. **已确认：正文格式是清洗后的 HTML 子集。** 输出 `body` 为白名单标签（`p h1–h6 ul ol li
   blockquote pre code strong em a img table thead tbody tr th td hr br span`），属性只保留
   `href src alt` 和 `data-bukit-id`；class 改成内联样式（复用 WechatSync `ContentProcessor`
   的规则，把它移到共享的 internal helper）；iframe 和视频嵌入改成链接；`<details>` 展开。
   小程序端可以直接交给 `rich-text`，或者用 mp-html 这类组件。
   备选：`rich-text` 的 nodes JSON，小程序端零依赖，但输出大约是 HTML 的 2–3 倍，转换器
   也更复杂。

7. **已确认：只承诺微信小程序。** 输出本身和平台无关；正文标签白名单按微信 `rich-text`
   定。支付宝、抖音小程序的兼容性写进文档，但不作为验收条件。

8. **排序确定。** 每个数据集必须有 `sort`；`collection` 默认按 `publishedAt desc`。排序值
   相同时按 `id` 升序。Notion API 返回的行顺序不参与输出。

9. **字节确定。** 由小程序数据自己的 `JsonSerializerContext` 序列化：camelCase、缩进关闭、
   UTF-8 无 BOM、末尾一个 `\n`；时间统一输出 ISO 8601 带偏移；字段顺序按声明顺序。
   哈希按写盘的字节计算。

10. **只输出公开内容。** 排除 `enabled: false`、草稿、未到发布时间、已过期的记录，以及
    `Indexable == false` 的内容页（与 agent manifest 一致）。被排除的记录直接从目录里消失，
    小程序端按删除处理。

11. **媒体和链接。** 声明为 `image` 类型的字段和正文里的 `img src`、`a href` 都转成
    `site.url` 下的绝对 HTTPS 地址。转换后如果仍是 Notion 签名 URL（匹配
    `content.media.downloadHosts`，或主机是 `*.amazonaws.com` 且带签名参数），构建失败。
    站内链接再加上 `data-bukit-id="<目标 id>"`。

12. **site 数据集自动生成。** 包含 `name`、`language`、`languages`、`url`、`logo`。

13. **已确认：导航两种来源都支持。** 站点按数据集自己选择：
    - `kind: module`（`module: nav`）：编辑在 Notion 里维护，字段和排序按数据集声明；
    - `kind: menu`（`menu: main`）：输出 `site.yaml` 里 `menus.<name>` 对应语言的条目，
      保持 `menus` 的原有顺序和层级，字段固定为 `title`、`url`、`children`（加上 `menus`
      已有的可选字段），链接按决定 11 转成绝对地址；没有 `source`。
    两种来源可以同时声明成两个数据集（名称不同）。`menu` 引用的菜单在 `site.yaml` 里不存在时，
    配置校验失败。网站自己的导航仍然只来自 `menus`，不受影响。

14. **校验。** 每个数据集可以声明 `required` 和 `types`（`string number boolean date image
    url string[]`）。校验失败时报诊断码 `miniprogram-dataset-invalid`，带上数据集名、记录 id
    和字段名，构建失败。`site.url` 不是 `https://` 开头时报 `miniprogram-site-url-required`。

## 小程序端同步协议（文档内容，非规范性）

1. 启动时请求 `mp/<lang>/catalog.json`，带 `?t=<时间戳>` 绕过 CDN 缓存。
2. `catalogHash` 和本地相同 → 结束。
3. `minClientSchema` 高于客户端支持的版本 → 提示用户更新小程序，继续使用本地数据。
4. 逐个比较数据集的 `hash`：
   - 小数据集（site、module、dataIndex、records、taxonomy、collection 的列表）：变了就立即拉取；
   - `detailCatalog` 变了：拉取二级目录，把哈希变了的详情标记为过期，用户打开时再拉；
     有本地旧版本就先显示旧的。
5. 本地有、目录里已经没有的数据集和详情，从本地删除。
6. 全部拉取成功后，才把新目录写入本地（失败时保留旧目录，下次重试）。
7. 某个数据文件返回 404（部署切换期间），重新拉取一次目录再试。
8. 并发不超过 5 个请求；本地缓存总量要控制在 10MB 以内，必要时按最近最少使用淘汰详情。

## Risks / Trade-offs

- [data 文档的图片本地化可能没有覆盖到] → 任务 0.3 先写测试确认。没覆盖到的话，扩展
  `ImageLocalizeStage` 本身也是 Core 变更，需要另外确认。
- [Notion 富文本属性的输出格式] → 一律输出纯文本；需要富文本的内容放在页面正文里，用
  `kind: collection` 或 `module` 的 `body` 字段。
- [列表索引过大] → `maxListItems` 上限，超过时构建失败。
- [编辑修改后小程序要等重建] → 文档给出定时构建和手动触发的 CI 示例；Notion 自动化 webhook
  只作为可选方案写进文档（取决于 Notion 套餐）。
- [`dist` 目录多一组文件] → 默认关闭，只有开启的站点才会生成。
- [旧哈希文件在 clean build 后消失，小程序正在用的旧目录会 404] → 同步协议第 7 步处理。

## Migration Plan

新增的可选输出，不开启就没有任何变化。正在用 `data/*.yaml` 的站点要把这些数据搬到 Notion
数据库，文档里给出建模对照表；导航可以继续用 `menus`（`kind: menu`），也可以搬到 Notion。回滚：关闭 `miniprogram.enabled`，下一次构建会按
投影计划清理 `mp/`。
