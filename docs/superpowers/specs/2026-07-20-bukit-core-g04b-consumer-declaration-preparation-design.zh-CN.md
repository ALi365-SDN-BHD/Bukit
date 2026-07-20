# Bukit Core G-04B1 外部消费者声明准备设计

日期：2026-07-20

状态：方案 A 已批准；书面规格待最终确认

## 1. 目的

G-04A 已把 4 个 `build-report.v1` CLR mirror 和 2 个跨程序集 Theme 类型移出普通 2.0 候选池。G-04B1 在不实施 breaking change 的前提下，把剩余 136 个 `compatibility = 2.0-candidate` 类型整理成可公开追溯、机器可核对、能够接收消费者反馈的准备材料。

本任务只建立声明所需的事实、数据和生命周期。它不打开消费者声明窗口，不把“未发现公开消费者”解释为“没有消费者”，也不授权 G-04C 修改任何访问级别。

## 2. 范围

G-04B1 将：

1. 从当前受治理公共面基线精确派生 136 个 2.0 候选身份，并按 assembly 和 owner 对账。
2. 建立机器可读候选清单，记录候选身份、治理来源、声明状态、迁移目标和外部证据摘要。
3. 建立面向消费者的声明说明，解释候选含义、反馈方式、窗口开启条件和兼容承诺。
4. 使用已认证 GitHub 连接器执行只读代码搜索和仓库依赖检索，并保留查询时间、范围、命中与限制。
5. 建立 G-04B1 证据报告，区分公开证据、仓库内证据、私有消费者未知项和后续外部动作。
6. 更新活动公共 API 治理指南，使维护者能够找到候选清单和声明生命周期。

G-04B1 不会：

- 创建、编辑或评论 GitHub Issue、Pull Request、Discussion 或 Release；
- 推送分支、标签或提交到远端；
- 修改 Core/Labs/插件源码、测试源码、访问级别、CLR 签名或项目引用；
- 修改公共 API drift baseline 的类型签名或治理分类；
- 修改配置 schema、报告 schema、插件协议、持久化格式或 asset URL；
- 把候选类型声明为已弃用、已删除或已批准收窄；
- 启动 G-04C、打开 2.0 分支或开始一个实际声明周期；
- 修改 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/` 或 `scripts-0.2/`。

## 3. 采用方案

采用“机器清单 + 对外声明草案 + 证据报告”的方案。

只写叙述报告无法可靠证明 136 个候选与受治理基线一致；直接发布 GitHub 声明又会越过外部写操作授权。选定方案把确定性数据、面向消费者的说明和审计证据分开，使 G-04B1 可以在本地完整复审，同时把真正的外部声明保留给单独批准的 G-04B2。

## 4. 交付文件

### 4.1 机器候选清单

新增：

`docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`

职责：保存 G-04B1 时点的 136 个候选身份及其声明准备状态。该文件是治理工件，不是产品配置、报告、插件或持久化 schema。

### 4.2 消费者声明说明

新增：

`docs/governance/bukit-core-2.0-consumer-declaration.md`

职责：提供可公开阅读的候选含义、反馈方式、窗口规则、兼容边界和后续决策条件。文件必须显式标记 `prepared-not-open`，不得让读者误以为声明周期已经开始。

### 4.3 G-04B1 证据报告

新增：

`docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md`

职责：记录候选对账、GitHub 检索方法、命中复核、证据限制、风险分层、验证结果及 G-04B2 前置条件。

### 4.4 活动治理入口

修改：

`guide/dev/public-api-governance.md`

职责：链接候选清单和消费者声明说明，并重申 1.x 不收窄、无公开命中不等于安全删除、外部窗口必须单独开启。

G-03 与 G-04A 报告保持为各自时点的发现和纠正记录，不回写历史数字或结论。

## 5. 候选清单模型

根对象包含：

- `schema`：固定为 `bukit-core-2.0-public-surface-candidates/v1`；
- `schemaVersion`：固定为 `1`；
- `preparedAtUtc`：本次证据采集完成的 UTC 时间；
- `sourceBaseline`：受治理 baseline 路径、Git commit 和 SHA-256；
- `selection`：固定表达式 `compatibility == 2.0-candidate`；
- `candidateCount`：固定为 `136`；
- `migrationTarget`：固定为 `2.0.0`；
- `declarationState`：固定为 `prepared-not-open`；
- `windowPolicy`：窗口开启与结束规则；
- `feedbackChannel`：计划使用 GitHub Issue、仓库 `ALi365-SDN-BHD/Bukit`、`issueNumber = null`、`state = not-created`；
- `assemblyCounts` 和 `ownerCounts`：从候选数组重算的汇总；
- `candidates`：按 `assembly`、`fullName` 排序的 136 个唯一条目。

每个候选条目包含：

- `assembly`、`fullName`、`owner`；
- `classification = implementation-public`；
- `compatibility = 2.0-candidate`；
- `migrationHorizon = 2.0-review`；
- `declarationStatus = consumer-declaration-pending`；
- `proposedAction = review-only`；
- `externalEvidence`，包含认证状态、检索时间、查询、命中状态、经复核仓库和证据限制；
- `privateConsumerStatus = unknown-until-voluntary-declaration`。

`review-only` 表示进入反馈和语义复核范围，不表示计划删除。任何出现外部消费者、反射、serializer、AOT、protected surface 或非候选 public signature 传播证据的类型，都必须在后续任务中转入保留、facade 迁移或 obsolete 路线，不能继续按零消费者处理。

G-04A 的 6 个纠正类型不得出现在候选清单中。`Bukit.Engine.RouteInventoryInspectEntry` 必须存在，但其状态仍为 `consumer-declaration-pending / review-only`，不能提前标记为 G-04C 已批准。

## 6. 外部消费者证据采集

### 6.1 认证与边界

GitHub 连接器必须先成功返回认证状态。所有调用只读；任务不得调用创建 Issue、评论、修改仓库或发布 Release 的工具。

### 6.2 查询策略

对每个候选执行并记录：

1. 完整 CLR 名称查询；
2. simple type name 查询；
3. 对可能命中的仓库复核 namespace、assembly、package/reference 上下文，排除同名但无关代码；
4. 排除 Bukit 自身仓库命中，单独标记 fork、镜像或复制源码；
5. 检索 `ALi365-SDN-BHD/Bukit`、仓库 URL 和可能的项目/包引用文本，补充仓库级依赖信号。

每个查询使用 `topn = 20`，并记录实际 query、UTC 时间、返回数量、是否达到 20 条结果上限、候选仓库及人工复核结论。返回恰好 20 条时按截断处理，不能据此作出完整的零消费者结论。结果上限、索引延迟、私有仓库不可见或连接器错误必须作为限制写入，不能把失败或截断查询记为零命中。

### 6.3 证据状态

允许的公开搜索状态为：

- `no-public-match-found`：认证查询成功，未发现经复核的外部命中；
- `owner-repository-only`：只有 Bukit 自身源码或文档命中；
- `fork-or-mirror-observed`：命中 fork、镜像或复制源码，尚不能证明独立消费；
- `external-match-needs-review`：存在可能的外部使用，但上下文不足；
- `confirmed-external-consumer`：有明确的类型或仓库依赖证据；
- `search-incomplete`：认证、限流、截断或工具错误使检索不完整。

任何 `confirmed-external-consumer` 或 `external-match-needs-review` 都必须在 G-04B1 报告中单列，且不得把对应类型推荐给 G-04C。`no-public-match-found` 只表示本次公开检索未找到证据，私有消费者状态仍保持未知。

## 7. 声明窗口生命周期

G-04B1 完成时：

- `declarationState = prepared-not-open`；
- GitHub 专用 Issue 尚未创建；
- `openedAtUtc`、`announcementUrl` 和 `eligibleAfterRelease` 不写入虚构值；机器清单使用明确的 `null`，文档解释其含义；
- 1.x 公共 CLR 可见性保持不变。

G-04B2 只有在单独获得外部写操作批准后才能：

1. 推送包含声明材料的提交；
2. 创建专用 GitHub Issue，并把实际 URL/编号写回治理记录；
3. 在 Release Note 或等效公开渠道发布声明；
4. 将状态改为 `open` 并记录实际开启时间。

窗口不得仅因经过若干日历天自动关闭。最早关闭条件是：声明发布后至少完成一个后续非 prerelease Core 稳定版本，所有反馈已分类处理，并由独立审计确认没有未解决的消费者证据。没有反馈仍不构成删除授权。

## 8. 数据流与一致性

```text
governed public API baseline
  -> filter compatibility == 2.0-candidate
  -> exact 136 identities
  -> authenticated GitHub read-only evidence
  -> candidate manifest
  -> public declaration document
  -> G-04B1 audit report
  -> separately approved G-04B2 publication
  -> one complete stable release cycle
  -> separately approved G-04C decision
```

清单中的 identity、classification、compatibility、migration horizon 和 owner 必须与受治理 baseline 一致。外部证据是附加事实，不能反向静默修改 baseline。若发现新的分类错误，应停止对应候选的声明处理并建立独立治理纠正任务。

## 9. 异常与失败处理

- 候选数量不是 136、存在重复或与 baseline identity 集合不一致：任务失败，不生成“已准备”结论。
- GitHub 认证失败：相关条目标记 `search-incomplete`，G-04B1 不得申请完整关闭。
- 查询限流、截断或单项错误：保留具体条目和原因，只重试受影响查询，不把错误改写为零命中。
- 发现外部消费者：保留原始证据链接和判定理由，将类型排除出任何试点推荐，但不在本任务修改代码或兼容级别。
- 文档与 JSON 汇总不一致：以受治理 baseline 重算，修正文档后重新验证。
- 发现 G-04A 六类型重新进入候选池：视为治理回归，停止任务并复核 baseline，而不是在清单中手工排除掩盖问题。

## 10. 验证与复审

实现阶段必须验证：

1. 两个 JSON 文件均可解析，baseline 的 `2.0-candidate` 集合与候选清单 136 个 identity 完全相等。
2. 汇总固定为：`Bukit.Cli.Shared=5`、`Bukit.Content=35`、`Bukit.Engine=57`、`Bukit.PluginHost=16`、`Bukit.Rendering=2`、`Bukit.Routing=1`、`Bukit.Shared=17`、`Bukit.Theme=3`。
3. 所有候选具有唯一 identity、完整 owner、固定治理字段和完整外部证据状态。
4. G-04A 六类型全部缺席，`RouteInventoryInspectEntry` 恰好出现一次且未被标记为已批准。
5. GitHub 查询为认证只读调用；没有任何外部写操作。
6. `bash scripts/checks/public-api-drift-self-test.sh` 通过。
7. `bash scripts/checks/public-api-drift.sh check Release` 通过，证明 CLR surface 和 baseline 未变化。
8. `dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --no-restore` 通过。
9. `bash scripts/checks/post-change-targeted.sh -- docs/governance/bukit-core-2.0-public-surface-candidates.v1.json docs/governance/bukit-core-2.0-consumer-declaration.md docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md guide/dev/public-api-governance.md` 在非沙箱环境完整通过。
10. `git diff --check`、占位符扫描、链接检查、绝对路径扫描和声明状态一致性检查通过。

这是公共兼容治理任务。定向门禁通过后必须进行一次独立只读复审，逐项检查候选身份、外部证据、声明窗口状态、未修改访问级别和无外部写操作。复审有未关闭 finding 时不能进入 G-04B2。

不运行 `ci-full`、release、`test-all`、`smoke-all` 或整仓库解决方案测试。

## 11. 完成条件与后续任务

G-04B1 只有在以下条件全部满足时才可关闭：

- 136 个候选与受治理 baseline 精确一致；
- GitHub 认证成功，136 个候选的完整名称和 simple name 查询均成功返回且没有 `search-incomplete`；截断查询已通过补充查询或保守的 `external-match-needs-review` 状态处理；
- 所有可能外部命中已人工复核并在报告中单列；
- 消费者声明文档明确为 `prepared-not-open`；
- 未发生外部写操作、访问级别变更或 baseline shape/classification 变更；
- 定向门禁通过且独立只读复审无未解决问题。

完成 G-04B1 后的下一项不是自动进入 G-04C，而是申请 G-04B2 外部发布授权。G-04B2 打开声明窗口并经过至少一个完整稳定发布周期后，才可重新评估单类型 2.0 试点。
