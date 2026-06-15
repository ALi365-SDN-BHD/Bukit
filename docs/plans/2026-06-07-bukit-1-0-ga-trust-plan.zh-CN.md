# Bukit 1.0 GA 信任计划书

日期：2026-06-07  
代码基线：`origin/main` / `HEAD` = `1008f4a2b634b6dc8f45604aa0325edeaee96e52`  
目标：把 Bukit 从 public preview 收口到“任何人都敢用于正式网站”的 1.0，而不是继续扩大功能面。

新增前提（2026-06-08）：Bukit 1.0 按全新项目实施。各模块不采用兼容模式，不承诺历史文件、历史网站、旧配置、旧主题或旧插件协议可继续运行。1.0 前允许集中清理旧行为；1.0 后只稳定全新契约。

## 0. 结论

Bukit 1.0 应按“信任优先”发布，而不是按“功能完整”发布。当前 main 已经具备进入 1.0 收口的基础：配置模型、内容模型、路由生成、安全路径校验、主题模板能力、外部插件协议、诊断码、`.bukit/` 报告、smoke 与 security 脚本都已经存在。现在的关键风险不是缺少能力，而是公开契约、文档、示例、审计产物和全新项目规则还没有形成一个稳定、可验证、可回滚的整体。

本计划建议：

1. `BukitJalil` 不进入 Bukit 1.0 GA 范围。
2. AI Intent、clone/import、主题注册/生态分发、开放插件生态可以保留，但默认归入 `GA-limited` 或 `Experimental`，不能共享核心 1.0 稳定承诺。
3. 1.0 只冻结核心建站链路：config -> content -> route -> render/theme -> plugin lifecycle -> build output -> audit/report -> diagnostics -> security gates。
4. 1.0 以全新项目为唯一目标面，不设置 legacy compatibility layer。旧写法应在 1.0 前清理为拒绝、删除或明确新写法提示。
5. 1.0 前允许一次集中 cleanup；cleanup 后核心契约必须按 SemVer 稳定。

## 1. 当前代码事实

### 1.1 已验证命令

在当前工作区运行结果：

| 命令 | 结果 | 说明 |
|---|---:|---|
| `git fetch origin main` | 通过 | 本地 `origin/main` 已刷新 |
| `dotnet test bukit.slnx -c Release --no-restore` | 通过 | 约 3,465 个测试通过 |
| `bash scripts/smoke.sh Release` | 通过 | 最终输出 `Smoke OK` |
| `bash scripts/security-regression.sh Release` | 通过 | 沙箱内首次因 VSTest 本地 socket 权限失败；提升权限后通过 |

### 1.2 工作区状态

当前 `HEAD` 与 `origin/main` 一致，但工作区存在未提交改动，主要集中在：

- `examples/starter/**`：starter 内容、主题、模板能力清单、taxonomy 示例。
- `src/Bukit.Engine/BuildReporter.cs`：`.bukit/` 报告 schema 与 security report 相关改动。
- `tests/Bukit.Cli.Tests/DoctorCommandTests.cs`、`tests/Bukit.Engine.Tests/BuildReporterTests.cs`：doctor 与 build report 测试。
- `docs/schemas/*.v1.schema.json`：build/routes/assets/incremental/security schema。

因此本文把它们视为“当前待评审工作区事实”，不是已经发布的稳定 1.0 契约。

### 1.3 已具备的 1.0 基础

| 面向 | 当前基础 | 主要代码/资产 |
|---|---|---|
| 配置契约 | `AppConfig` 已有明确 record 模型，覆盖 `site/content/build/theme/taxonomy/logging/deploy` | `src/Bukit.Config/AppConfig.cs` |
| 内容模型 | 有 canonical schema、字段映射、provider、body store、schema validator | `src/Bukit.Engine/ContentModelSchema*`、`src/Bukit.Content/**` |
| 路由行为 | 有 route source、collection/permalink、override、output path encoding、安全校验 | `src/Bukit.Routing/RouteGenerator.cs`、`RouteSecurityValidator.cs` |
| 主题接口 | 有 `theme.yaml`、`extends`、模板能力 manifest、theme source lock、doctor 检查 | `src/Bukit.Theme/**`、`src/Bukit.Engine/TemplateCapabilitiesResolver.cs` |
| 插件接口 | 有 built-in plugin pipeline、process protocol plugin、handshake v2、capabilities、env isolation、output tracking | `src/Bukit.Engine/Plugins/**` |
| 可复现/审计 | 已有 `.bukit/build-report.json`、`routes.json`、`assets.json`、`incremental-manifest.json`、`security-report.json` 的写入基础 | `src/Bukit.Engine/BuildReporter.cs`、`docs/schemas/*.json` |
| 错误定位 | 已有 `DiagnosticCode` 范围和 formatter | `src/Bukit.Shared/DiagnosticCode.cs` |
| 安全边界 | 已有 route/output path 防逃逸、SSRF guard、插件能力、security regression | `RouteSecurityValidator.cs`、`SafePathResolver.cs`、`scripts/security-regression.sh` |
| 回归体系 | 单测、架构测试、smoke、security、quality gate 脚本均存在 | `tests/**`、`scripts/**` |

## 2. 1.0 支持等级

1. `GA-locked`：1.0 后只允许对全新 1.0 契约做非破坏性演进；breaking change 必须进入未来 major。
2. `GA-limited`：正式可用，但边界窄、约束明确，不承诺生态完整性。
3. `Experimental`：可随 public preview 演进，不进入 1.0 稳定承诺。
4. `Out of scope`：不属于 Bukit 1.0。

| 能力 | 1.0 等级 | 冻结/说明 |
|---|---|---|
| `site.yaml` 核心字段、默认值、校验语义 | `GA-locked` | 必须发布 schema；旧字段不运行，只给拒绝信息或新写法提示 |
| Markdown 内容源 | `GA-locked` | 正式网站基础路径 |
| Notion 内容源 | `GA-limited` | API、缓存、媒体下载、field policy 边界必须写清楚 |
| Composite content sources | `GA-limited` | 仅支持 1.0 新语义；不为旧 `provider` 行为保留兼容层 |
| 内容模型 schema、reserved meta、field normalization | `GA-locked` | 模板和插件依赖此面 |
| 路由优先级、派生路由、output path encoding | `GA-locked` | 必须有 golden inventory |
| SEO/GEO publish projection | `GA-limited` | 输出和审计 schema 稳定；评分规则可演进但需版本化 |
| Starter theme 与本地主题 | `GA-locked` | starter 是官方信任样板 |
| Remote theme source/lock | `GA-limited` | 远程获取与 lock 行为稳定，registry 生态另计 |
| Theme registry/search/install 生态 | `Experimental` | 不承诺生态可用性 |
| Built-in plugin lifecycle | `GA-locked` | 内建插件顺序、失败策略、输出归属需冻结 |
| External process protocol plugin | `GA-limited` | 协议 v2、capability、env/output 边界稳定 |
| Source-generated plugin SDK | `GA-limited` | 若对外暴露，需独立版本说明 |
| AI Intent / samples/intent | `Experimental` | 不纳入核心 1.0 信任链 |
| clone/import | `Experimental` 或 `GA-limited` | 可作为重新生成 1.0 新项目的工具，不作为历史站点兼容层 |
| BukitJalil | `Out of scope` | 不进入 Bukit 1.0 |

## 3. 1.0 必须冻结的公开契约

### 3.1 配置契约

冻结范围：

- `site.yaml` 七大节点：`site`、`content`、`build`、`theme`、`taxonomy`、`logging`、`deploy`。
- 所有默认值、校验规则、override precedence、环境变量覆盖行为。
- 旧字段处理表：1.0 不运行兼容旧字段；旧写法只能是 rejected 或 rejected-with-message。

当前缺口：

- 决定 `content.provider` 与 `content.sources` 的 1.0 唯一推荐形态；如果保留双入口，必须是新项目语义，不是旧项目兼容。
- warning-only 项在 1.0 不应继续存在；应升级为 rejected-with-message 或从文档和示例中删除。
- `site.rssMode`、taxonomy legacy 模板配置、未声明 plugin capabilities 等旧行为不进入 1.0 运行面，应在 cleanup 中删除、拒绝或强制显式配置。
- `build.report.enabled` 当前默认关闭，1.0 应决定 release/profile 是否默认生成 `.bukit/` 审计产物。

验收：

- `bukit config schema` 输出成为官方 schema。
- `bukit config check`、`bukit doctor`、build-time validation 对同一错误给出一致路径和诊断码。
- 每个 public 字段有生命周期状态：stable、limited、experimental、removed。1.0 不设置 deprecated-but-working 状态。

### 3.2 内容模型契约

冻结范围：

- canonical content record：identity、classification、publishing、localization、provenance、fields、media。
- reserved meta keys：`title`、`slug`、`type`、`collection`、`language`、`i18nKey`、`publishAt`、`updatedAt`、`summary`、`seo_*`、`tags`、`categories`。
- `page.fields.*` 与 engine-owned meta 的边界。
- Markdown / Notion / composite sources 在同一字段上的归一化结果。

当前缺口：

- starter 内容仍触发 extra schema fields：如 `seo_title`、`cover`、`cover_alt`、`tableOfContents`。需要决定这些字段是 starter schema 的正式字段、theme-only 字段，还是 warning 噪音。
- 内容同时声明 `type` 与 `collection` 会产生 warning。1.0 应选择唯一新项目写法，并在 starter 中消除另一种写法。
- publish audit 对 author/entity/source/time/updatedAt 等要求大量 warning；需区分“正式内容质量建议”和“starter 示例必须满足的最低发布标准”。

验收：

- provider parity 测试覆盖 Markdown、Notion、composite。
- schema strict/warn/off 行为有快照测试。
- starter 内容在默认 smoke 下不产生误导性 schema warning。

### 3.3 路由契约

冻结范围：

- 优先级：full route override -> base collection/permalink -> partial route override 的实际行为必须写入契约。
- collection vs type 匹配规则。
- list、filtered list、pagination、taxonomy、archive、alias、static HTML route 的派生规则。
- `outputPathEncoding`：`none`、`slug`、`urlencode`、`sanitize`。
- route/url/outputPath 安全拒绝语义和诊断码。

当前缺口：

- 技能/文档中存在“partial override 忽略 outputPath”的描述，但当前 `RouteGenerator` 对 nested `route.outputPath` 会使用 override。必须在 1.0 cleanup 中二选一：要么承认 nested `route.outputPath` 是全新契约，要么直接拒绝。
- 顶层 `outputPath` 当前被拒绝并提示使用 `route.outputPath`。1.0 应保持拒绝旧顶层写法，并给出明确诊断码与新写法提示。
- 派生路由大量参与 publish audit，但 list/taxonomy 是否应进入 search/rss/sitemap 的默认策略仍需冻结。

验收：

- route inventory golden tests 覆盖原始内容、派生页、静态 HTML、插件输出。
- `dist/.bukit/routes.json` 作为可审计路由事实，schema 固定。
- 所有 route conflict 与 unsafe path 失败都有稳定 `BKT-02xx` 诊断码。

### 3.4 主题接口契约

冻结范围：

- `theme.yaml` 字段、`version`、`requires_bukit`/engine range、`extends` 继承。
- `layouts/bukit.templates.yaml` 模板能力声明。
- required templates、kind accepts、template fallback、theme params。
- starter theme 作为 1.0 官方样板。
- remote theme source 与 lock file 行为。

当前缺口：

- starter 已通过 doctor/smoke，但仍产生大量 publish/seo audit warning，说明它还不是“正式网站可信样板”。
- 主题继承和 fallbackDir 若保留，应作为 1.0 新主题契约明确；无 `theme.yaml` 老主题不进入 1.0 运行兼容面，应由 doctor 拒绝或要求显式生成 manifest。
- 主题接口版本字段与 engine 支持范围需要成为 doctor 的硬检查或明确 warning。

验收：

- starter、alt、seo-best-practice 三类官方主题均通过 doctor/build/smoke。
- theme doctor 输出能直接定位缺失模板、能力声明不匹配、继承冲突、engine version 不兼容。
- 主题包可复现：同一 theme source/ref/lock 得到同一模板和资产清单。

### 3.5 插件接口契约

冻结范围：

- built-in plugin lifecycle：derive-pages -> render -> publish projection -> after-build。
- 内建插件顺序和输出归属。
- external process protocol v2：handshake、derive-pages request/response、after-build request/response。
- capability names：`derive-pages`、`emit-outputs`。
- env isolation、timeout、stdout/stderr limits、entry path、sha256、stale output cleanup。

当前缺口：

- 代码中 handshake 当前要求 schema version 2；治理文档仍提到 `v2 -> v1` 回退。1.0 应统一为只支持协议 v2，不保留 v1 fallback。
- 未声明 `capabilities` 默认放行属于高风险旧行为；1.0 应拒绝未声明能力的外部插件，而不是 warning 放行。
- 外部插件失败路径有 `InvalidOperationException` 包装场景，需要盘点是否都映射到稳定 `BKT-07xx`。

验收：

- 协议 schema 固化为 JSON schema 或等价文档。
- ProtocolEchoPlugin 覆盖 success、bad JSON、empty stdout、timeout、ok=false、capability missing、output traversal、stale cleanup。
- 插件输出全部进入 build manifest/audit story。

### 3.6 构建产物契约

冻结范围：

- `.bukit/build-report.json`
- `.bukit/routes.json`
- `.bukit/assets.json`
- `.bukit/incremental-manifest.json`
- `.bukit/security-report.json`
- `.bukit/seo-report.json`
- `.bukit/publish-audit-report.json`
- 后续如保留 GEO，则 `.bukit/geo-report.json`

当前缺口：

- `BuildReporter` 已写 schema 和 schemaVersion，但 security report 当前是固定 passed/checks 结构，还不等价于真实安全扫描证据。
- build report 中包含 startedAt/endedAt/durationMs/root/output 等天然不稳定字段；可审计需要保留，可复现对比需要定义忽略字段或 normalized report。
- assets 只枚举 output 下 `assets/`，不覆盖根级静态文件、feed/sitemap/search/llms 等 publish projection 输出。1.0 需要定义 artifact inventory 的完整范围。

验收：

- clean build 两次：输出文件集、route inventory、asset/report normalized hash 一致。
- clean vs incremental：public output inventory 一致。
- 发布包包含 `.bukit/` 审计目录，可用于 diff 和 rollback。

### 3.7 错误与诊断契约

冻结范围：

- `BKT-000x` config
- `BKT-010x` theme
- `BKT-020x` route
- `BKT-030x` render
- `BKT-040x` schema
- `BKT-050x` content
- `BKT-060x` build
- `BKT-070x` plugin
- `BKT-080x` seo/geo
- `BKT-090x` media

当前缺口：

- 诊断码范围已存在，但很多 `ConfigException` 仍没有 code。
- CLI 输出当前会打印 raw enum value 和 formatter 信息，需统一人类可读格式、机器可读格式、exit code。
- audit warning code 与 DiagnosticCode 是两套系统，1.0 需说明它们的关系：build failure diagnostic vs publish quality issue。

验收：

- GA-locked 路径的关键失败不得无 code。
- 每个错误包含：code、对象路径、原因、修复建议。
- CLI 有 `--json` 或等价机器可读错误输出策略。

### 3.8 安全边界契约

冻结范围：

- config path resolution
- route/url/outputPath validation
- output root clean safety
- dotfile publish policy
- symlink follow policy
- theme name/path/source/ref/lock safety
- media download SSRF/private network blocking
- plugin env/output/capability/path/sha256 safety
- unsafe URL sanitization in content rendering

当前缺口：

- `security-regression.sh` 已能覆盖重要安全面，但它的结果还没有写回 `.bukit/security-report.json`。
- 远程主题与媒体下载涉及网络边界，1.0 应默认保守：无 lock/无 allow policy 不进入 release gate。
- external plugin 默认 warn 允许执行，正式网站场景应提供更明确的 `externalPluginPolicy: deny` 推荐。

验收：

- `bash scripts/security-regression.sh Release` 是 release blocker。
- security report 反映真实检查结果，而不是静态 passed。
- 所有 unsafe path/url/plugin output 被拒绝并有 code。

## 4. 分阶段执行计划

### Wave 0：锁定 1.0 产品边界

目标：先明确哪些能力承诺，哪些不承诺。

任务：

- 发布 `docs/bukit-1.0-contract-matrix.zh-CN.md`，明确 1.0 是全新项目契约，不是历史兼容矩阵。
- 把本计划中的 support tiers 写入 README、guide、skills。
- 明确 `BukitJalil` out of scope。
- 明确 AI Intent、clone/import、registry、开放插件生态的等级。
- 将所有历史字段、历史主题、旧插件协议标注为 removed/rejected，不列入 GA 运行面。

验收：

- 所有示例、skills、guide 不再暗示 Experimental 能力属于 1.0 GA。
- public preview 文案改为“核心 1.0 收口中”，不是继续扩大预览范围。

### Wave 1：消除官方资产和治理噪音

目标：让 starter/example 成为用户敢复制的正式样板。

任务：

- 清理 starter 默认 smoke 中的 publish/seo warning：author、updatedAt、summary、entity/source、visible h1/article、jsonld title mismatch、missing search/rss/sitemap route。
- 解决 starter schema extra fields：把字段纳入 schema 或从示例中移除。
- 消除默认示例里的旧配置 warning，如 `site.rssMode`。
- 对 `type` 与 `collection` 双声明选择唯一 1.0 写法，并更新 starter。
- 固化 doctor/template manifest 回归测试。

验收：

- `bash scripts/smoke.sh Release` 通过，且 starter 默认路径不出现 release-blocking 或误导性 warning。
- `dotnet test bukit.slnx -c Release --no-restore` 通过。

### Wave 2：冻结核心契约

目标：把当前行为变成可承诺契约。

任务：

- 配置契约：生成 schema，补 provider/sources 新语义、removed/rejected 测试。
- 内容契约：补 provider parity 与 reserved meta 测试。
- 路由契约：补 route inventory golden tests，决定 nested `route.outputPath` 行为。
- 主题契约：补 `theme.yaml` version/engine range/extends/template capabilities 测试。
- 插件契约：统一为 handshake v2-only 文档与实现，补协议版本拒绝回归。
- 诊断契约：给 GA-locked failure path 补 code。

验收：

- 新增 `docs/bukit-1.0-contracts.zh-CN.md`。
- 新增或更新契约测试套件，能在 CI 中独立标识 contract failure。

### Wave 3：可复现、可审计、可回滚

目标：让正式网站构建结果有证据、有对比、有回退依据。

任务：

- 定义 release artifact bundle：public output + `.bukit/` + version metadata + theme/plugin lock info。
- 定义 deterministic compare：忽略时间/耗时/本地绝对路径，比较稳定字段。
- 扩展 `assets.json` 为完整 artifact inventory 或新增 `artifact-manifest.json`。
- security report 接入真实检查结果。
- clean build repeated、clean vs incremental 加入 CI。

验收：

- 同一输入树连续构建两次，normalized artifact manifest 一致。
- clean vs incremental public output inventory 一致。
- rollback 文档能说明如何选择、审计、回退到上一发布包。

### Wave 4：全新项目发布治理

目标：让 1.0 用户只面对一套清晰的新项目规则；旧输入被明确拒绝，而不是静默兼容。

任务：

- 完成 public preview -> 1.0 reset guide：说明如何重新生成 1.0 项目、如何手工搬运内容，不承诺旧站点原地升级。
- 将 compatibility governance 表改造成 removal/rejection governance：每个旧行为只允许 removed、rejected、rejected-with-message、experimental-out-of-scope。
- 定义 removal policy：1.0 前清理旧行为；1.0 后仅对 1.0 新契约使用 SemVer。
- 定义 release checklist 和 blocker。

验收：

- 每个 breaking cleanup 都有拒绝行为、诊断码、测试和 changelog。
- release checklist 包含测试、smoke、security、determinism、docs consistency。

## 5. Release Gates

1. `dotnet test bukit.slnx -c Release --no-restore`
2. `bash scripts/smoke.sh Release`
3. `bash scripts/smoke-all.sh Release`
4. `bash scripts/security-regression.sh Release`
5. `bash scripts/check-doc-asset-consistency.sh`
6. deterministic build compare：clean twice
7. incremental equivalence compare：clean vs incremental
8. schema validation：`.bukit/*.json` against `docs/schemas/*.v1.schema.json`
9. rejection suite：历史/旧写法 fixtures 必须被 1.0 engine 明确拒绝，并给出诊断码或新项目重写提示
10. no GA-locked failure path without diagnostic code

1.0.2 的硬门槛与可追溯产物列表另见：
   - [Bukit 1.0.2 Release Checklist（硬门槛）](./bukit-1.0.2-release-checklist.md)

## 6. 首批可拆 Issue

### P0

- 统一 plugin protocol handshake：1.0 只支持 v2，移除治理文档中的 v2 -> v1 fallback。
- 决定 nested `route.outputPath` 的 1.0 语义，并同步 routing 文档、skills、测试。
- 清理 starter 默认 publish/seo warning，让官方样板达到正式网站最低标准。
- 把 `site.rssMode` 从 starter/smoke 示例中移除，并在 1.0 中拒绝旧字段。
- 将 `security-report.json` 从静态 passed 改为真实检查摘要。

### P1

- 给 `.bukit` artifacts 增加 JSON schema validation 测试。
- 补 route inventory golden tests。
- 补 `content.provider` vs `content.sources` 新项目语义测试，避免旧兼容优先级继续存在。
- 补 external plugin 缺失 capability 的拒绝测试。
- 补 no-`theme.yaml` 主题被 doctor 拒绝或要求生成 manifest 的测试。

### P2

- 移除或替换内部 obsolete sync body resolver 调用。
- 收窄 import 的宽默认 pageTypes。
- 为 theme registry/search/install 定义 Experimental 文案。
- 定义 release artifact bundle 和 rollback 指南。

## 7. 不做事项

- 不把 BukitJalil 纳入 1.0。
- 不为了 1.0 增加大型新功能。
- 不通过降低 doctor/smoke/security 严格度来消除失败。
- 不保留 warning-only 运行路径。
- 不为历史文件、历史网站、旧主题、旧插件协议保留运行时兼容模式。
- 不承诺未版本化的主题生态、插件生态或 AI Intent 生态稳定。

## 8. 下一步

建议先执行 Wave 1。当前测试和 smoke 已经能通过，说明仓库不是“先修到能跑”的状态，而是“先把正式发布噪音和契约冲突清掉”的状态。完成 Wave 1 后，再进入 Wave 2 冻结核心契约，否则冻结的可能是仍带 preview 漂移的行为。
