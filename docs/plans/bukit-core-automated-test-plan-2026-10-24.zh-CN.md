# Bukit Core 自动化测试计划书

> 日期：2026-10-24
> 状态：草案，第 9 节待决事项已全部决定
> 范围：`src/Bukit-Core`（14 个项目）；`src/Bukit-Plugins` 与 `src/Bukit-Labs` 后置，不在本期
> 前提：按"从零开始"编制，不依赖仓库中已有测试的数量和内容

## 1. 目的与背景

Bukit 是 .NET 10 Native AOT 静态站点引擎，当前处于企业内部优先的稳定运行模式（见 `docs/governance/bukit-core-product-positioning.md`）。核心承诺是**确定性的可信内容发布**：同一输入得到同一输出，失败不破坏已发布产物，输出符合声明的 schema。

本计划书定义：测试什么、按什么顺序测、怎样判断完成，以及哪些行为需要先由负责人拍板才能写测试。

## 2. 测试对象

| 层 | 项目 | 代码规模（约） |
|---|---|---|
| 基础 | Bukit.Shared | 1.7k 行 |
| 配置 | Bukit.Config | 6.8k 行 |
| 领域 | Bukit.Engine.Abstractions、Bukit.Content、Bukit.Content.Notion、Bukit.Notion | 13k 行 |
| 路由、主题、渲染 | Bukit.Routing、Bukit.Theme、Bukit.Rendering | 6.9k 行 |
| 引擎 | Bukit.Engine | 41.8k 行 |
| CLI | Bukit.Cli、Bukit.Cli.Shared | 17.1k 行 |
| 插件宿主 | Bukit.Plugin.Abstractions、Bukit.PluginHost | 4.2k 行 |
| 外部插件 | Bukit-Plugins（IndexNow、WechatSync、Import、Clone 等） | 约 17k 行 |
| Labs | Bukit.Labs.Cli | 10.4k 行 |

不在范围内：BukitJalil、远程主题源与主题市场（Core 明确不支持）、第三方外部插件。

## 3. 质量目标

| 目标 | 度量 |
|---|---|
| 确定性 | 同一输入连续构建两次，输出目录字节一致 |
| 失败安全 | 提交前任一阶段失败，正式输出与托管缓存保持不变 |
| 增量正确 | 增量构建与全量构建输出一致 |
| 路径安全 | 任何内容、配置、别名输入不能让写入落到输出根之外 |
| 契约稳定 | 所有报告通过对应 `docs/schemas/*.v1.schema.json` 校验 |
| 覆盖率 | 整体行覆盖 ≥ 84%，单项目 ≥ 70%（沿用 `docs/coverage-baselines.json`）；Engine 安全层（`BuildTransaction`、`OutputDirectoryCleaner`、`AssetOutputPlan`）≥ 95% |

## 4. 测试策略

### 4.1 层次与比例（粗估）

| 类型 | 占比 | 说明 |
|---|---|---|
| 单元 | 约 65% | 纯函数与单类，无文件系统副作用或只用临时目录 |
| 契约 | 约 15% | JSON schema、序列化、CLI 选项与文档一致性 |
| 集成 | 约 15% | fixture 站点完整构建、假 Notion 服务、假外部插件进程 |
| 端到端与架构 | 约 5% | golden 输出树、AOT 发布冒烟、依赖矩阵 |

### 4.2 工具

沿用项目已有栈：xunit、coverlet、NetArchTest.Rules、BenchmarkDotNet；一个源项目对应一个 `tests/Bukit.<Name>.Tests` 项目。属性式随机测试（slug、路径规范化）可用 xunit 的 `Theory` 加种子化生成器实现，不新增依赖。

### 4.3 约定

- 每个测试使用独立临时目录，结束后清理；不读写仓库工作区。
- 时间、网络、环境变量、进程启动均通过可替换接缝注入。
- 用 `Trait` 分类：`Unit`、`Contract`、`Integration`、`Slow`、`KnownDefect`。
- 已确认缺陷的测试写期望的正确行为，标 `KnownDefect` 并在修复前排除，修复后摘除标记。

## 5. 阶段计划

阶段按依赖自底向上，同一阶段内按风险排序。每阶段都有出口标准，达标才进入下一阶段。

### 阶段 0：测试基础设施

产物：
- 临时站点沙箱（含符号链接构造）。
- 站点构造器：代码生成 `site.yaml`、Markdown、主题目录。
- 三个最小 fixture 站点：单语 Markdown、多语、含分类法与分页。
- 假 Notion 服务：本地 `HttpListener`，支持分页、429、5xx。
- 假外部插件进程：可配置为正常、超时、崩溃、乱码输出、超大输出。
- 断言助手：目录快照比较、JSON schema 校验、字节级 golden 比较。

出口：空站点在沙箱中构建成功并通过快照比较。

### 阶段 1：Bukit.Shared

- `SafeUrl`、`UrlRedactor`、`SsrfGuard`：内网、环回、重定向、IPv6、凭据脱敏。
- `SlugHelper`、`PathUtils`、`PlatformPathHelper`：中文、符号、大小写、保留名。
- `SafeFileEnumerator`：符号链接、reparse point、深层目录。
- `ShortcodeProcessor`、`ScribanLayoutDirectiveParser`、`ValueCoercion`、诊断码格式化。
- 随机测试：任意输入经规范化后不含 `..`、绝对路径、空段。

出口：Shared 行覆盖 ≥ 90%。

### 阶段 2：Bukit.Config

- 严格 YAML：未知字段、类型错误、重复键、必填缺失。
- 默认值：逐项断言。
- 校验器：collections、i18n、provider、theme manifest、SEO 标题模板、时区。
- `modelSchema` 各映射：必填键缺失时的行为（见第 9 节待决 2）。
- 环境变量覆盖：优先级、非法值、无关 `BUKIT_*__*` 变量的影响。
- JSON Schema 生成与运行时校验器逐字段对照，防漂移。

出口：Config 行覆盖 ≥ 90%，schema 对照测试覆盖全部配置节。

### 阶段 3：领域与内容源

- `ContentDocument.Create`：字段回退链、状态默认值、`draft` 与 `status` 关系。
- `CanonicalContentGraphBuilder` 与 Validator：关系有向、悬空引用、重复 id。
- Markdown：front-matter（BOM、CRLF、YAML 边界）、正文投影、目录。
- 媒体：本地化、签名与格式校验、重复引用、下载失败。
- Notion（假服务）：属性映射、分页、限流重试、块渲染、HTML 转块、写入。
- 组合提供者：多源冲突、body 缓存。
- 业务规则：draft 撤下页面与投影；过期、noindex 只退出索引聚合；`VerifiedAt` 不映射 `reviewedAt`。

出口：领域与内容各项目行覆盖 ≥ 85%。

### 阶段 4：路由、主题、渲染

- 路由：permalink 模板、分页、列表路由、冲突、路径安全。
- 主题：`theme.yaml` 严格校验、extends、section 数据与 schema、tokens。
- Scriban：模板模型字段、函数、错误定位、`page.content_model.slug` 契约、linter 与符号分析。
- 渲染确定性：同一输入同一输出。

出口：三个项目行覆盖 ≥ 85%。

### 阶段 5：Engine 安全层

按顺序：
1. `OutputDirectoryCleaner`：站点根、用户主目录、文件系统根、`.git`、符号链接、无 marker 的非空目录。
2. `BuildTransaction`：提交前失败保持原状、提交中途失败反向还原、备份保留、异步上下文、资源租约冲突。
3. `AssetOutputPlan`：精确冲突、结构冲突、大小写敏感与不敏感文件系统。
4. 增量构建：manifest v3、各 Contributor 的失效条件、目录哈希超限行为、增量与全量等价。
5. 输出归属：过期文件清理、未跟踪文件保留、遗留或无效归属触发的恢复。

出口：以上五项每项至少含正常、失败、边界三类用例；增量等价测试通过。

### 阶段 6：Engine 输出与审计

- 内置插件逐个：Alias、Archive、DataFiles、Feed、ImageProcessing、LlmsTxt、Menu、PagesIndex、Pagination、RelatedContent、SearchIndex、Sitemap、Taxonomy、Analytics。
- 搜索索引：HTML 去标签、script 与 style 处理、截断、片段。
- SEO：JSON-LD 各类型、标题模板、canonical、hreflang、alternates、Open Graph。
- GEO：`llms.txt`、`llms-full.txt`，FAQ、HowTo、引用结构。
- 审计：SEO、GEO、发布、机器可读性信任、隐私检查、路由清单。
- 所有报告对 `docs/schemas` 做契约校验。

出口：Engine 行覆盖 ≥ 85%；所有已发布 schema 均有至少一个通过用例和一个失败用例。

### 阶段 7：管线集成与端到端

- 三个 fixture 站点完整构建，输出树与 golden 快照逐字节比较。
- 确定性：连续构建两次一致；改动单文件后增量结果等于全量结果。
- 多语言根聚合：sitemap、feed、llms、robots、search、agent manifest。
- 失败注入：渲染中途失败、磁盘满、插件崩溃，验证正式输出不被破坏。
- 并发：并行渲染结果与串行一致。

出口：golden 与确定性测试稳定通过，连续运行 20 次无抖动。

### 阶段 8：CLI

- 解析器与帮助：选项、必填、冲突、未知参数、退出码。
- 命令：`build`、`doctor`、`config`、`clean`、`lint`、`preview`、`version`、`completion`。
- `seo`、`geo`、`publish` 及子命令：报告读取、schema 校验、`diff`、`--external`。
- `deploy`：本地裸 git 仓库验证 `--dry-run`、分支名校验、CNAME、超时。
- `dev`：监听范围、重建、WebSocket 握手、普通 HTTP 请求处理。
- 命令与文档一致：每个注册命令都出现在 CLI 参考文档中，反之亦然。

出口：Cli 行覆盖 ≥ 80%；每个命令至少一个成功和一个失败退出码用例。

### 阶段 9：插件体系

- PluginHost：清单加载与迁移、平台选择、哈希与路径校验、权限评估、环境清空与白名单、密钥掩码。
- 协议 v1：handshake、manifest、invoke 的正常与异常（超时、崩溃、乱码、超大输出、进程树泄漏）。
- 锁文件与 CI 策略。
- 外部插件（假服务）：IndexNow 两阶段、WechatSync、Import、Clone 的契约。

出口：PluginHost 行覆盖 ≥ 85%；每类异常都有对应用例。

### 阶段 10：架构与门禁

- 依赖矩阵：`Engine.Abstractions` 不引用 `Config`；Core 不引用 Plugins 与 Labs。
- 公共 API 漂移基线。
- AOT 发布冒烟：`PublishAot` 成功并能运行 `version`。
- 覆盖率下限校验。

出口：架构测试全绿，基线与源码一致。

### 阶段 11：Labs 与导入、克隆（后置，不在本期）

仅补契约与冒烟：命令解析、输出结构、不联网路径。阶段 9 中的外部插件（IndexNow、WechatSync、Import、Clone）契约测试同样后置。

## 6. 已知缺陷转化为测试

下列缺陷已对照源码确认（详见思维导图"已对照源码确认"分支）。每条对应一个 `KnownDefect` 测试，所属阶段如下。

| 缺陷 | 阶段 |
|---|---|
| `SeoExternalAuditor` 硬编码读取 `routes`，发布报告用 `documents` | 8 |
| `clean` 删除 `.bukit`，含手写 `plugins.yaml` 与锁文件（决定：应保留） | 5 |
| `HashUtil` 对超限文件只记长度，同长度改动会误命中缓存 | 5 |
| `TaxonomyRedirectWriter` 别名经 `Trim` 后拼路径，是否可越界待查（决定：含 `..` 或绝对路径应拒绝并报错） | 5 |
| `ConfigCollectionReader` 缺必填键的 `modelSchema` 条目被丢弃（决定：应加载时报错） | 2 |
| `SeoAlternatesService.AddVariantRouteAlternates` 恒返回 `existing` | 6 |
| 无条目的 FAQPage、HowTo 走 Article 构建器，且引用输出两份 | 6 |
| `StripHtmlToText` 丢失两个 script 或 style 块之间的文本 | 6 |
| `DocsCheckCommand` 未注册 | 8 |
| `deploy --dry-run` 仍执行 clean 构建（决定：应不构建，只校验现有输出） | 8 |

## 7. 执行规则

本计划的执行遵守 `AGENTS.md`：

- 只运行与改动对应的专项测试，例如 `dotnet test tests/Bukit.Config.Tests -c Release --filter "FullyQualifiedName~<类名>"`。
- 未经明确授权，不运行 `scripts/test-all.sh`、`scripts/smoke-all.sh`、整个解决方案测试或发布门禁。
- 每批开工前用 `python3 scripts/checks/codex-workflow.py closure` 生成受影响闭包，并处理所有未映射文件。
- 同一时刻只有一个写入者修改仓库文件。
- 专项测试通过后用 `codex-workflow.py cache record` 记录绿色证据，未变化的命令不重复运行。
- 本计划只新增测试与测试辅助代码，不修改 Core 源码；缺陷修复是单独的、需用户确认的 Core 任务。

## 8. 风险

| 风险 | 缓解 |
|---|---|
| 12 万行代码，用例量约 3000 到 4000，周期长 | 按阶段出口分批交付，每阶段独立可用 |
| golden 快照随功能变化频繁失效 | 快照只放三个最小站点，更新需要评审 |
| 文件系统差异（大小写、符号链接、Windows） | 相关用例按平台条件化，CI 至少覆盖 Linux 与 macOS |
| 假服务与真实 Notion、微信行为偏差 | 假服务只验证本项目的请求构造与错误处理，不声称等价真实服务 |
| 竞态用例抖动 | 并发用例连续运行 20 次作为稳定标准 |

用例总量为按"每百行 1 到 3 个"的经验比例粗估，不是精确数字。

## 9. 待决事项

以下 6 项已于 2026-10-24 由负责人决定。

| # | 事项 | 决定 | 影响 |
|---|---|---|---|
| 1 | 覆盖率目标 | 整体沿用 84%/70%，仅对 Engine 安全层上调：`BuildTransaction`、`OutputDirectoryCleaner`、`AssetOutputPlan` 行覆盖 ≥ 95% | 阶段 5 出口标准 |
| 2 | `modelSchema` 缺必填键的条目 | 加载时报错 | 阶段 2：测试期望抛 `ConfigException` |
| 3 | `clean` 对 `.bukit/plugins.yaml` 与 `plugins.lock.yaml` | 保留，只删缓存与构建产物 | 阶段 5：测试期望配置与锁文件不被删除 |
| 4 | `deploy --dry-run` | 不构建，只校验现有输出；无输出时给出明确错误，提示先 build | 阶段 8：测试期望输出目录不被改动 |
| 5 | 范围 | 只做 Core，外部插件与 Labs 后置 | 阶段 9 只含 PluginHost 与协议；阶段 11 及外部插件契约延后 |
| 6 | 别名含 `..` 或绝对路径 | 拒绝并报错，给出诊断码 | 阶段 5：测试期望构建失败且不写入任何文件 |

**对 Core 源码的影响**：决定 2、3、4、6 都要求修改 Core 行为（当前实现与之不符）。按 `AGENTS.md`，Core 修改须作为单独的、经用户确认的 Core 任务进行；本计划只提供测试。这几条测试在修复完成前标 `KnownDefect` 排除。

## 10. 验收

计划视为完成的条件：
1. 阶段 0 到 10 的出口标准全部满足。
2. 第 3 节的 6 项质量目标均有对应的自动化测试且稳定通过。
3. 第 6 节所列缺陷，每条都有测试，且处于"已修复并通过"或"已知缺陷并说明原因"两种状态之一。
4. 覆盖率达到第 9 节确认的目标。

## 11. 修订记录

| 日期 | 说明 |
|---|---|
| 2026-10-24 | 初稿 |
| 2026-10-24 | 记录 6 项待决事项的决定；覆盖率、缺陷语义、范围随之更新 |
