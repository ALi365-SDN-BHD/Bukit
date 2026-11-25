# Bukit 通用数据能力规划：让站点不再需要自建转换层

> **状态：** 规划草案 v2（2026-11-25），没有立项。下文的配置和模板接口都是草案，立项时再定。v2 按自查审计修订，修订记录见第 9 节。
>
> **执行约束：** 这里的每个功能点都改 Core（`src/Bukit-Core/`），按 `AGENTS.md` 的 Website/Core 隔离规则，每一项都要作为单独的、用户确认过的 Core 任务立项，走 openspec、Native AOT、public-api-drift、三语文档和 CHANGELOG、BukitJalil 契约这些流程。本文只做规划，不授权实施。
>
> **依据：**
> - Bukit 代码基线：`main` 提交 `bc660d1d`。CloudBase 内容源见 [实施方案](2026-11-23-cloudbase-content-source-implementation-design.zh-CN.md)（下文简称"CloudBase 方案"）。
> - 消费方：travelWeb（马来西亚定制旅行站）。它的转换层在 travelWeb 仓库 `poc-bukit/transform/`（分支 `refactor/split-sync`），模板在 `poc-bukit/themes/nanyang/layouts/`。

## 1. 背景

travelWeb 已经决定：所有内容放在 CloudBase（PG 模式），前台页面和 CMS 共用这一个数据源。

公开站现在的构建方式是：

```
内容源 → 读取层 → 原始记录 → 转换层（Node） → data/*.json + content/*.md → bukit build
```

转换层生成 17 个 `data/*.json`（模板通过 `site.data_files` 读取）和每页一个 Markdown。主题的 27 个模板文件（其中 22 个页面模板）几乎全部依赖这些预先算好的数据，`helpers.html` 里写明了原因：模板里只做取值，避免触发 Scriban 的累计循环上限。

Bukit 的 CloudBase 内容源（还没发布，在 `[Unreleased]` 下）只能把一张表读成一组页面，不能取代转换层：

- 字段值只保留字符串、数字、布尔、字符串数组。JSON 对象会被丢掉，数组里不是字符串的元素也会被丢掉（`CloudBaseFieldProjector.TryConvert`）。
- 不支持关联字段，也不支持跨源关系（CloudBase 方案 1.2 节，排在 P3）。
- 不能生成 `site.data_files`。`mode: data` 的源进入的是 `site.modules` 和 `site.data_index`。
- 没有缓存和离线模式（排在 P2）。

本文梳理转换层做的事，判断哪些可以做成 Bukit 的通用功能，哪些只能留在站点里。目标是让关系多、派生数据多的站点也能直接从数据源构建。

## 2. Bukit 现有的相关能力

| 能力 | 现状 | 和本文的关系 |
|---|---|---|
| `site.related` | 按共同取值给页面打分，结果在 `site.related_pages[id]`。索引只支持 `tags`、`categories`、`keywords`、`collection`、`type`、`date` | 相似内容推荐，但不能按自定义字段打分 |
| `relationMappings` | 把原始字段映射成标准的 `related_to` 关联，可选 `targetType`、`targetField`、`targetIdField`；按文档用于内容模型、结构化数据和审计 | 已有“引用”的模型雏形；模板文档里没有按引用取目标页面的用法，也没有反查 |
| Notion 源的关联解析 | 支持关联解析和跨源关联查找 | CloudBase 源没有对应能力 |
| `mode: data` + `dataIndex` | 按“范围 + 键”取标量值，值类型支持 `object`、`object[]`；源名、字段名、范围和键都要符合 `^[a-z][a-z0-9_]*$` | 适合页面文案、站点设置；但 travelWeb 的文案键带 `.` 和 `-`（如 `cta.default.title`、`custom.days.4-5.label`），不符合键名规则，见 F10 |
| `site.modules.<type>` | `mode: data` 的文档列表 | 适合 FAQ、顾问、评价、图库这类不出页面的数据 |
| `build.templateLoopLimit` | 默认 1000，可以调高 | 只是放宽限制，计算量还在模板里 |
| 模板函数 | Scriban 内置的 `array`、`string`、`date`、`math`，加上 `util.to_json` | 单页的小计算可以写在模板里 |

## 3. 转换层做的事和差距

| # | 转换层做的事 | travelWeb 的例子 | 归类 |
|---|---|---|---|
| 1 | 对象字段、对象数组、数字数组 | 路线每日行程（标题、说明、住宿、餐食、景点、体验）、攻略分段、推荐月份 `[3,4,5]`、联系方式对象 | 通用：F1 |
| 2 | 正向引用，按数组顺序 | 路线 → 途经城市；每日行程 → 景点、体验；内容 → 图片键 | 通用：F2 |
| 3 | 反向关联，包括嵌套路径 | 城市页的“经过这里的路线”；景点页的“包含这个景点的路线”（来自 `days[].attractions`） | 通用：F3 |
| 4 | 有序列表、筛选、去重、取前几条 | 有页面的城市；有路线的城市；出现过的景点类型；精选路线在前取 6 条；每月推荐路线前 3 条 | 通用：F4 |
| 5 | 按自定义字段的相似推荐 | 相似路线：共同城市数 + 共同人群数，取前 3 | 通用：F5（扩展 `site.related`） |
| 6 | 一次构建读到同一个发布版本 | CMS 按 release 发布，构建要读同一个 release 的全部内容 | 通用：F6 |
| 7 | 离线、可复现的构建 | 同一份内容重复构建，逐字节比较回归 | 通用：F7（CloudBase P2） |
| 8 | 枚举的取值和显示标签 | 人群、区域、景点类型、体验分类、攻略分类的中文标签（`labels.json`） | 通用：F8 |
| 9 | 键值文案、站点设置单例 | 247 条页面文案（模板里写 `C["key"]`）、品牌和联系方式 | 已有 `dataIndex`，但键名规则不兼容：F10 |
| 10 | 图库和署名 | 图片键 → 文件名、作者、许可；署名页 | 已有 `site.modules`，加上 F2 按键引用 |
| 11 | 小程序数据从 CloudBase 生成 | `miniprogram-data/`，路线详情要 `days`（`object[]`） | 依赖 F1；小程序的正文规则已排在 CloudBase P3 |
| 15 | 页面 front matter 里的派生字段 | `seo_title`（如“{城市名}定制旅游攻略”）、城市页 `summary` 的拼接句式、`image`（图片键 → `/assets/img/<文件>.webp`）、`image_alt`、固定的 `publishAt` | 通用：F9；Bukit 的 SEO、OG 分享图和结构化数据读的是这些字段，不能只在模板里算 |
| 12 | 单页的小计算 | 住宿分段和晚数；换城日（标题含“→”）；相册取前 5 张存在的图片；“3–5月”这类月份文字（跨年写“11月–次年2月”） | 站点：模板函数 |
| 13 | SEO 描述的补句和按显示宽度截断 | 太短时补一句，超过约 140 个字符宽度在句号处截断 | 站点：模板，或者以后作为 SEO 选项再评估 |
| 14 | 业务规则 | 示例电话不生成 `tel:` 链接；文案里有停用号码就中止构建；路线默认图按 slug 散列挑选；城市配色轮换；咨询对话的选项组合（`chat.json`） | 站点：模板或站点脚本 |

## 4. 功能点

### F1 CloudBase 源保留结构化字段

- **问题**：`CloudBaseFieldProjector` 丢掉 JSON 对象和非字符串数组元素。Markdown 源的 front matter 本来就支持嵌套值，两种源的能力不一致。
- **做法（草案）**：jsonb 对象和数组按原结构投影成内容字段；数字、布尔数组保留元素类型。白名单只看顶层字段名；硬性剔除的敏感名（`user_id`、`owner` 等）要同样作用到嵌套对象的键，否则 jsonb 里的个人信息会绕过 D7；值里出现 `cloud://`、签名链接时按 D17 处理，隐私检查（D11）兜底。
- **通用性**：高。凡是用 CloudBase 存结构化内容的站点都用得上。
- **改动范围**：小。主要是 `CloudBaseFieldProjector`、内容字段类型、测试夹具。
- **要先定的**：嵌套深度和大小上限；嵌套值里的图片对象名是否换链（建议 P1 不换，只允许 `fileFields` 顶层）。

### F2 引用字段

- **问题**：模板只能拿到 slug 数组，要自己去别的集合里找目标页面。
- **做法（草案）**：在源或集合上声明引用字段，构建时解析成目标文档，保持数组顺序：

  ```yaml
  content:
    references:
      - collection: route
        field: cities              # 也支持嵌套路径：days[].attractions
        target: { collection: city, key: slug }
      - collection: route
        field: image_key
        target: { source: media, key: key }   # 引用 mode: data 的源
  ```

  模板里用 `page.refs.cities` 得到有序的目标列表，每项有 `title`、`url`、`fields`。引用解析的结果还要能写进页面字段（例如图片键解析成文件路径写进 `image`），这样 SEO、OG 分享图和搜索索引也能用到，不只是模板。
- **通用性**：高。和现有的 `relationMappings`、Notion 关联解析是同一类需求，可以统一成一个模型。
- **改动范围**：中。要在内容加载完成后、渲染之前统一解析；目标不存在时按 `schemaFailMode` 报错或警告（现有 `requireRelationTargets` 可以复用）。
- **依赖**：CloudBase 源上的嵌套路径（如 `days[].attractions`）依赖 F1。
- **注意**：解析在 Core 里一次完成，模板只取值，不会碰到循环上限。

### F3 反向索引

- **问题**：城市页要列出“经过这里的路线”，景点页要列出“包含这个景点的路线”（要遍历每条路线的每一天）。单个详情页反查还不算多，但列表页要为每一项都反查：43 个景点 × 8 条路线 × 每条约 6 天，在同一个外层循环里就会超过默认的 1000 次上限（Scriban 的上限按一次渲染里最外层循环累计），计算量也随内容数量平方增长。
- **做法（草案）**：基于 F2 的引用声明，自动生成反向索引：

  ```yaml
  content:
    references:
      - collection: route
        field: days[].attractions
        target: { collection: attraction, key: slug }
        backref: routes            # 在景点上生成 page.backrefs.routes
  ```

  反向列表默认按引用方集合的顺序排列，可以配置排序字段；同一个引用方只出现一次。
- **通用性**：高。“反链”“相关文章”“作者的全部文章”都是这一类。
- **改动范围**：中。依赖 F2。

### F4 命名查询

- **问题**：页面要的各种有序列表（有页面的城市、有路线的城市、出现过的类型、精选在前取 N 条）现在都由转换层生成在 `order.json` 里，模板里有 44 处用到。
- **做法（草案）**：在 `site.yaml` 里声明查询，结果放进 `site.queries.<name>`：

  ```yaml
  site:
    queries:
      route_cities:
        from: city
        where: [{ field: has_page, eq: true }, { has_backref: routes }]
      attraction_types:
        from: attraction
        distinct: type
      featured_routes:
        from: route
        sort: [{ field: featured, direction: desc }]   # 稳定排序，保持原有顺序
        limit: 6
  ```

- **通用性**：高。其他静态站点生成器也有类似的 where、first、group 能力。
- **改动范围**：中。要定一个小而封闭的查询语法（筛选、排序、去重、取前几条、按反向索引是否为空），不做通用表达式。

### F5 `site.related` 支持自定义字段

- **问题**：相似路线要按“共同城市数 + 共同人群数”打分，`site.related` 的索引只支持内置的几种字段。
- **做法（草案）**：`indices` 允许写 `field: <字段名>`，按共同元素个数乘权重计分；可以限定只在同一集合内比较；同分时的排序规则写进文档（例如按集合顺序）。
- **通用性**：高。
- **改动范围**：小到中。
- **注意**：同分时的排序和 travelWeb 现在的写法不一定相同，切换时页面上的推荐顺序可能变化，要在验收里单独核对。

### F6 按发布版本读取

- **问题**：CMS 按 release 发布。构建要读同一个 release 的全部内容，否则构建途中有人发布，就会读到新旧混合的内容。
- **做法（草案）**：两种都提供，由站点选择：
  1. 筛选值可以引用环境变量，例如 `filter: { field: release_id, value: ${env:RELEASE_ID} }`，CI 触发构建时传入；
  2. 文档里给出“只看当前线上版本”的视图写法（`security_invoker` 加 RLS），并说明它在构建途中发布时的取舍。
- **安全取舍**：Bukit 只用 Publishable Key（`anon`）读取，而这个密钥不算秘密。做法 1 要求 `anon` 能读到“正在构建、还没上线”的 release，意味着内容在构建成功之前就能被公开读到；构建失败的 release 也会暴露。可选的处理：(a) 接受这个窗口，并在文档写明；(b) 构建时用权限更高、只给 CI 的凭据读取，这要扩展 CloudBase 方案 D5 的凭据策略（现在只实现了 Publishable Key），并重新评估 G0-4。立项时要先定这一条。
- **通用性**：中到高。凡是有草稿和发布流程的数据源都用得上。
- **改动范围**：小（不含凭据策略的扩展）。环境变量只能用在筛选值里，不能出现在 `envId`、`table` 这类会拼进地址的字段（沿用 CloudBase 方案 D18 的白名单校验）。

### F7 CloudBase 离线快照

- **问题**：没有离线模式，就不能用同一份数据重复构建做回归，CI 每次构建都要联网。
- **做法**：就是 CloudBase 方案第 13 节的 P2（缓存、离线快照、`BuildTransaction` 登记资源）。快照里只存投影后的公开字段。
- **通用性**：高。
- **改动范围**：大（Notion 的同类机制约 1.5k 行）。

### F8 枚举标签来自模型

- **问题**：枚举的显示标签现在写在站点代码里（`labels.json`），和 CMS 的字段定义各存一份。
- **做法（草案）**：`content.modelSchema` 的字段 `enum` 支持“取值 + 标签”，构建时放进 `site.data.labels.<collection>.<field>`；或者从一张 `mode: data` 的标签表读取。
- **通用性**：中。
- **改动范围**：小。

### F9 集合级派生字段

- **问题**：页面的 SEO 标题、摘要句式、分享图、替代文字现在由转换层写进每页的 front matter。改为直读数据源后，这些值要么存进数据库（每条内容都要维护），要么由 Bukit 在加载时生成。Bukit 的 SEO 模型、OG 分享图、JSON-LD 和搜索索引读的是页面字段，所以只在模板里拼字符串不够。
- **做法（草案）**：集合上声明字段的默认值模板，内容没填这个字段时，加载阶段按模板生成：

  ```yaml
  content:
    derivedFields:
      city:
        seo_title: "{{ name }}定制旅游攻略"
        image: "{{ refs.image_key.fields.file | prepend '/assets/img/' | append '.webp' }}"
        image_alt: "{{ name }} {{ name_en }}"
  ```

  模板只能引用本条内容的字段和 F2 解析出的引用，不能遍历其他集合（避免把转换层原样搬进配置）。
- **备选**：由 CMS 在发布时把这些字段算好写进库里。这样 Bukit 不用改，但每个站点的 CMS 都要自己实现。
- **通用性**：中到高。按集合给 SEO 字段设默认值是常见需求。
- **改动范围**：中。要决定表达式能力的边界，以及和现有 `canonicalMappings` 的关系。
- **注意**：travelWeb 的描述截断规则（第 3 节第 13 项）不在 F9 范围内。

### F10 `dataIndex` 键名规则

- **问题**：`dataIndex` 的键必须符合 `^[a-z][a-z0-9_]*$`。travelWeb 的 247 个文案键用 `.` 分层、有些带 `-`（`custom.days.4-5.label`），直接放不进去。
- **做法**：二选一：(a) 键名规则放宽到允许 `.` 和 `-`，并在模板里按完整键取值；(b) 不改 Bukit，travelWeb 把键改成符合规则的写法（把 `.` 拆成范围加键，把 `-` 换成 `_`），同时改模板和 `audit-copy` 的检查。
- **通用性**：(a) 通用，`i18n` 文案常用点号分层的键。
- **改动范围**：(a) 小，但要确认键名规则是否被别的地方（路由元数据、BukitJalil）依赖。

## 5. 不做成通用功能的部分

第 3 节第 12～14 项是 travelWeb 自己的业务规则，放进 Core 会让 Core 背上站点逻辑。去处：

- **单页的小计算**（住宿分段、换城日、相册、月份文字）：写成主题里的模板函数。每页只处理自己的数据，循环次数很小。
- **SEO 描述的补句和截断**：先留在模板里。等有第二个站点需要同样的规则，再评估做成 `site.seo` 的选项。
- **校验类规则**（停用号码就中止构建）和组合数据（`chat.json`）：留一个很薄的站点脚本，或者在模板里用 `util.to_json` 输出。

## 6. travelWeb 的迁移路径

1. **现在**：travelWeb 用“CloudBase 读取层 + 转换层”，CMS 不等 Bukit。转换层已经从 Notion 同步脚本里拆出来，并验证过逐字节一致。
2. **Bukit 每完成一项功能**，travelWeb 删掉转换层里对应的部分，模板跟着改：
   - F1、F2、F9 完成：内容集合可以改由 Bukit 的 CloudBase 源直读，`content/*.md` 不再需要生成（只有 F1 不够：front matter 里的 SEO 字段和分享图要靠 F2、F9 生成）；
   - F10 完成（或文案键改名）：页面文案改由 `dataIndex` 读取，去掉 `copy.json`；
   - F2、F3 完成：去掉 `rel.*` 和名称索引；
   - F4、F5 完成：去掉 `order.json`、`months.json` 和相似路线；
   - F6、F7 完成：发布流程改为 Bukit 直接按 release 构建，并能离线回归；
   - F8 完成：去掉 `labels.json`。
3. **最后**只剩业务规则（第 5 节），转换层可以删除。

**验收**：改写后的输出不会和现在逐字节一致（推荐顺序、SEO 描述可能变化）。每一步都要逐页核对：页面数量、网址、标题和描述、关联列表、结构化数据，并用 Bukit 的发布审计和隐私检查兜底。

## 7. 优先级和排序

| 顺序 | 功能点 | 价值 | 改动 | 依赖 |
|---|---|---|---|---|
| 1 | F1 结构化字段 | 高 | 小 | 无 |
| 2 | F2 引用字段 | 高 | 中 | 嵌套路径依赖 F1 |
| 3 | F3 反向索引 | 高 | 中 | F2 |
| 4 | F6 按发布版本读取 | 中到高 | 小 | 无 |
| 5 | F4 命名查询 | 高 | 中 | F3（按反向索引筛选） |
| 6 | F5 related 自定义字段 | 中 | 小到中 | 无 |
| 7 | F9 集合级派生字段 | 中到高 | 中 | F2 |
| 8 | F10 `dataIndex` 键名规则 | 中 | 小 | 无 |
| 9 | F8 枚举标签 | 中 | 小 | 无 |
| 10 | F7 离线快照 | 高 | 大 | 无（就是 CloudBase P2） |

F1 先做；F2、F3、F4、F9 是取代转换层的关键。F6 的凭据取舍要在立项时先定。

## 8. 未决问题

1. 引用和反向索引放在 `content.references` 统一声明，还是扩展现有的 `relationMappings`？要考虑和 Notion 关联解析统一。
2. 命名查询的语法边界：只做筛选、排序、去重、取前几条，还是允许分组？
3. F6 的环境变量插值是否只限 CloudBase 源，还是对所有源的筛选值开放？
4. 结构化字段进入搜索索引、小程序数据和 `content/*.json` 时的形状。
5. G0-1（具名消费方、业务理由、验收标准）：travelWeb 作为消费方，验收标准按第 6 节补充。
6. F6：构建读取未上线 release 的凭据方式（接受公开窗口，还是扩展凭据策略）。
7. F9：派生字段放在 Bukit 配置里，还是由 CMS 发布时写库。

## 9. 修订记录

v2（2026-11-25，自查审计）：

- 新增 F9（集合级派生字段）：v1 的迁移路径写“F1 完成就能直读”，漏掉了 front matter 里的 SEO 标题、摘要句式、分享图，这些由 Bukit 的 SEO 和搜索流程读取，不能只在模板里算。
- 新增 F10：核对后发现 `dataIndex` 的键名规则 `^[a-z][a-z0-9_]*$` 不接受 travelWeb 现有的文案键；v1 写成“已有能力”。
- F6 补充安全取舍：用 Publishable Key 读取未上线的 release，会让内容在构建成功前被公开读到。
- F1 补充：硬性剔除的敏感名要作用到 jsonb 嵌套键。
- F2 补充：引用解析结果要能写进页面字段；嵌套路径依赖 F1。
- F3 改正循环上限的说法：上限按一次渲染里最外层循环累计，问题出在列表页逐项反查。
- 第 2 节 `relationMappings` 的描述改为按文档能确认的范围；第 3 节第 11 项改正：CloudBase P3 排的是小程序的正文规则，小程序数据本身依赖 F1。
