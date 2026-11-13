# Bukit 根目录布局支持合并搜索（`search.mode: merged`）提案

- 日期：2026-11-13
- 基线：`93c2686ad0d33c778cd80c61e2900630bb0f713c`
- 前置提案：`docs/analysis/bukit-core-i18n-default-language-root-proposal-2026-11-12.zh-CN.md`（第 5 节决策 4、第 9 节"后续事项"）
- 范围：`site.defaultLanguageInSubdir: false` 与 `site.search.mode: merged` 的组合；以及内置搜索 UI 的索引地址
- 本轮性质：**只分析和提案，不实施**。未修改源码、测试、配置、CI/gate；未运行 `dotnet test`。实施须作为单独的 Core 任务开展（`AGENTS.md` "Website/Core isolation"），确认决策不等于授权实施。
- 状态：**已实施**（2026-11-13）。决策按第 7 节推荐项确认；E1/F2 与本提案分别在 PR #142 的 `d3a28d0a`、`11b9a07c` 中实施，openspec change 已归档为 `openspec/changes/archive/2026-11-13-add-i18n-root-layout-merged-search/`（PR #143）。与正文不一致之处见文末“实施结果”，正文保留提案时的原文。

---

## 1. 问题

根目录布局（默认语言放在 `dist/`）目前不能和合并搜索一起用：`SiteConfigValidator.cs:104-111` 在配置校验阶段直接报错 `site.defaultLanguageInSubdir: false cannot be combined with site.search.mode: merged.`。前置提案把它列为"本版本不支持"，没有给出技术障碍。

需要站内跨语言搜索的站点（双语文档站、多语言知识库）因此只能二选一：放弃根目录布局，或者放弃合并搜索、改用 index 模式由前端自己拉多份索引。

## 2. 现状代码

### 2.1 三种模式写出的文件

| 模式 | 带前缀布局（默认） | 根目录布局 |
|---|---|---|
| split | `/en/search.json`、`/zh-CN/search.json` | `/search.json`（默认语言）、`/zh-CN/search.json` |
| index | 各语言各一份 + 根目录 `/search.index.json` | 同左，默认语言那份在 `/search.json` |
| merged | 各语言各一份 + 根目录 `/search.json`（全部语言） | **冲突**：默认语言自己的索引和合并索引都要写 `/search.json` |

### 2.2 相关代码

| 位置 | 现状 | 与本提案的关系 |
|---|---|---|
| `Bukit.Config/SiteConfigValidator.cs:104-111` | 根目录布局 + 多语言 + merged 时报 `ConfigInvalidValue` | 取消该拒绝 |
| `Bukit.Engine/PublicOutputLifecycle.cs:99-104` `SearchPlanPath` | 根语言：非 merged 时 `search.json`，merged 时 `null`；其他语言变体：总是 `search.json`；根目录计划：merged 为 `search.json`，index 为 `search.index.json` | 根语言 merged 时改为 `<lang>/search.json` |
| `Bukit.Engine/PublishProjectionContract.cs` `SearchIndexPublishProjection` → `PublishAggregateProjectionWriters.cs:139` `WriteSearchIndex` | 每个语言变体调用 `GenerateSingleSearchIndex(context.OutputDir, ...)`，固定写 `<OutputDir>/search.json`；随后 `WriteSearchUi` | 根语言 merged 时写到 `<lang>/search.json` |
| `Bukit.Engine/I18nRootSearchWriter.cs` | merged：`GenerateMergedSearchIndex(OutputDir, Results, ...)` 写根目录 `/search.json`；index：写 `/search.index.json` | 不变 |
| `Bukit.Engine/SearchIndexBuilder.cs:12-50` `GenerateMergedSearchIndex` | 把各语言条目写进一个数组；条目字段为 `id/title/url/summary/content/snippet/type/contentType/collection/reviewStatus/tags...`，**没有 `language`**（不准确，见“实施结果”） | 新增 `language` 字段（见 Q2） |
| `Bukit.Engine/SearchIndexBuilder.cs:322-345` `GenerateSearchIndexIndex` | 每种语言一条 `{language, path}`，`path` 为 `<baseUrl>/search.json` | 不变（merged 与 index 互斥） |
| `Bukit.Engine/I18nOutputLayout.cs:156` `AuditReadPaths` | 根语言 merged 时 `Search: null`（这个组合目前不可能出现） | 改为 `<lang>/search.json` |
| `Bukit.Engine/I18nRootProjectionCoordinator.cs:102` | 根目录清单里 search 的路径：index 为 `search.index.json`，否则 `search.json` | 不变 |
| `Bukit.Engine/I18nRootOutputConflictGuard.cs` | 用根目录投影计划检查默认语言 `static/` 冲突 | merged 时 `/search.json` 已在根目录计划里，自动生效 |
| `Bukit.Engine/Plugins/BuiltIn/SearchIndexPlugin.cs:34` | 内置 UI 脚本：`input.getAttribute('data-search-url') \|\| '/search.json'`；生成的 `<input>`（约第 165 行）**没有** `data-search-url` | 见第 3 节 E1 |
| `Bukit.Cli/Resources/StarterTheme/SearchTemplate.html:44` | starter 主题：`fetch('{{ base_url }}/search.json')`，取当前语言自己的索引 | 方案 B 下，默认语言页面取到合并索引（见 5.3） |

## 3. 本轮发现的现有问题

| 编号 | 级别 | 问题 | 证据 | 建议 |
|---|---|---|---|---|
| E1 | Important | 内置搜索 UI 永远请求域名根的 `/search.json`，不管当前语言和 baseUrl。后果：① GitHub Project Pages 等带 baseUrl（如 `/repo`）的站点，所有模式下都 404；② 带前缀布局 + split，根目录没有 `/search.json`，所有语言 404；③ 根目录布局 + split，各语言 UI 都读到默认语言的索引，中文页面搜出英文结果 | `SearchIndexPlugin.cs:34` 回退值写死；`WriteSearchUi` 生成的 `<input>` 不设 `data-search-url` | 与本提案独立，可单独先修：`WriteSearchUi` 写入 `data-search-url`。split / index 指向当前语言 `<baseUrl>/search.json`；merged 指向根目录 `<rootBaseUrl>/search.json` 并写入 `data-search-language` 供过滤（见 5.3；实施时改为读本语言索引，见“实施结果”） |
| F2 | Minor（疑似，未复现；实施时已复现并修复） | 根目录布局下默认语言的计划清单不含 `bukit-search.html`（`PublicOutputLifecycle.cs:75` 对根语言排除），但 `WriteSearchIndex` 仍调用 `WriteSearchUi` 写出该文件。计划和实际写出不一致，可能影响归属记录和关闭 `search.ui` 后的清理 | `PublicOutputLifecycle.cs:75`；`PublishAggregateProjectionWriters.cs:153`；`SearchIndexPlugin.cs:172` | 实施时核实：若排除是有意的（例如由别处计划），补注释；否则把根语言的 UI 纳入计划并补关闭后清理的测试 |

## 4. 方案对比

| 方案 | 做法 | 优点 | 缺点 |
|---|---|---|---|
| A | 合并索引改名为 `/search.all.json`，默认语言保留 `/search.json` | 默认语言索引地址不变 | merged 模式的文件名在两种布局下不同；主题、文档、外部工具都要分情况处理 |
| **B（推荐）** | 合并索引仍在 `/search.json`；根目录布局下默认语言自己的索引挪到 `/<lang>/search.json` | merged 模式在两种布局下 `/search.json` 都是合并索引，契约一致；与前置提案决策 3 同一思路（index 模式默认语言 sitemap 已挪到 `/<lang>/sitemap.xml`） | 默认语言页面用 `{{ base_url }}/search.json` 会取到合并索引，需要按语言过滤（5.3） |
| C | merged 时默认语言不生成自己的索引，只保留根目录合并索引（类似 merged feed） | 改动最小 | 只有默认语言缺单独索引，不对称；按语言消费索引的外部工具缺一份 |

**推荐 B。** `/<lang>/` 是默认语言自己的段，保留前缀检查放行（`I18nOutputLayout.ReservedPrefixes` 只含其他语言）；迁移跳转（PR #140）只派生 HTML 路由，不会占用 `<lang>/search.json`。

## 5. 设计要点（方案 B）

### 5.1 路径

- `I18nOutputLayout` 新增 `SearchRelativePath(language)`：根目录布局 + 根语言 + merged 时为 `<lang>/search.json`，否则为 `search.json`。写法参照 `SitemapRelativePath`。
- `SearchPlanPath`、`WriteSearchIndex`（经 `PublishProjectionContext`）、`AuditReadPaths` 都改用它，保证计划、写出、审计读取同一路径。
- 根目录合并索引路径不变：`/search.json`。

### 5.2 合并索引条目新增 `language`

- `GenerateMergedSearchIndex` 写每条记录时加 `"language": r.Language`。
- 属于新增字段：现有使用方忽略未知字段即可。带前缀布局的 merged 模式同样受益。
- split / index 的单语言索引是否也加：建议**加**，记录结构在所有模式下一致（见 Q2）。

### 5.3 搜索 UI

- 内置 UI 按 E1 的修法写入 `data-search-url`；merged 模式额外写 `data-search-language="<当前语言>"`，脚本在加载后按 `item.language` 过滤（无该属性时不过滤，兼容自定义页面）。（实施时未采用，见“实施结果”）
- `WriteSearchUi` 需要知道根目录 baseUrl：根目录布局下根语言的变体 baseUrl 就是根；其他语言变体需要从 `BuildVariantContext.RootBaseUrl` 传入。`PublishProjectionContext`（`PublishProjectionContract.cs:18`）目前只有变体 `BaseUrl`，需新增根目录 baseUrl 字段，或由 `I18nOutputLayout.Create` 按站点配置计算。
- starter 主题和第三方主题：默认语言页面 `{{ base_url }}/search.json` 在方案 B 下取到合并索引，内容多于当前语言。文档里给出按 `language` 过滤的写法；不修改主题模板语法。

### 5.4 配置校验

- 删除 `SiteConfigValidator.cs:104-111` 的组合报错；同步删除 Config 测试中期望该报错的用例，改为期望通过。
- 用户指南 `11-i18n-seo.md` 删除"merged 被拒绝"的说明。

### 5.5 Worktree 构建

- 根语言 Worker 的快照包含 `<lang>/search.json`；`I18nWorktreeProtocol.ValidateOutputPaths` 对根语言路径做保留前缀和安全路径检查，`<lang>/` 为默认语言自身代码，放行。
- 父进程写根目录合并索引的逻辑不变；需要测试证明 Worktree 与进程内产物一致。

### 5.6 冲突、增量与切换

- 默认语言 `static/search.json` 与根目录合并索引冲突：根目录投影计划已含 merged 搜索，`I18nRootOutputConflictGuard` 自动报 `i18n-root-output-conflict`。`static/<lang>/search.json` 与默认语言自己的索引冲突由现有 `AssetOutputPlan.Validate`（BKT-0604）兜底。
- split ↔ merged、带前缀 ↔ 根目录切换时，`/search.json` 的归属在默认语言和根目录之间转移；依赖现有 `CollectAndClean` 按 manifest 归属清理，需补测试。
- 增量站点哈希：`search.mode` 已参与哈希，不新增。

### 5.7 不受影响的部分

- index 模式与 `search.index.json`。
- `site.search.route` 与 SearchAction（目标为 `<site.url><baseUrl><language-prefix><route>`，与索引文件无关）。
- 迁移跳转、保留前缀、根目录文件归属规则（`IsRootOwnedAggregate` 不涉及 search）。

## 6. 影响与风险

- **新组合，无存量用户**：根目录布局 + merged 目前被拒绝，`/<lang>/search.json` 不影响任何已发布站点。
- **E1 修复改变内置 UI 行为**：带 baseUrl 的站点、带前缀 split 站点的 UI 从 404 变为可用；带前缀 merged 站点的 UI 从"显示全部语言"变为"只显示当前语言"（Q4）。需在 CHANGELOG 标为行为变化。
- **`language` 字段**：所有模式的搜索记录多一个字段，`search.json` 体积小幅增加（每条约 15–20 字节）。
- **主题适配**：默认语言页面取到合并索引，第三方主题若不过滤会显示其他语言结果。文档说明即可，不属于破坏性变更（只出现在新组合下）。
- **审计**：默认语言的发布审计改读 `<lang>/search.json`；路径错了会出现 `publish.search_missing_route` 误报，需测试覆盖。

## 7. 待用户确认的决策

| # | 决策 | 选项 | 推荐 |
|---|---|---|---|
| Q1 | 路径方案 | A / B / C（第 4 节） | B |
| Q2 | 新增 `language` 字段的范围 | a) 只加在合并索引；b) 所有模式的搜索记录都加 | b，记录结构一致，前端不必区分模式 |
| Q3 | E1（内置 UI 写死 `/search.json`）的处理 | a) 作为独立修复先做；b) 并入本提案一起实施 | a，带 baseUrl 和 split 的站点现在就受影响，不应等 D2 |
| Q4 | merged 模式下内置 UI 是否按当前语言过滤 | a) 默认过滤，不加开关；b) 新增 `site.search.uiScope: language\|all` | a；需要跨语言结果的站点可用自定义页面 |
| Q5 | F2（根语言 `bukit-search.html` 计划与写出不一致） | a) 随本提案核实并修复；b) 另开任务 | a，同一处代码 |
| Q6 | 实施方式 | 单独的 Core 任务，先写 openspec change（参照 `openspec/changes/archive/2026-11-12-add-i18n-default-language-root/`） | 是 |

## 8. 影响文件与测试

### 8.1 代码（均为 Core，需单独授权）

- `src/Bukit-Core/Bukit.Config/SiteConfigValidator.cs`
- `src/Bukit-Core/Bukit.Engine/I18nOutputLayout.cs`
- `src/Bukit-Core/Bukit.Engine/PublicOutputLifecycle.cs`
- `src/Bukit-Core/Bukit.Engine/PublishAggregateProjectionWriters.cs`
- `src/Bukit-Core/Bukit.Engine/SearchIndexBuilder.cs`
- `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/SearchIndexPlugin.cs`（E1、Q4）
- 视 F2 核实结果：`PublicOutputLifecycle.cs:75`

### 8.2 测试

| # | 文件 | 内容 |
|---|---|---|
| T1 | `tests/Bukit.Config.Tests/`（`ConfigPhase2Validator` 相关） | 根目录布局 + merged 通过校验；原拒绝用例改为通过 |
| T2 | `tests/Bukit.Engine.Tests/I18nRootLayoutTests.cs` | 根目录布局 + merged：`/search.json` 含全部语言、条目带 `language`；默认语言索引在 `/<lang>/search.json`；其他语言索引位置不变 |
| T3 | `tests/Bukit.Engine.Tests/I18nMergedSearchIndexTests.cs` | 合并索引条目带 `language`；`maxContentLength` 截断不变 |
| T4 | 同 T2 | 发布审计读取默认语言 `<lang>/search.json`，不出现 `publish.search_missing_route` 误报 |
| T5 | 同 T2 | split ↔ merged、带前缀 ↔ 根目录切换后无残留，归属正确 |
| T6 | 同 T2 | 默认语言 `static/search.json` 报 `i18n-root-output-conflict` |
| T7 | `tests/Bukit.Cli.Tests/WorktreeBuildCoordinatorTests.cs`、`tests/Bukit.Engine.Tests/I18nWorktreeProtocolTests.cs` | Worktree 与进程内产物一致；父进程放行 `<lang>/search.json` |
| T8 | `tests/Bukit.Engine.Tests/`（`SearchIndexPlugin` 相关） | E1：`data-search-url` 带 baseUrl 和语言；merged 时带 `data-search-language`；无属性时回退行为不变 |
| T9 | 同 T8 | Q4：merged 下 UI 只显示当前语言结果（脚本断言或生成内容断言） |
| T10 | 同 T2 | F2：根语言 `search.ui` 开启时 `bukit-search.html` 被计划和归属；关闭后清理 |

### 8.3 文档

- `guide/user/11-i18n-seo.md`、`guide/user/10-built-in-outputs.md`、`guide/user/04-site-yaml-config.md`（如需）
- `guide/skills/bukit-i18n/SKILL.md`
- openspec：`i18n-output-layout`（路径）、`i18n-worktree-build`（快照路径）
- `CHANGELOG.md`、`CHANGELOG.zh-CN.md`、`CHANGELOG.ms.md`（E1 标为行为变化）
- 前置提案第 9 节：实施后把该后续事项标为已解决

## 实施结果（2026-11-13）

| 正文位置 | 提案 | 实施 |
|---|---|---|
| 2.2 `GenerateMergedSearchIndex` | 条目没有 `language` | 不准确：页面记录本来就写 `language`（无语言时为 `und`），只有列表路由在没有带语言的条目时缺省。实施后所有记录都带非空 `language`：自身没有或为 `und` 时，多语言构建取写出它的语言，单语言站点为 `und` |
| 3 E1、5.3 | merged 模式 UI 指向根目录合并索引，并写 `data-search-language` 由脚本按语言过滤 | 每种语言的片段都读本语言索引 `<语言 base URL>/search.json`（根目录布局 merged 下默认语言为 `/<lang>/search.json`），不新增 `data-search-language`。效果同 Q4（只显示当前语言），但不依赖脚本过滤；需要跨语言结果时把片段标记复制到模板并修改 `data-search-url` |
| 3 F2 | 疑似，未复现 | 用失败测试复现：根语言的 `bukit-search.html` 未进入归属，关闭 `search.ui` 后残留。已改为纳入计划与归属 |
| 8.2 T8、T9 | 断言 `data-search-language` | 改为断言各布局、模式、baseUrl 下的 `data-search-url`，以及加载失败时的提示 |
| 补充 | — | 索引加载失败或内容不是数组时片段显示 "Search is unavailable"；加载期间输入的查询在加载完成后执行；聚合投影输出路径统一为 `/` |

遗留（Minor，已在用户指南说明）：Starter 主题 `SearchTemplate.html` 请求 `{{ base_url }}/search.json` 且不按语言过滤，根目录布局 merged 下默认语言的搜索页会显示所有语言。已修复（2026-11-13）：该模板只保留 `language` 与 `site.language` 相同（或为 `und`）的记录。

## 修订记录

| 日期 | 内容 |
|---|---|
| 2026-11-13 | 初稿 |
| 2026-11-13 | 标为已实施，补充“实施结果”并在正文标注不一致之处 |
| 2026-11-13 | 遗留 Minor 已修复：Starter 主题搜索页按 `site.language` 过滤记录 |
