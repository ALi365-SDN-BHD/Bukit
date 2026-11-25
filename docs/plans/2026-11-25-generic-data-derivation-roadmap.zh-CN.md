# Bukit 通用数据能力规划：让站点不再需要自建转换层

> **状态：** 规划草案 v3（2026-11-25），没有立项。下文的配置和模板接口都是草案，立项时再定。修订记录见第 11 节。
>
> **执行约束：** 这里的功能点都改 Core（`src/Bukit-Core/`）。按 `AGENTS.md` 的 Website/Core 隔离规则，每一项都要作为单独的、用户确认过的 Core 任务立项，并通过 `AGENTS.md` 要求的验证边界和 public-api-drift 检查。openspec、三语文档和 CHANGELOG、BukitJalil 契约表这些流程，沿用 CloudBase 内容源立项时的做法。本文只做规划，不授权实施。
>
> **依据：**
> - Bukit 代码基线：`main` 提交 `bc660d1d`。CloudBase 内容源见 [实施方案](2026-11-23-cloudbase-content-source-implementation-design.zh-CN.md)（下文简称"CloudBase 方案"，D1～D18 指它第 4 节的决策）。
> - 消费方：travelWeb（马来西亚定制旅行站）仓库。转换层在 `poc-bukit/transform/`（`index.mjs`、`rules.mjs`，在 travelWeb 的 `refactor/split-sync` 分支上，合并前 Bukit 仓库的读者看不到），主题模板在 `poc-bukit/themes/nanyang/layouts/`，站点配置在 `poc-bukit/site.yaml`。

## 1. 背景

travelWeb 已经决定：所有内容放在 CloudBase（PG 模式），前台页面和 CMS 共用这一个数据源。

公开站现在的构建方式是：

```
内容源 → 读取层 → 原始记录 → 转换层（Node） → data/*.json + content/*.md + 图片 → bukit build
```

转换层生成 17 个 `data/*.json`（模板通过 `site.data_files` 读取）、每页一个 Markdown，并下载图片。主题的 27 个模板文件（其中 22 个页面模板）几乎全部依赖这些预先算好的数据，`partials/helpers.html` 开头写明了原因：关联关系预先算好，模板里只取值，避免触发 Scriban 的累计循环上限。

Bukit 的 CloudBase 内容源（还没发布，在 CHANGELOG 的 `[Unreleased]` 下）只能把一张表或视图读成一组文档，取代不了转换层：

- 字段值只保留字符串、数字、布尔、字符串数组。JSON 对象会被丢掉，数组里不是字符串的元素也会被丢掉（`CloudBaseFieldProjector.TryConvert`）。
- 不支持关联字段（CloudBase 方案第 13 节排在 P3）；跨源关系投影是 P1 的非目标（方案 1.2 节），还没有排期。
- 不能生成 `site.data_files`。`mode: data` 的源进入的是 `site.data`、`site.modules` 和 `site.data_index`。
- 没有缓存和离线模式（排在 P2）。

本文梳理转换层做的事，判断哪些已经能用 Bukit 现有机制做、哪些可以做成通用功能、哪些只能留给站点。目标是让关系多、派生数据多的站点也能直接从数据源构建。

## 2. Bukit 现有的相关能力

| 能力 | 现状 | 和本文的关系 |
|---|---|---|
| 静态 `data_files` | 站点 `data/` 目录下的任意 JSON/YAML 进入 `site.data_files`（`DataFilesPlugin`） | 常量（标签、配色、首页固定列表、默认图映射）提交成静态文件就够，不需要转换层，也不需要新功能 |
| `site.related` | 按共同取值打分，结果在 `site.related_pages[id]`，每项只有 `title`、`url`、`score`。索引只支持 `tags`、`categories`、`keywords`、`collection`、`type`、`date`；全站一套；排序用 `List.Sort`，同分顺序没有定义 | 见 F5 |
| `customFields[].reference` | `ContentReferenceRuleConfig`：`targetType`、`idField`、`labelField`、`urlField` | 现成的引用规则模型，F2 应该在它的基础上扩展 |
| `relationMappings` | 把原始字段映射成标准的 `related_to`，可选 `targetType`、`targetField`、`targetIdField`；按文档用于内容模型、结构化数据和审计 | 模板文档里没有按引用取目标页面的用法，也没有反查 |
| Notion 源的关联解析 | 支持关联解析和跨源关联查找 | CloudBase 源没有对应能力 |
| `mode: data` + `dataIndex` | 按“源 → 范围 → 键”两层取值；值可以是标量，也可以是 `object`、`object[]`；源名、字段名、范围、键都要符合 `^[a-z][a-z0-9_]*$` | 适合文案和站点设置；travelWeb 的文案键不符合，见 F10 |
| `templateLoopLimit` | 默认 1000，可以调高。上限按每个最外层 `for`/`while` 分别累计（包括其中的嵌套循环和调用的函数），组件和区块另起计数；`array.filter`、`array.map` 加匿名函数不计入上限 | 循环上限不是硬阻碍，见 F3 的动机 |
| `BUKIT_<段>__<段>` 环境变量覆盖 | 可以用环境变量覆盖配置，但列表项不能这样设置 | `content.sources` 是列表，所以按发布版本筛选需要 F6 |
| permalink | 占位符只有 `{slug}`、`{title}`、`{year}`、`{month}`、`{day}`、`{type}`、`{collection}`；front matter 的 `route: { url }` 可以逐页覆盖 | 见 F12 |
| 增量构建 | `RenderReadRecorder` 记录每次渲染读了哪些共享数据；`page` 根被视为不读共享数据，没登记的新根被当作“全部读取” | 新的引用、反链、查询都要登记，见第 5 节 |

## 3. 转换层做的事和差距

分三类：**常量**（静态文件就够，不算差距）、**通用功能**（第 4 节）、**站点规则**（第 6 节）。

| # | 转换层做的事 | travelWeb 的例子 | 归类 |
|---|---|---|---|
| 1 | 常量映射 | 枚举标签、`themeStyle`/`expTone` 配色、首页固定的体验和攻略、静态页分享图、默认图映射（`rules.mjs`） | 常量：静态 `data/*.json`；以后由 CMS 字段定义生成时见 F8 |
| 2 | 结构化字段 | 路线每日行程（`object[]`）、攻略分段、推荐月份 `[3,4,5]`、联系方式对象 | F1 |
| 3 | 正向引用，按数组顺序 | 路线 → 途经城市；每日行程 → 景点、体验；内容 → 图片键 | F2 |
| 4 | 反向关联，含嵌套路径和过滤 | 城市页“经过这里的路线”；景点页“包含这个景点的路线”（来自 `days[].attractions`）；主题页“适合这个人群的城市”（按 `bestFor` 过滤） | F3 |
| 5 | 列表、筛选、去重、分组、取前几条 | 有页面的城市、有路线的城市、出现过的景点类型、精选在前取 6 条、按 12 个月分组的推荐路线和节庆（对数字数组做“包含”判断）、按标签定义顺序排列的攻略分类、景点页“同城前 2 个体验” | F4 |
| 6 | 按自定义字段的相似推荐 | 相似路线：共同城市数 + 共同人群数，不设阈值，取前 3，同分按路线原顺序 | F5 |
| 7 | 一次构建读同一个发布版本 | CMS 按 release 发布 | F6 |
| 8 | 离线、可复现的构建 | 同一份内容重复构建，逐字节回归 | F7 |
| 9 | 键值文案 | 247 条页面文案（模板里写 `C["key"]`） | F10 |
| 10 | 媒体资产 | 图库图片下载成固定文件名 `<file>.webp` 和 `<file>-640.webp`（模板的 srcset 写死了 `-640`）；顾问、评价照片下载成 `advisor-N.ext`；署名信息 `images.json` | F11 |
| 11 | 页面网址 | 景点 `/malaysia/{city}/attractions/{slug}/`；静态页的网址来自数据（`routeUrl`）；现在靠 front matter 的 `route: { url }` 逐页覆盖 | F12 |
| 12 | 同一张表部分出页面 | 18 个城市只有 8 个出页面，但全部城市都会被引用（周边、攻略相关城市、全部城市列表） | F13 |
| 13 | front matter 里的派生字段 | `seo_title`（“{城市名}定制旅游攻略”）、拼接出来的 `summary`（城市、体验）、主题的标题 `“{名称} · 马来西亚”`、`seo_desc`、`image`（图片键 → 文件路径）、`image_alt`、`parent_url`（面包屑）、固定的 `publishAt` | F9 |
| 14 | 正文由结构化字段拼成 | 攻略正文由 `sections[]`（标题、段落、列表）拼成 Markdown；城市、景点、体验由段落数组拼成；路线正文用 `tagline`，主题用 `description` | 站点：建议由 CMS 存 Markdown 正文（第 6 节） |
| 15 | 小程序数据 | `miniprogram-data/`，路线详情要 `days`（`object[]`），城市和路线详情要 `body` | 依赖 F1；正文是否受 CloudBase P3“小程序正文规则”影响：待确认 |
| 16 | 单页的小计算 | 住宿分段和晚数、换城日（标题含“→”）、相册取前 5 张存在的图片、“3–5月”这类月份文字（跨年写“11月–次年2月”）、节庆的 12 个月标记、城市配色轮换、城市短名 | 站点：模板函数 |
| 17 | 业务规则 | 示例电话不生成 `tel:` 链接；文案里有停用号码就中止构建；路线默认图按 slug 散列挑选；咨询对话的选项组合（`chat.json`） | 站点：第 6 节 |

## 4. 功能点

### F1 CloudBase 源保留结构化字段

- **问题**：`CloudBaseFieldProjector` 丢掉 JSON 对象和非字符串数组元素。Markdown 源的 front matter 支持嵌套对象和对象数组（`MarkdownFrontMatterParser`），两种源的能力不一致。
- **做法（草案）**：jsonb 对象和数组按原结构投影。规范形状复用 `DataIndexObjectValue` 的规则：最大深度 8、键顺序规则统一、加大小上限。
- **类型**：Markdown front matter 的列表元素是字符串。CloudBase 如果保留数字，同一个模板对两种源的 `array.contains` 结果会不同。立项时要二选一：两种源都保留类型，或者 CloudBase 也统一成字符串（文档写明）。
- **隐私**：白名单只看顶层字段名不够，嵌套值也要走白名单（例如 `allowedPaths`）；硬性剔除的敏感名要作用到嵌套键。D11 只兜底 CloudBase 标识（envId、`cloud://`、网关主机、签名链接），**不检测个人信息**，不能作为个人信息的兜底。
- **`cloud://`**：嵌套值里出现 `cloud://` 时，是丢掉整个字段还是只丢这一项，要定下来（现有规则是不在 `fileFields` 里的字段整个丢掉，见 D17）。
- **通用性**：高。
- **改动范围**：小到中。

### F2 引用字段

- **问题**：模板只能拿到 slug 数组，要自己去别的集合里找目标页面。
- **做法（草案）**：扩展现有的 `customFields[].reference` 规则，支持数组和嵌套路径，构建时解析成目标文档。配置键不要叫 `references`（它已经是保留的来源字段名，见 `ContentFieldReservations`）：

  ```yaml
  content:
    links:
      - collection: route
        field: cities
        target: { collection: city, key: slug }
      - collection: route
        field: days[].attractions
        target: { collection: attraction, key: slug }
      - collection: route
        field: image_key
        target: { source: media, key: key }     # 引用 mode: data 的源
  ```

- **形状**：普通数组字段解析成有序列表。嵌套路径要同时提供两种形状：按原结构分组（每天的景点，模板逐天显示），和打平去重（全程途经景点）。
- **写进页面字段**：解析结果要能写进页面字段（例如图片键解析成文件路径写进 `image`），这样 SEO、OG 分享图和搜索索引也能用到。
- **改动范围**：中。目标不存在时按 `schemaFailMode` 报错或警告，复用 `requireRelationTargets`。
- **依赖**：CloudBase 源上的嵌套路径依赖 F1；目标集合里不出页面的条目依赖 F13。

### F3 反向索引

- **问题**：城市页要列“经过这里的路线”，景点页要列“包含这个景点的路线”。
- **动机**：模板里用 `array.filter` 反查不计入循环上限，所以不是做不到。但反查要遍历每条路线的每一天，计算量随内容数量平方增长，每个模板都要重复写；更重要的是模板里算出来的结果，SEO、搜索索引和小程序数据都用不上。
- **做法（草案）**：基于 F2 的引用声明生成反向索引，支持过滤和取前几条：

  ```yaml
  content:
    links:
      - collection: route
        field: days[].attractions
        target: { collection: attraction, key: slug }
        backref: { name: routes, limit: 0 }
  ```

- **顺序**：反向列表按引用方集合的顺序排列。“集合顺序”要先定义清楚：多个源拼接时按什么排（建议按集合的排序字段，同值按源加载顺序），同一个引用方只出现一次。
- **改动范围**：中。依赖 F2。

### F4 命名查询

- **问题**：页面要的各种有序列表现在由转换层生成在 `order.json`、`months.json` 里，模板里 `D.order` 出现 44 处。
- **做法（草案）**：在 `site.yaml` 里声明查询，结果放进 `site.queries.<name>`。语法只做一组封闭的操作：筛选（等于、包含、有无反链）、稳定排序、按枚举定义顺序排序、去重、分组、取前几条：

  ```yaml
  site:
    queries:
      route_cities:
        from: city
        where: [{ field: has_page, eq: true }, { has_backref: routes }]
      featured_routes:
        from: route
        sort: [{ field: featured, direction: desc }]   # 稳定排序
        limit: 6
      routes_by_month:
        from: route
        group: { field: best_months, values: [1,2,3,4,5,6,7,8,9,10,11,12] }  # 对数组字段按“包含”分组
        limit: 3
  ```

- **按页面参数的查询**（“和当前景点同城的前 2 个体验”）：先用模板里的 `array.filter` 解决，不进 F4。
- **改动范围**：中。依赖 F3（按有无反链筛选）。

### F5 `site.related` 支持自定义字段

- **问题**：相似路线要按“共同城市数 + 共同人群数”打分。
- **做法（草案）**：
  - `indices` 允许写 `field: <字段名>`，按共同元素个数乘权重计分；
  - 允许按集合分别配置（只在同一集合内比较），阈值可以设为 0；
  - 输出里加上文档 id 和 slug，模板才能取到卡片要的完整数据；
  - 排序改为稳定排序：同分按集合顺序。现在用的 `List.Sort` 不稳定，同分顺序没有定义。
- **改动范围**：小到中。要改 public 的 `RelatedIndexConfig`（见第 5 节）。

### F6 按发布版本读取

- **问题**：CMS 按 release 发布，构建要读同一个 release 的全部内容。现有的 `BUKIT_` 环境变量覆盖不能设置列表项，而 `content.sources` 正好是列表。
- **做法（草案）**：
  1. 筛选值可以引用环境变量，例如 `filter: { field: release_id, value: "${env:RELEASE_ID}" }`（值要加引号，裸写 `{`、`}` 在 YAML 流式映射里很可能不合法）。变量没有设置或为空时以诊断码失败，不能退化成空值查询；否则结合 RLS 的静默过滤，可能读到 0 条内容也构建成功。
  2. 文档给出“只看当前线上版本”的视图写法（`security_invoker` 加 RLS），并说明构建途中有人发布时的取舍。
- **多条件**：P1 的筛选只支持单字段等值。城市表要同时按 `release_id` 和是否出页面筛选时放不下，要用视图，或者扩展多条件筛选。
- **安全取舍**：Bukit 只用 Publishable Key（`anon`）读取，这个密钥不算秘密。做法 1 要求 `anon` 能读到“正在构建、还没上线”的 release，内容在构建成功之前就能被公开读到，构建失败的 release 也会暴露。可选：(a) 接受这个窗口，文档写明；(b) 构建时用只给 CI 的高权限凭据，这要扩展 D5 的凭据策略并重新评估 G0-4。立项前先定。
- **改动范围**：小（不含凭据策略的扩展）。环境变量只能用在筛选值里，`envId`（D18）、`table`（D4）这类会拼进地址的字段不能用。

### F7 CloudBase 离线快照

- 就是 CloudBase 方案第 13 节的 P2：缓存、离线快照、`BuildTransaction` 登记资源。快照里只存投影后的公开字段。改动大（Notion 的同类机制约 1.5k 行）。

### F8 枚举标签来自模型（可选）

- **现状够用**：标签是常量，提交成静态 `data/*.json` 就能用（第 3 节第 1 项）。只有想让 CMS 字段定义成为唯一来源时才需要 F8。
- **做法（草案）**：字段的 `enum` 支持“取值 + 标签”，按定义顺序输出到一个不和 `site.data.<源名>` 冲突的命名空间。
- **改动范围**：中。`CustomFieldDefinitionConfig.Enum` 是 public 的 `IReadOnlyList<string>`，要过 public-api-drift；标签顺序要保留，不能用会按 ordinal 重排键的结构。`themeStyle`、`expTone` 是配色，不是枚举标签，不在 F8 范围内。

### F9 集合级派生字段

- **问题**：SEO 标题、摘要句式、描述、分享图、替代文字、面包屑父级现在由转换层写进 front matter。Bukit 的 SEO 模型（`SeoModelBuilder` 读 `seo_title`、`seo_desc` 等字段，没有 `seo_desc` 时从 `summary` 截取）、OG 分享图、JSON-LD 和搜索索引读的都是页面字段，模板改不了这些输出。
- **做法（草案）**：集合上声明派生字段，加载阶段按 Scriban 表达式生成：

  ```yaml
  content:
    derivedFields:
      city:
        seo_title: { value: "{{ name }}定制旅游攻略" }
        summary: { value: "{{ name }}（{{ name_en }}）旅游：{{ summary }} 景点、路线……", mode: override }
        image: { value: "/assets/img/{{ links.image_key.fields.file }}.webp" }
        seo_desc: { value: "{{ summary | seo.describe 140 }}" }
  ```

- **模式**：`default`（字段为空时才生成）和 `override`（总是生成）。城市、体验的 `summary` 是用原值拼出的新句子，只有“为空才生成”永远不会触发。
- **受控函数**：描述的补句和按显示宽度截断这类规则，以受控函数提供（例如 `seo.describe`），或者接受描述变化并写进验收（第 7 节）。
- **限制和确定性**：表达式只能引用本条内容的字段和 F2 解析出的引用，不能遍历其他集合；单独的循环上限；出错时按 `schemaFailMode` 处理；固定文化格式（数字和日期格式与系统区域设置无关），保证逐字节稳定。
- **备选**：由 CMS 发布时把这些字段算好写进库里。Bukit 不用改，但每个站点的 CMS 都要自己实现。
- **改动范围**：中。依赖 F2。

### F10 `dataIndex` 键名

- **问题**：travelWeb 的 247 个文案键**全部**不符合 `^[a-z][a-z0-9_]*$`：用 `.` 分层（最深 4 段），有 `-`（`custom.days.4-5.label`），56 个含大写（驼峰，如 `custom.daysHint`），2 个含 `+`（`custom.days.10+.label`）。另外 `dataIndex` 是“范围 → 键”两层结构，扁平的键要决定怎么切分范围。
- **做法**：二选一：
  - (a) 键名规则放宽到 `[A-Za-z0-9._+-]`，并定下范围怎么切（例如固定一个范围，或者按第一段切）；
  - (b) 不改 Bukit：travelWeb 把 247 个键改成符合规则的写法（处理 `.`、`-`、大写、`+`），同时改模板和 `audit-copy`。
- **通用性**：(a) 通用，i18n 文案常用点号分层的驼峰键。
- **改动范围**：(a) 小，但要确认键名规则是否被路由元数据、BukitJalil 依赖。

### F11 媒体资产（`mode: data` 的换链和本地化）

- **问题**：媒体本地化只处理 content 模式的 `fileFields`，下载到 `content.media.downloadDir`；CloudBase 的 `mode: data` 不换链，含 `cloud://` 的字段整个丢掉。图库、顾问照片、评价照片都是不出页面的数据，现在由转换层下载成固定文件名，并生成 640 宽的版本；模板的 srcset 写死了 `-640.webp`。没有这一项，F2、F9 拼出来的 `/assets/img/<文件>.webp` 都是空链接。
- **做法（草案）**：`mode: data` 的源也支持 `fileFields` 的签名和本地化；本地文件名稳定（按对象名或字段值）；响应式尺寸改用 Bukit 的图片处理生成，模板随之改，不再依赖写死的 `-640` 文件名。
- **安全**：沿用 D8、D17、D11，换链失败时用占位图。
- **改动范围**：中。

### F12 permalink 支持字段占位符

- **问题**：景点网址含所属城市（`/malaysia/{city}/attractions/{slug}/`），静态页的网址来自数据。permalink 只支持内置占位符，现在靠 front matter 的 `route: { url }` 逐页覆盖。
- **做法（草案）**：二选一：
  - (a) permalink 支持字段占位符（`{fields.city}`），值按现有规则清洗，结果要在构建时检查是否重复；
  - (b) 把网址存进库：依赖 F1，表里加 jsonb 列 `route`（`{ "url": ... }`），由 CMS 生成。
- **改动范围**：(a) 中；(b) Bukit 不改，CMS 负责。

### F13 同一张表部分出页面

- **问题**：Bukit 的 content 集合里每条都会出页面。travelWeb 的城市表有 18 条，只有 8 条出页面，其余 10 条只被引用和列出。
- **做法（草案）**：集合支持“出页面条件”（例如 `routeWhen: { field: has_page, eq: true }`），不满足的条目不出页面、不进 sitemap 和搜索，但仍然可以被 F2 引用、被 F4 查询。备选：同一张表配两个源（一个 content 模式只读出页面的行，一个 data 模式读全部行），但这样 F2 要能跨这两个源解析，而且同一条内容会有两份。
- **改动范围**：中。

## 5. 横向要求（每个功能点都要满足）

- **增量构建**：F2、F3 的结果如果挂在 `page` 上，目标页的标题、网址或反链变了，引用方的页面不会重新渲染（`page` 根被视为不读共享数据）。要么把引用和反链结果计入页面哈希，要么登记成共享集合；`site.queries` 这类新根要登记进 `RenderReadRecorder`，否则会被当作“全部读取”，增量构建就失效了。
- **确定性**：所有排序都用稳定排序，并写明同值的规则；对象的键顺序有统一规则；表达式求值用固定文化格式。同一份数据重复构建要逐字节一致。
- **public API**：F2、F4、F5、F8、F9、F12、F13 会给 public 的配置记录加字段（例如 `RelatedIndexConfig`、`CustomFieldDefinitionConfig`、内容配置）。每一项立项时要列出改动的 public 类型；能用 internal 配置类型的，沿用 CloudBase 方案 D1 的先例。
- **隐私**：新暴露到公开产物的数据（嵌套字段、引用目标的字段、查询结果）都只能来自白名单字段。

## 6. 不做成通用功能的部分

- **单页的小计算**（第 3 节第 16 项）：写成主题里的模板函数，每页只处理自己的数据。
- **正文**（第 14 项）：建议 CMS 直接存 Markdown 正文。攻略分段、段落数组改成一个 Markdown 字段，Bukit 用 `bodyField` 读取。这样 Bukit 不用新增“拼正文”的功能。
- **业务规则**（第 17 项）：
  - 停用号码检查、示例电话的处理：放到 CMS 的发布函数里校验和生成（例如在发布时写好 `phoneHref`），不放在构建里；
  - 路线默认图：CMS 发布时写入图片键，或者模板函数；
  - `chat.json`：咨询对话页在模板里用 `util.to_json` 输出，数据来自 `dataIndex` 文案和 F4 查询。
- 如果以上业务规则不放进 CMS，就要保留一个读 CloudBase 的站点脚本，转换层不会完全删除。

## 7. travelWeb 的迁移路径

### 7.1 17 个数据文件的去处

| 文件 | 去处 |
|---|---|
| `labels.json` | 静态 `data/` 文件（以后可选 F8） |
| `order.json` | F4；首页固定列表 `homeExperiences`、`homeGuides` 放静态文件 |
| `months.json` | F4 的分组，或模板里用 `array.filter` |
| `city_map.json` | F1、F2、F3、F13；配色和短名用模板函数 |
| `attraction_map.json` | F2、F3；“同城前 2 个体验”用模板 `array.filter` |
| `route_map.json` | F1、F2、F3、F5；住宿、换城日、相册、月份文字用模板函数 |
| `experience_map.json` | F3 |
| `guide_map.json` | F2；默认图由 F9 或 CMS 写入 |
| `theme_map.json` | F3（带过滤的反链）、F4（按人群“包含”查询）；配色放静态文件 |
| `festivals.json` | F1；12 个月标记和月份文字用模板函数 |
| `images.json` | F11 |
| `site.json` | `mode: data` 单例；顾问、评价照片依赖 F11；`phoneHref` 由 CMS 写入 |
| `names.json` | F2 解析出的引用；咨询对话页要的名称表随 `chat.json` 处理 |
| `chat.json` | 模板 `util.to_json`，数据来自 F10 和 F4 |
| `copy.json` | F10 |
| `pages.json` | 静态页集合，网址依赖 F12 |
| `faq.json` | `site.modules` |

### 7.2 步骤

1. **现在**：travelWeb 用“CloudBase 读取层 + 转换层”，CMS 不等 Bukit。转换层已经从 Notion 同步脚本里拆出来，验证过逐字节一致。
2. **内容集合改为直读**：需要 F1、F2、F9、F11、F12、F13 都完成，再加上 CMS 存 Markdown 正文。之后 `content/*.md` 不再由转换层生成。
3. **关联和列表**：F3、F4、F5 完成后，去掉 `rel.*`、`order.json`、`months.json`。
4. **文案**：F10 完成（或者文案键改名）后，去掉 `copy.json`。
5. **发布和回归**：F6、F7 完成后，按 release 直接构建，并能离线回归。
6. **收尾**：业务规则移到 CMS 发布函数后，转换层可以删除；否则保留一个很薄的站点脚本（第 6 节）。

**验收**：改写后的输出不会和现在逐字节一致，例如推荐顺序、SEO 描述可能变化。每一步都要逐页核对页面数量、网址、标题和描述、关联列表、结构化数据，并用 Bukit 的发布审计和隐私检查兜底。会变化的项目（第 4 节 F5、F9）在验收里单独列出，并说明接受的理由。

## 8. 优先级和排序

内容集合改为直读（第 7.2 节第 2 步）是关键路径，排在前面：

| 顺序 | 功能点 | 价值 | 改动 | 依赖 |
|---|---|---|---|---|
| 1 | F1 结构化字段 | 高 | 小到中 | 无 |
| 2 | F2 引用字段 | 高 | 中 | 嵌套路径依赖 F1 |
| 3 | F13 部分出页面 | 高 | 中 | 无 |
| 4 | F11 媒体资产 | 高 | 中 | 无 |
| 5 | F12 permalink 字段占位符 | 高 | 中（或由 CMS 负责） | 无 |
| 6 | F9 集合级派生字段 | 高 | 中 | F2 |
| 7 | F3 反向索引 | 高 | 中 | F2 |
| 8 | F4 命名查询 | 中到高 | 中 | F3 |
| 9 | F5 related 自定义字段 | 中 | 小到中 | 无 |
| 10 | F10 `dataIndex` 键名 | 中 | 小（或站点改名） | 无 |
| 11 | F6 按发布版本读取 | 中到高 | 小 | 凭据决策 |
| 12 | F7 离线快照 | 高 | 大 | 无（就是 CloudBase P2） |
| 13 | F8 枚举标签 | 低 | 中 | 无（静态文件已够用） |

## 9. 未决问题

1. F2：引用规则扩展现有的 `customFields[].reference`，还是和 `relationMappings`、Notion 关联解析统一成一个新模型？
2. F4：查询语法的边界（是否要分组以外的聚合）。
3. F6：环境变量插值只限 CloudBase 源，还是对所有源的筛选值开放？读取未上线 release 用什么凭据？
4. F1：数字数组保留类型还是统一成字符串？嵌套值里的 `cloud://` 按什么粒度丢弃？
5. F9：派生字段放在 Bukit 配置里，还是由 CMS 发布时写库？
6. F12、F13：由 Bukit 提供，还是由 CMS 生成（网址列、两个源）？
7. 增量构建：引用和反链结果计入页面哈希，还是登记为共享集合？
8. 小程序数据改用 CloudBase 源后，`body` 字段是否受 CloudBase P3“小程序正文规则”影响？
9. G0-1（具名消费方、业务理由、验收标准）：travelWeb 作为消费方，验收标准按第 7 节补充。

## 10. 术语

- **转换层**：travelWeb 构建前运行的 Node 脚本，把原始记录转成 Bukit 的输入文件。
- **原始记录**：读取层从数据源读出、与来源无关的内容记录。
- **release**：CMS 的一次发布，包含这次上线的全部内容版本。

## 11. 修订记录

v3（2026-11-25，独立审计）：

- 新增 F11（媒体资产）、F12（permalink 字段占位符）、F13（部分出页面）；第 3 节补上页面网址、正文拼接、媒体下载三类漏项，并把常量单列为“静态文件就够”。
- F10 改正事实：247 个文案键全部不合规，除了 `.`、`-`，还有 56 个驼峰键和 2 个带 `+` 的键；补充范围怎么切分。
- 第 13 项 SEO 描述和第 15 项的矛盾：SEO 描述同样由 `SeoModelBuilder` 从页面字段读取，改为 F9 的受控函数或写进验收。
- F9 增加 `override` 模式（城市、体验的 `summary` 是用原值拼成的），改用 Scriban 写法，补充确定性和限制。
- F1 删掉“D11 兜底个人信息”的说法，补充嵌套白名单、类型一致性和 `cloud://` 粒度。
- F2 改用 `links` 键名（`references` 是保留字段），在 `customFields[].reference` 的基础上扩展，定义分组和打平两种形状。
- F3 动机改正：`array.filter` 不计入循环上限，改为以计算量、复用和 SEO 可用性为理由；补过滤、limit 和顺序定义。
- F4 补“包含”、分组、按枚举顺序排序；按页面参数的查询留给模板。
- F5 补输出形状、稳定排序、按集合配置。
- F6 示例值加引号，写明和 `BUKIT_` 覆盖机制的关系、变量缺失时报错、多条件筛选。
- F8 改为可选，改动评为“中”，避开 `site.data` 命名冲突。
- 新增第 5 节横向要求：增量构建失效、确定性、public API、隐私。
- 第 7 节给出 17 个数据文件的去处；业务规则建议移到 CMS 发布函数；修正“F1、F2、F9 完成即可直读”和“转换层可以删除”的说法。
- 第 8 节按关键路径重排优先级。
- 执行约束改正流程出处；第 1、2 节改正 `site.data`、跨源关系排期、`dataIndex` 的描述，补上静态 `data_files`、`customFields[].reference`、`BUKIT_` 覆盖、permalink、增量构建。

v2（2026-11-25，自查审计）：

- 新增 F9（集合级派生字段）、F10（`dataIndex` 键名规则）。
- F6 补充 Publishable Key 读取未上线 release 的安全取舍。
- F1 补充嵌套键的硬性剔除；F2 补充解析结果写进页面字段、嵌套路径依赖 F1。
- 改正循环上限、`relationMappings`、小程序数据依赖的描述。
