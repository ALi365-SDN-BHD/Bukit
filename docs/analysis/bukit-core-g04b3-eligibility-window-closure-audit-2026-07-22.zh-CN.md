# Bukit Core G-04B3 Eligibility／消费者声明窗口关闭审计

日期：2026-07-22

本地源码基线：`main@c5926adf04ae064e9bbf04b440ef10229e3e331b`

公开 GitHub 基线：`main@b8bc7059fa9f1040d71e12cac1697c8cecac741a`

状态：`closure-evidence-complete / public-state-sync-required / separate-approval-required`

## 1. 执行结论

从**证据充分性**看，G-04B3 已具备提交窗口关闭决策的输入：窗口后稳定版本、
全部现有反馈分类、稳定版本后的 136 项认证公开搜索，以及该搜索的独立只读复审
均已完成。当前没有收到直接使用任一候选 CLR 类型的声明，也没有在受审公开范围
确认外部候选引用。

从**治理执行条件**看，现在仍不能关闭窗口：

1. [Issue #60](https://github.com/ALi365-SDN-BHD/Bukit/issues/60) 链接的 GitHub
   默认分支仍停在 `b8bc7059…`，本地 `main` 领先 15 个提交；认证搜索刷新、三项
   消费者分类以及 Notion 迁移后的候选方向尚未形成一致的公开治理状态。
2. `openRequiresSeparateApproval = true` 只记录打开声明窗口需要单独批准，不能反向
   解释为关闭政策。本任务只获授权审计，没有获授权设置 `eligibleAfterRelease`、
   改变 `declarationState` 或关闭 Issue；生命周期和外部状态变更仍需另行明确授权。

因此，本报告的判定是：**关闭证据集已经完整，但必须先同步公开治理状态并在同步后
重新读取反馈，再申请一次明确的窗口关闭授权。** 当前 governed lifecycle 必须保持：

- `declarationState = open`；
- `feedbackChannel.state = open`；
- `eligibleAfterRelease = null`；
- 136 项仍为 `consumer-declaration-pending`；
- G-04C 未获授权。

这不是“证据不足”，也不是“已经 eligible/closed”。它把证据 readiness、公开发布
一致性和外部写入权限三件事分开，避免用本地完成状态替代公开窗口状态。

## 2. 审计边界

本任务只新增计划与审计报告。没有修改：

- [540 类型公共 API 基线](../governance/bukit-core-public-api-baseline.v1.json)；
- [136 项候选 manifest](../governance/bukit-core-2.0-public-surface-candidates.v1.json)；
- [活动消费者声明](../governance/bukit-core-2.0-consumer-declaration.md)；
- Core、测试、schema、插件协议、持久化格式、访问级别或项目引用；
- Issue #60、评论、标签、发布或 GitHub 分支。

`no-public-match-found` 只表示受审认证公开搜索未发现匹配。私有、未索引、复制的
DLL/源码、反射、继承、序列化、Native AOT 或未自愿声明的消费者仍不可见。

## 3. 门槛逐项判定

| 门槛 | 当前证据 | 判定 |
|---|---|---|
| 消费者声明窗口有效打开 | Issue #60 于 `2026-07-21T02:19:46Z` 创建，审计时仍为 `open` | 满足 |
| 至少一个窗口后的非 prerelease 稳定版本 | `v1.0.10` 为 `draft = false / prerelease = false`，发布于 `2026-07-22T04:24:34Z` | 满足 |
| 全部已收到反馈完成分类 | 审计时共 2 条评论：一条 acknowledgement，一条 CLI/产品消费声明；均已分类且无候选级 CLR 使用声明 | 满足 |
| 稳定版本后认证搜索刷新 | 136 项于 `2026-07-22T05:16:15.308Z` 至 `05:24:16.126Z` 完成认证搜索，晚于稳定发布和最后一条评论 | 满足 |
| 搜索误报与截断已处置 | 1640 项误报完成处置；87 个达到 `topn = 20` 的主查询均有成功且未截断的上下文补充查询 | 满足 |
| 独立只读证据复审 | 搜索刷新复审为 C/I/M=`0/0/0`、Ready=Y | 满足 |
| 当前本地公共 API 无 drift | 恢复隔离工作树资产后，`public-api-drift-self-test.sh` 与真实 `public-api-drift.sh check Release` 均退出 0；build 0 warnings、0 errors | 满足 |
| 公开 GitHub 治理状态与本地状态一致 | GitHub 默认分支仍为 `b8bc7059…`，本地为 `c5926adf…`，相差 15 个提交 | **不满足** |
| 窗口关闭的明确授权 | 本任务范围明确为只读审计，未获得生命周期变更或外部写入授权；`openRequiresSeparateApproval` 只描述 opening approval，不作为关闭依据 | **不满足** |

前七项证明“可以提交关闭决策”，后两项阻止“现在执行关闭”。公开状态同步本身也
不能自动构成关闭授权。

## 4. 官方 GitHub 证据

### 4.1 Issue #60 与评论

认证 GitHub connector 与官方 REST API 在 `2026-07-22T06:19:24Z` 附近取得一致
结果：

| 对象 | 当前状态与处置 |
|---|---|
| Issue #60 | 标题为 `[G-04B2] Bukit Core 2.0 public surface consumer declaration`；`state = open`；`comments = 2`；`closed_at = null`；最后更新于 `2026-07-22T04:32:05Z` |
| 评论 `5041834957` | `2026-07-22T04:23:33Z`，正文 `ok`；分类为 non-consumer acknowledgement，不提供候选依赖证据 |
| 评论 `5041881389` | `2026-07-22T04:32:05Z`，声明 `silushangxun.com` 使用 Bukit；分类为产品/CLI 消费声明，不提供具体候选、程序集、反射、继承、序列化或 Native AOT 依赖 |

没有忽略任何现有评论。审计时点之后出现的新反馈不在本报告证明范围内，执行任何
关闭动作前必须重新读取并分类。

### 4.2 发布证据

官方 GitHub Releases REST API 检索时间为 `2026-07-22T06:19:11Z`：

| Release | draft | prerelease | published_at | 判定 |
|---|---:|---:|---|---|
| [`v1.0.10`](https://github.com/ALi365-SDN-BHD/Bukit/releases/tag/v1.0.10) | false | false | `2026-07-22T04:24:34Z` | 窗口后的合格稳定版本 |
| [`v1.0.10-rc.1`](https://github.com/ALi365-SDN-BHD/Bukit/releases/tag/v1.0.10-rc.1) | false | true | `2026-07-20T05:29:57Z` | prerelease 且早于窗口，不合格 |

`v1.0.10` 只满足稳定发布门槛，不单独证明无消费者，也不授权关闭或 G-04C。

## 5. 136 项认证搜索与候选一致性

本地 manifest 的当前结果：

- 136/136 `authenticated = true`；
- 136/136 `searchStatus = no-public-match-found`；
- 搜索窗口为 `2026-07-22T05:16:15.308Z` 至 `05:24:16.126Z`；
- 认证搜索发生在稳定版本发布及第二条评论之后；
- 候选 identity `(assembly, fullName)` 的规范化 SHA-256 为
  `b301745dad378c8b884855073c87c0925203db5799239d76172b31e4744d32f1`；
- 当前 540 类型基线中的 136 个 `2.0-candidate` 与 manifest identity hash 相同，
  集合差异为 0；
- Notion 两层迁移前后，`identity + externalEvidence` 投影 SHA-256 均为
  `741544c77d80d7255f3228b0a8459596dc672bfc7e3176f27c1e9d3e6f2fab16`。

因此，Notion 迁移没有改变或污染认证搜索证据。它改变的是后续处置方向：

| proposedAction | 数量 | 分布 |
|---|---:|---|
| `replace-with-bukit-notion` | 47 | `Bukit.Content` 31；`Bukit.Shared` 16 |
| `review-only` | 89 | 其余候选 |

136 项仍全部为 `consumer-declaration-pending`，私有消费者状态仍全部为
`unknown-until-voluntary-declaration`。`replace-with-bukit-notion` 是迁移方向，不是
弃用、删除或访问级别收窄授权。

## 6. 当前公共 API 基线演进

本地公共 API 基线现在有 540 项：

| compatibility | 数量 |
|---|---:|
| `1.x-do-not-narrow` | 239 |
| `1.x-migration-safe` | 6 |
| `1.x-shape-stable` | 119 |
| `2.0-candidate` | 136 |
| `not-a-clr-contract` | 40 |

相对先前 476 项基线，Notion 两层迁移新增 `Bukit.Notion` 62 项与
`Bukit.Content.Notion` 2 项，均为 1.x 保留面；136 候选数量和 identity 未变化。
这证明新的 canonical Notion 库没有偷偷扩大 G-04B3 候选集合，但 47 个旧 facade
必须在未来 G-04C 中逐类型提供迁移说明，不能整体删除。

## 7. 三个已声明消费者复核

本轮只读复核使用各项目当前工作树，不修改项目文件：

| 项目与 HEAD | 项目/源码证据 | 候选证据 | 分类 |
|---|---|---|---|
| SRBiz-bukit `bd975194…` | 无 C#/F#/VB 项目文件或源码；通过 `bukit build/config check/doctor` 和随仓库 Native AOT 可执行程序构建 `silushangxun.com`；工作树有 9 项既有未提交变化 | 打包的 Bukit Native AOT 主产品二进制包含 `ContentValidationIssue`、`RssGenerator.Post` 实现符号；两个插件二进制无候选完整名称命中。这是 Bukit 产品自身的载荷，不是站点源码或外部程序集对候选 CLR API 的引用 | confirmed CLI/config/theme/process-plugin consumer；无已确认直接 CLR candidate 引用 |
| sitegen `aaf2837c…` | 无项目/源码文件；携带 10 个 `SiteGen.*` 旧名称 DLL；workflow 用 `dotnet sitegen.dll build` 构建 `silkroute.cc` | 当前 136 个 `Bukit.*` 完整类型名匹配为 0；旧 `SiteGen.*` 二进制不能被解释成当前候选的全域无使用证明 | confirmed legacy process consumer；无已确认当前 Bukit CLR candidate 引用 |
| ALi365WebSiteBuilder `401b8791…` | 无 C#/F#/VB 项目文件或源码；workflow 执行 `./bukit build` 构建 `ali365.com.my` | 当前 136 个完整类型名匹配为 0；`.trae/skills` 中的 `using Bukit.*` 是文档示例且没有可编译项目，不构成 CLR 消费 | confirmed CLI/config/theme consumer；无已确认直接 CLR candidate 引用 |

这些项目证明 Bukit 的 CLI、配置、主题和进程边界是实际产品合同，应继续稳定；它们
不证明任一候选安全删除，也不要求把 136 项全部永久保留。

## 8. 本地与公开治理状态不一致

认证 GitHub commit 查询确认审计时公开默认分支 HEAD 为 `b8bc7059…`；本地 `main`
为 `c5926adf…`，领先 15 个提交。

| 项目 | 公开 GitHub `b8bc7059…` | 本地 `c5926adf…` |
|---|---|---|
| 公共 API 类型数 | 476 | 540 |
| 候选 identity | 136，hash `b301745d…` | 136，hash `b301745d…` |
| 候选动作 | 136 `review-only` | 89 `review-only`；47 `replace-with-bukit-notion` |
| 认证搜索时间 | `2026-07-20T14:39:17.454Z` 至 `14:46:22.692Z`，早于稳定版本 | `2026-07-22T05:16:15.308Z` 至 `05:24:16.126Z`，晚于稳定版本 |
| manifest SHA-256 | `b3e67a29ea9ff678aa82c9be097e8c9389d389e75ee97358ee745bd9818b348a` | `ab578905249ce8faf2769bb747a7c9cf67b91e799076733cbf3be98d401c9b75` |
| baseline SHA-256 | `6b74b0c1d2ffccd41bebbeb2634c4da1f25ad8fb875e4d5c5d2aa1656d001c34` | `1c1650698b4ef973a9e2a7fafaca80f17743f8c6ce1e55afd13ef357555bfe00` |

候选 identity 一致，说明公开用户看到的候选清单没有换人；但关闭依据、反馈分类和
迁移方向尚未公开一致。Issue #60 的 canonical 文档链接解析到 GitHub `main`，所以
不能在本地状态未发布时声称公开声明窗口已经完成一致性闭环。

## 9. 严格关闭判定与后续顺序

### 9.1 本轮判定

- release gate：通过；
- feedback disposition gate：通过；
- post-stable authenticated search gate：通过；
- independent search review：通过；
- local public API drift gate：通过；
- public governance convergence：未通过；
- separate closure approval：未取得。

所以窗口**继续 open**。本报告不修改 manifest，也不关闭 Issue。

### 9.2 推荐的受控后续顺序

1. 先合并本审计报告到本地 `main`。
2. 另行获得明确授权后，将包含搜索刷新、消费者分类、Notion 治理和本审计报告的
   本地 `main` 发布到 GitHub；不得把“本地已提交”当成公开发布成功。
3. 发布后验证 GitHub 默认分支 commit、manifest/baseline hash、活动声明链接与本地
   一致。
4. 再次读取 Issue #60 和全部评论；若出现新反馈，逐项分类并停止关闭流程，直到
   没有未处置消费者证据。
5. 建立独立的窗口关闭执行任务，明确决定是否以及如何更新 governed lifecycle 和
   Issue 状态；该任务必须获得外部写入与关闭的显式授权，并设计仓库／Issue 状态
   不一致时的恢复策略。
6. 即使窗口正式关闭，G-04C 仍须另立逐类型迁移与兼容性任务；47 个 Notion facade
   只能按 replacement mapping 评审，其余 89 项也不能按数量批量收窄。

## 10. 明确非结论

- 没有证明“不存在消费者”。
- 没有把三个 CLI/process 消费项目认定为 CLR SDK 消费者。
- 没有把产品二进制中的实现符号认定为站点的直接 CLR 引用。
- 没有设置 eligibility、关闭窗口、关闭 Issue 或批准 G-04C。
- 没有因 540 类型新基线而重开或扩大 136 候选集合。
- 没有建议在 1.x 中缩窄任何类型。
