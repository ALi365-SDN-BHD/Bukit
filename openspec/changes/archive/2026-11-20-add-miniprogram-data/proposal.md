# Add versioned mini-program data output

## Why

团队要给站点配一个小程序，内容和网站共用同一份 Notion 数据，编辑在 Notion 里修改。
Bukit 只负责输出小程序要用的数据，**不生成小程序项目**；小程序的页面、组件和渲染由
使用方自己写。

现有输出不能直接给小程序用：

- 每篇内容的 `content/<route>.json` 字段很全，但 `agent-manifest.json` 只有 id、route
  和各表示形式的链接，没有标题、摘要、封面、日期，做不了列表页。
- `mode: data` 的数据（数据模块、`dataIndex` 键值）不产生路由，也就**没有任何公开的
  JSON 输出**，只能在模板里通过 `site.modules`、`site.data`、`site.data_index` 使用。
  导航只来自 `site.yaml` 的 `menus`（`MenuPlugin`）。
- 正文是站点用的 HTML（带 Notion class、相对链接、懒加载属性、iframe），小程序的
  `rich-text` 渲染不了。
- 图片可能是相对路径，或者是会过期的 Notion 签名 URL。小程序要求 HTTPS 绝对地址，
  而且域名必须在合法域名列表里。
- 没有版本信息。小程序没法判断哪些数据变了，只能每次全量拉取。

## What Changes

- 新增一份可选开启的**小程序数据输出**：在 `site.yaml` 里用白名单声明数据集，构建时
  每个数据集输出一份确定的 JSON 文件到该语言输出根目录下的 `mp/`（例如
  `dist/mp/`、`dist/en/mp/`）。
- 支持五类数据，全部可以放在 Notion 里维护：
  1. 内容（collection）：列表索引，加上每篇一个详情文件；
  2. 数据模块（`mode: data` 按 `type` 分组），例如轮播、公告、FAQ、导航；
  3. 键值数据（`dataIndex`），例如站点设置、文案、开关；
  4. 记录集（一个 `mode: data` 的 Notion 数据库原样输出成记录数组），取代 `data/*.yaml`；
  5. 分类（从内容的 tags/categories 生成）、站点信息和导航。导航可以来自 Notion 数据
     模块，也可以来自 `site.yaml` 的 `menus`，由站点自己选择。
- **版本目录**：每种语言一份 `<语言输出根目录>/mp/catalog.json`，带一个总哈希和每个数据集的哈希；
  内容数据集还有二级目录，里面是每篇详情的哈希。版本号是输出字节的 SHA-256，数据文件
  按哈希命名，可以永久缓存。小程序启动时只请求目录，只拉取哈希变了的数据。
- 输出是确定的：只输出声明过的字段，排序固定，固定 LF、UTF-8 无 BOM，不写当前时间。
  内容没变就重新构建，哈希也不变。
- 正文转成小程序可用的 HTML 子集；图片和链接一律用 `site.url` 拼成 HTTPS 绝对地址；
  站内链接附带目标的 id，方便小程序映射到自己的页面。
- 构建时校验：数据集引用的源不存在、字段缺失或类型不对、图片仍是 Notion 签名 URL、
  没有配置 HTTPS 的 `site.url` 时，构建失败，并指出是哪个数据集的哪条记录。
- 文档：Notion 数据建模指南、数据集配置参考、小程序端的同步协议（非规范性的参考实现
  说明），以及"Notion 修改后如何触发重建"的部署说明。

## Capabilities

### New Capabilities

- `miniprogram-data`：小程序数据集的声明、输出、版本目录和构建时校验。

### Modified Capabilities

无。已有的 `content/*.json`、`agent-manifest.json`、`search.json` 不变。

## 实现位置（已确认，见 design.md 决定 1）

**做成 Core 内置插件**，而不是像 WechatSync 那样的外部插件。原因是外部插件只能读
构建产物，而 ②③④ 和导航这几类数据在构建产物里根本不存在。要让外部插件拿到这些数据，
要么它自己再调一遍 Notion（重复实现 provider、token 和图片本地化），要么 Core 新增一份
公开的 data 输出（会把原本不公开的数据模块发布出去）。两条路都比内置插件差。

按 AGENTS.md 的 Website/Core 隔离规则，内置插件属于 Core 变更，需要用户明确确认后，
作为单独的 Core 任务实施。

## 跨仓影响

- **BukitJalil**：如果它会校验或编辑 `site.yaml`，需要认识新的 `miniprogram` 配置节
  （`schemas/` 下的 JSON Schema 由 Core 生成）。只有它升级内置 Bukit 版本后才生效。
- **使用方的小程序仓库**：按文档里的同步协议实现客户端。不在本仓库内。
- **部署**：小程序的合法域名必须是 ICP 备案过的 HTTPS 域名，`*.github.io` 基本不可用。
  这是部署前提，Bukit 不处理。

## 批准范围

已确认（2026-11-19）：

1. 五类数据全部支持。
2. 不需要接入 Bukit 以外的新数据源。
3. 数据集用 `site.yaml` 白名单声明。
4. 数据以 Notion 为主要存放位置，方便编辑修改。
5. 所有数据都带版本信息；小程序启动时只请求版本目录，与本地版本比对，只拉取有更新的数据。

设计决定（2026-11-19 逐项确认，详见 design.md）：

1. 实现位置：Core 内置插件，默认关闭。
2. 版本目录分两层：一级目录列出数据集哈希，内容详情放在二级目录。
3. 数据文件按内容哈希命名；只有 `catalog.json` 用固定文件名。
4. 列表不做服务端分页：每个集合一个不含正文的列表索引，由小程序本地分页。
5. 正文输出清洗后的 HTML 子集。
6. 只承诺微信小程序。
7. 导航两种来源都支持：Notion 数据模块（`kind: module`），以及 `site.yaml` 的
   `menus`（`kind: menu`），由站点自己选择。

仍需单独授权：按 AGENTS.md 的 Website/Core 隔离规则，以单独的 Core 任务实施
（tasks.md 0.2）。授权之前本变更只是提案，不进入实施。

## 非目标

- 不生成小程序项目、页面、组件或 SDK；不提供小程序端代码包。
- 不提供运行时 API 或数据库，输出只是部署在站点下的静态文件。
- 不写入 Notion，也不配置 Notion 自动化或 webhook。
- 不改变 `bukit deploy`、CI 或发布流程；不负责定时重建。
- 不改变网站现有的 HTML、feed、sitemap、搜索和 agent 输出。
- 不做小程序端的完整性校验（SHA-256 只用作版本号）。
