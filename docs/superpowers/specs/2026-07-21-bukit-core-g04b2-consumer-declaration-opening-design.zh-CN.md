# Bukit Core G-04B2 消费者声明窗口开启设计

日期：2026-07-21

状态：已批准设计；待实施计划与最终发布批准

## 1. 目的

G-04B1 已把受治理公共 API 基线中的 136 个 `2.0-candidate` 类型整理为
机器可读候选清单，并完成已认证的 GitHub 公开检索。当前材料已进入
`origin/main`，但生命周期仍为 `prepared-not-open`，专用 GitHub Issue 尚未
创建。

G-04B2 的目的，是在不修改任何 CLR 可见性、产品契约或候选分类的前提下，
把已准备的消费者声明正式公开为一个可接收反馈的 GitHub Issue，并把真实
Issue 编号、URL 和开启时间回写受治理记录。

G-04B2 只负责打开声明窗口。它不关闭窗口，不把公开零命中解释为零消费者，
不批准弃用、收窄或删除任何类型，也不启动 G-04C。

## 2. 已批准范围

### 2.1 包含

1. 在 `ALi365-SDN-BHD/Bukit` 创建一个专用 GitHub Issue。
2. Issue 使用英文主体和简短中文说明。
3. 把 GitHub 返回的真实 Issue 编号、URL 和 `createdAt` 回写治理文件。
4. 将全局声明状态从 `prepared-not-open` 调整为 `open`。
5. 保持 136 个候选的 `consumer-declaration-pending / review-only` 状态。
6. 更新声明文档、G-04B1 报告的后续状态区块和活动治理指南。
7. 对本地变更执行机器对账、定向门禁和独立只读复审。
8. 在最终发布批准后，以 fast-forward-only 方式把受控提交推送到
   `origin/main`。
9. 推送后重新读取 GitHub Issue 和远端治理文件，确认状态一致。

### 2.2 不包含

- 不修改任何 Release、Release Note、tag 或 prerelease 状态；
- 不创建 Pull Request、Discussion、milestone 或新 label；
- 不修改现有 Issue、PR 或 Release；
- 不修改 Core、Labs、插件或测试源码；
- 不修改 CLR 可见性、类型、成员、签名、程序集或项目引用；
- 不修改公共 API baseline 的候选身份、分类或签名；
- 不修改配置、主题、报告、插件协议、持久化格式或 asset URL；
- 不设置日历截止日期，不预填未来稳定版本；
- 不关闭声明窗口，不批准 G-04C 或 G-04D；
- 不修改 backup/reference 目录。

## 3. 发布方案

采用“本地完整预检、Issue-first、真实元数据回写、失败补偿”的方案。

G-04B1 材料已经位于公开的 `origin/main`，因此 Issue 可以链接现有清单和声明。
在创建 Issue 前，所有不依赖真实 Issue 元数据的正文、状态更新模板和验证命令
必须在独立 worktree 中准备完成。Issue 是第一个外部写操作；创建成功后只把
GitHub 返回值填入预先限定的字段，不临时扩展范围。

相较于先创建并关闭 Issue、随后重新打开的两阶段方案，本方案减少不必要的
外部状态转换。相较于 PR 驱动方案，本方案缩短 Issue 已开放但治理文件尚未
同步的时间。其代价是需要明确的失败补偿：如果真实元数据写回、验证、提交或
fast-forward push 失败，必须立即把 Issue 标记为 opening paused 并关闭。

## 4. 权威来源与前置条件

### 4.1 权威来源

- 候选身份与分类：
  `docs/governance/bukit-core-public-api-baseline.v1.json`；
- 当前 136 项声明清单：
  `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`；
- 对外声明正文：
  `docs/governance/bukit-core-2.0-consumer-declaration.md`；
- G-04B1 证据和限制：
  `docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md`；
- 活动维护者入口：`guide/dev/public-api-governance.md`；
- GitHub Issue 编号、URL、状态和时间：GitHub 连接器的创建与读取结果。

### 4.2 创建前必须满足

1. 认证 GitHub profile 查询成功。
2. 认证账号对 `ALi365-SDN-BHD/Bukit` 具有创建和更新 Issue 的权限。
3. 仓库为公开仓库，默认分支仍为 `main`。
4. `origin/main` 仍包含 G-04B1 的 manifest、声明、报告和 guide。
5. 没有同目的的开放 Issue。
6. 候选清单仍为 136 个唯一 identity，且没有 `search-incomplete`。
7. 本地分支基于创建前重新取得的 `origin/main`，不是陈旧 commit。
8. Issue 标题、正文和四个治理文件的精确变更摘要已向用户展示，并获得一次
   最终、明确的发布批准。

任一前置条件失败时，G-04B2 停止在本地准备阶段，不创建 Issue。

## 5. GitHub Issue 契约

### 5.1 标题

```text
[G-04B2] Bukit Core 2.0 public surface consumer declaration
```

### 5.2 正文结构

Issue 正文按以下顺序编写：

1. `Purpose`
2. `What is being reviewed`
3. `Current 1.x compatibility commitment`
4. `Candidate manifest and declaration`
5. `How to report usage`
6. `Reflection, inheritance, serialization, and Native AOT`
7. `Window lifecycle`
8. `Explicit non-claims`
9. `中文说明`

正文必须链接 `origin/main` 上的候选 manifest 和消费者声明，不链接本地路径、
临时分支或尚未存在的 commit。

### 5.3 消费者反馈字段

请求消费者在能够公开的范围内提供：

- 完整 CLR 类型名；
- 使用入口和使用方式；
- 所使用的 Bukit 版本或 commit；
- 是否通过项目引用、DLL、源码复制、反射、继承、序列化或 Native AOT 使用；
- 迁移到 facade、替代入口或 obsolete 路线时的限制。

不得要求消费者公开 token、凭据、私有源码、客户数据或其他敏感业务信息。
消费者可以只描述依赖性质，并请求维护者在非公开渠道进一步核实。

### 5.4 必须声明的边界

- 136 项是 review candidates，不是删除清单；
- 1.x CLR 可见性和现有产品契约保持不变；
- `no-public-match-found` 不等于没有消费者；
- 私有、未索引和未主动声明的消费者仍不可观察；
- 本 Issue 不弃用、收窄或删除任何类型；
- 本 Issue 不授权 G-04C；
- 窗口不会仅因日历时间到期而关闭；
- 最早关闭资格要求开启后至少完成一个非 prerelease 稳定发布周期、处理所有
  反馈并通过独立关闭审计。

Issue 创建时不添加 assignee、milestone 或 label，避免依赖不存在或语义不明的
仓库元数据。

## 6. 治理数据变更

### 6.1 Candidate manifest

只允许修改以下根级字段：

```text
declarationState: prepared-not-open -> open
feedbackChannel.issueNumber: null -> GitHub returned issue number
feedbackChannel.state: not-created -> open
windowPolicy.openedAtUtc: null -> GitHub returned createdAt
windowPolicy.announcementUrl: null -> GitHub returned issue URL
```

以下字段保持不变：

```text
candidateCount = 136
migrationTarget = 2.0.0
windowPolicy.minimumStableReleaseCycles = 1
windowPolicy.calendarTimeAloneIsInsufficient = true
windowPolicy.openRequiresSeparateApproval = true
windowPolicy.eligibleAfterRelease = null
```

`.candidates` 数组必须与 G-04B1 manifest 字节级或结构级完全一致。特别是全部
136 项继续保持：

```text
declarationStatus = consumer-declaration-pending
proposedAction = review-only
privateConsumerStatus = unknown-until-voluntary-declaration
```

### 6.2 Consumer declaration

声明文档从准备说明调整为当前窗口说明，并加入真实 Issue 链接、编号和开启
时间。仍保留 1.x 不变、无公开命中不等于删除安全以及 G-04C 未授权等内容。

### 6.3 G-04B1 evidence report

G-04B1 报告中的 136 项检索统计、误报复核和 `prepared-not-open` 历史结论不
重写。文件顶部增加明确的生命周期提示，末尾追加 `G-04B2 后续状态` 区块，
区分：

- G-04B1 完成时状态为 `prepared-not-open`；
- G-04B2 当前生命周期状态为 `open`；
- Issue 的真实编号、URL 和开启时间；
- G-04C 仍未获授权。

### 6.4 Active governance guide

活动 guide 将当前状态更新为 `open`，链接真实 Issue，并说明反馈字段、窗口
关闭资格和 `eligibleAfterRelease = null` 的原因。

### 6.5 Design and plan

新增本设计和对应实施计划，作为外部写操作、失败补偿、验证与审计的可追溯
依据。设计与计划不是产品 schema，也不改变窗口状态。

## 7. 数据流与发布顺序

```text
origin/main G-04B1 materials
  -> isolated worktree and fresh origin/main verification
  -> draft issue body and exact local change template
  -> local machine assertions and document checks
  -> user receives exact publication preview
  -> explicit final publication approval
  -> create GitHub Issue
  -> capture issue number, URL, createdAt
  -> write exact returned metadata into four governance files
  -> targeted verification and independent read-only review
  -> one publication commit after design/plan commits
  -> fast-forward-only push HEAD:main
  -> fetch Issue and origin/main files again
  -> verify remote state is consistent
```

不允许根据预计的下一个 Issue 编号、客户端时钟或未来版本号填写治理记录。

## 8. Git 与远端发布策略

实施在 `codex/g04b2-open-consumer-declaration` 独立 worktree 中完成。主工作树
当前未提交修改不属于本任务，不得暂存、提交、stash 或清理。

设计和实施计划先在本地分支提交，不触发外部发布。Issue 创建和状态文件更新
经过最终批准后，创建一个独立 publication commit。发布使用：

```text
git push origin HEAD:main
```

该 push 必须是 fast-forward；若 `origin/main` 已前进，停止并重新核对远端，
不得 force push、merge 未审查的远端变化或覆盖其他工作。成功后本地主工作树
可以暂时落后远端，不在本任务中强行更新带有未提交修改的 `main`。

## 9. 失败与补偿

### 9.1 Issue 创建前

认证、权限、重复 Issue、远端基线、候选对账、草稿检查或最终批准任一失败，
均不创建 Issue，不改变远端状态。

### 9.2 Issue 创建后、push 前

如果真实元数据写回、验证、提交、复审或 push 失败：

1. 使用 Issue 更新接口在正文顶部加入 `Opening paused` 说明和失败阶段；
2. 关闭 Issue；
3. 不把本地 `open` 状态提交视为已发布；
4. 保留本地证据，另行修复后重新申请恢复窗口；
5. 不删除或伪造原 Issue 历史。

关闭 Issue 是已批准发布流程的补偿动作，仅在 Issue 已创建而仓库状态无法完成
同步时使用。

### 9.3 push 成功后复核失败

如果 push 已成功但远端读取发现 Issue 或文件状态不一致：

1. 停止宣布 G-04B2 完成；
2. 优先修复不一致的治理元数据并重新验证；
3. 若无法立即恢复一致，标记并关闭 Issue；
4. 不回滚或重写 Git 历史，不 force push；
5. 建立明确的后续修复提交。

## 10. 验证与审计

### 10.1 创建前预检

- GitHub profile、仓库、权限和重复 Issue 查询成功；
- `git fetch origin main` 后 worktree base 与 `origin/main` 一致；
- manifest JSON 可解析，候选 136 个且 identity 唯一；
- 所有候选认证证据完整且无 `search-incomplete`；
- G-04A 六项缺席；`RouteInventoryInspectEntry` 恰好一项；
- Issue 正文无占位符、本地路径、虚构编号或敏感信息；
- 计划修改路径严格限定。

### 10.2 创建后机器断言

- Issue 处于 open；
- manifest Issue 编号、URL、时间与 GitHub 返回值完全一致；
- `.candidates` 与创建前快照完全一致；
- 只修改允许的根级状态字段；
- declaration、report 和 guide 使用同一个 Issue URL；
- `eligibleAfterRelease` 仍为 `null`；
- 不存在“已批准删除”“无外部消费者”或 G-04C 已授权等越界声明。

### 10.3 仓库门禁

执行：

```text
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/public-api-drift.sh check Release
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --no-restore
bash scripts/checks/post-change-targeted.sh -- \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md \
  guide/dev/public-api-governance.md \
  docs/superpowers/specs/2026-07-21-bukit-core-g04b2-consumer-declaration-opening-design.zh-CN.md \
  docs/superpowers/plans/2026-07-21-bukit-core-g04b2-consumer-declaration-opening.md
git diff --check
```

不运行 `ci-full`、release、`test-all`、`smoke-all` 或 whole-solution tests。

### 10.4 独立只读复审

这是公共兼容治理和外部写操作任务。publication commit 在 push 前必须经过一次
独立只读 aggregate review，至少检查：

- 用户批准范围与 Issue 正文一致；
- GitHub 返回元数据没有被改写；
- 候选数组和公共 API baseline 没有变化；
- 四个活动文件生命周期一致；
- 没有 Release、源码、schema、协议或 G-04C 漂移；
- 失败补偿仍可执行；
- 分支只含计划路径。

任何 Critical 或 Important finding 必须在 push 前关闭。

## 11. 完成条件

G-04B2 只有在以下条件全部满足时才可标记完成：

1. 专用 GitHub Issue 已创建且处于 open；
2. Issue 使用批准的英文主体和简短中文说明；
3. `origin/main` 中 manifest、声明、报告和 guide 指向同一个真实 Issue；
4. `declarationState = open`；
5. `feedbackChannel.state = open`；
6. `openedAtUtc` 等于 GitHub Issue `createdAt`；
7. `eligibleAfterRelease = null`；
8. 136 个候选记录、1.x 可见性和公共 API baseline 均未变化；
9. 所有门禁通过，独立只读复审无未解决 finding；
10. 推送后远端复核通过。

完成 G-04B2 不构成 G-04C 授权。窗口关闭、后续稳定版本识别、消费者反馈
处置和单类型 2.0 试点均必须另立任务。
