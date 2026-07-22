# Bukit Core G-04B3 消费者声明窗口检查点

日期：2026-07-22

源码基线：`main@b8bc7059fa9f1040d71e12cac1697c8cecac741a`

状态：`post-stable-evidence-refresh-required`

## 1. 执行结论

**G-04B3 尚未产生资格、窗口关闭或 G-04C 授权。** 声明窗口之后的一个
非 draft、非 prerelease 稳定版本已经存在，故本检查点不再是
`waiting-stable-release`；但 136 个候选的认证公开代码检索尚不能刷新，且
Issue #60 的每条反馈仍须保留、分类和复核。因此状态是
`post-stable-evidence-refresh-required`，不是 `eligible`。

- `v1.0.10` 于窗口打开后以稳定版本发布，满足“至少一个后续稳定发布周期”
  的必要条件；它只解除该单一前提，不能单独关闭窗口。
- `v1.0.10-rc.1` 是 GitHub 标记的 prerelease，且发布时间在窗口打开之前；
  它不合格。任何窗口前发布同样不合格。
- 当前可用的 136 项公开检索记录来自 2026-07-20 的认证快照。当前环境没有
  `GH_TOKEN` 或 `GITHUB_TOKEN`，匿名 API 剩余额度为 0，不能把旧的零公开
  命中刷新成当前结论。
- Issue #60 当前有 1 条评论，而非“无评论”。`ClrsDream` 的 `ok` 按其字面
  内容分类为**非消费者依赖 acknowledgement**；该评论保留为已接收反馈，
  不删除、不忽略，也不证明不存在消费者。
- 自愿声明称 `silushangxun.com`、`silkroute.cc` 与 `ali365.com.my` 使用
  Bukit 构建。对应 `silushangxun.com` 的 SRBiz-bukit 仓库已有独立只读证据，
  确认为 CLI/config/theme/process-plugin 消费者；其审计范围内未观察到直接
  `Bukit.*` CLR 引用。另两个站点仍只有自愿声明。三者都不提供 136 个候选的
  CLR 使用细节，不能据此授权或否决任一具体候选。

`no-public-match-found`、零公开命中或日历时间都**不是**无消费者证明：私有、
未索引或未自愿披露的消费者仍不可见。治理资料的 containment（136 项仍为
`consumer-declaration-pending / review-only`，且 1.x 访问级别未变）也不等于
实际 public surface 已收窄。

## 2. 范围和不变量

本报告只记录检查点证据。没有修改 [476 类型基线](../governance/bukit-core-public-api-baseline.v1.json)、
[136 候选 manifest](../governance/bukit-core-2.0-public-surface-candidates.v1.json)、
活动消费者声明、Core、测试或 GitHub 外部状态。

本报告不：

- 关闭、评论、重新标记或以其他方式变更 [GitHub Issue #60](https://github.com/ALi365-SDN-BHD/Bukit/issues/60)；
- 设置 `eligibleAfterRelease`、关闭窗口、收窄/弃用/删除任一类型；
- 将稳定版、评论、公开检索或本地治理 containment 解释成 G-04C 授权。

## 3. 本地治理与 Git 基线证据

检索工作树为 `main@b8bc7059fa9f1040d71e12cac1697c8cecac741a`：`HEAD`、
`main`、`origin/main` 和 `merge-base(HEAD, main)` 均为此提交。

| 检查项 | 当前结果 |
|---|---|
| 基线 schema / 类型数 | `bukit-core-public-api-baseline-v1` / 476 |
| 兼容性计数 | `1.x-do-not-narrow` 175；`1.x-migration-safe` 6；`1.x-shape-stable` 119；`2.0-candidate` 136；`not-a-clr-contract` 40 |
| 候选 manifest | 136 项；`declarationState = open`；全部 `consumer-declaration-pending / review-only` |
| 候选逐项 identity 对账 | 对基线 `compatibility == 2.0-candidate` 与 manifest 的 `(assembly, fullName)` 分别排序为 TSV；均为 136 行，SHA-256 都是 `b301745dad378c8b884855073c87c0925203db5799239d76172b31e4744d32f1`，`comm -3` 差异为 0 |
| manifest `windowPolicy` | `openedAtUtc = 2026-07-21T02:19:46Z`；`minimumStableReleaseCycles = 1`；`calendarTimeAloneIsInsufficient = true`；`openRequiresSeparateApproval = true`；`eligibleAfterRelease = null`；公告为 Issue #60 |

### 3.1 472 到 476 的演进不重开候选集

manifest 的 `sourceBaseline` 仍可追溯到 `eb068fa3…` 的 472 类型快照；当时
136 个 `2.0-candidate` 已确定。当前 476 类型基线来自
`ea66c1efcacb71bd41d814608ab10d27303fc7c8`，新增的四项均为
`Bukit.Config` 的 `serialized-contract / 1.x-shape-stable / retain-1.x`：

- `AnalyticsConsentConfig`
- `AnalyticsCspConfig`
- `AnalyticsGoogleConsentConfig`
- `AnalyticsGoogleConsentDefaultsConfig`

因此变化是 472→476，`1.x-shape-stable` 为 115→119；`2.0-candidate` 保持 136，
上述 identity 对账也证明候选集合没有被重新打开或扩大。

### 3.2 漂移门禁证据边界

同一 `main@b8bc7059` 的控制方新鲜证据显示 public-API drift 检查 exit 0。本工作树
运行 `bash scripts/checks/public-api-drift.sh check Release` 未能独立完成，原因是
`--no-restore` 所需的 `project.assets.json` 缺失（`NETSDK1004`）；这属于隔离工作树
的环境准备不足，不是 drift 失败，也没有为此 restore 或修改任何代码。

## 4. 官方 GitHub REST API 证据

以下当前 Issue 与 release 状态只取自官方 GitHub REST API。检索时间为
**`2026-07-22T04:29:23Z`**；请求为只读的
`GET /repos/ALi365-SDN-BHD/Bukit/issues/60`、
`GET /repos/ALi365-SDN-BHD/Bukit/issues/60/comments?per_page=100` 与
`GET /repos/ALi365-SDN-BHD/Bukit/releases?per_page=100`，使用
`Accept: application/vnd.github+json` 和 API version `2022-11-28`。

| 对象 | API 快照结果 |
|---|---|
| Issue #60 | 标题与声明一致；`state = open`；创建于 `2026-07-21T02:19:46Z`；`comments = 1`；未关闭 |
| 唯一评论 | `ClrsDream`，`2026-07-22T04:23:33Z`，[`ok`](https://github.com/ALi365-SDN-BHD/Bukit/issues/60#issuecomment-5041834957)；分类为非消费者依赖 acknowledgement，保留待后续审计复核 |
| 合格稳定发布 | [`v1.0.10`](https://github.com/ALi365-SDN-BHD/Bukit/releases/tag/v1.0.10)：`draft = false`、`prerelease = false`、`published_at = 2026-07-22T04:24:34Z`；在窗口打开后 |
| 不合格 RC | [`v1.0.10-rc.1`](https://github.com/ALi365-SDN-BHD/Bukit/releases/tag/v1.0.10-rc.1)：`prerelease = true`，`published_at = 2026-07-20T05:29:57Z`；既是 RC 又在窗口前 |

控制方还在 GitHub 官方 release 页面复核了同一 `v1.0.10` tag 与发布时间；本检查点的
Issue/release 状态判定以上表 REST 快照为准。

这份快照只证明一个所需的稳定发布周期已经出现。它没有提供私有消费者可见性、
下载/采用量，也不将 Issue 评论或公开搜索变为无消费者证明。

## 5. 自愿消费者声明（独立证据源）

用户自愿声明 `silushangxun.com`、`silkroute.cc` 和 `ali365.com.my` 使用 Bukit
完成构建。此项独立于 Issue #60 与公开代码搜索；它强化了 1.x 产品契约保留，
但本身不改变 CLR candidate 的兼容性结论。处置如下。

| 反馈对象与证据范围 | 已确认 | 候选级证据与限制 | 后续所需补证 |
|---|---|---|---|
| `silushangxun.com` / SRBiz-bukit 只读审计 | `site.yaml` 指向该站点；README/scripts 调用 `bukit build`、`config check`、`doctor`；shell launcher 执行 arm64 Native AOT `bukit/bin/bukit`。审计范围内无 `.csproj`、`.sln`、`.slnx`、`.dll`、`ProjectReference`、`PackageReference` 或 `Bukit.*` CLR 调用；分类为 confirmed CLI/config/theme/process-plugin consumer。 | `none-observed-in-audited-repo`：未观察到 136 候选、程序集引用、反射、继承、序列化或 Native AOT CLR 细节；这不是所有私有代码均无直接 CLR 使用的证明。 | 若要主张候选级无使用或有使用，仍须提供直接 CLR 消费证据或明确覆盖范围。 |
| `silkroute.cc` 与 `ali365.com.my` / 用户自愿声明 | 声明使用 Bukit 构建。 | `unresolved-usage-mode`；没有项目级证据，不能从站点源码推断其依赖，也没有 136 候选或 CLR 使用细节。 | 明确运行的是 `bukit` CLI 二进制，还是直接引用 `Bukit.*` CLR 程序集/类型；若为后者，列出准确程序集、类型和使用方式。 |

这些记录都不是 `RouteInventoryInspectEntry` 或其他任何具体候选被消费的证据，
也不是未被消费的证据。因此它们既不能授权也不能否决任何具体候选；它们必须保留
在后续反馈处置中。

## 6. 仍缺的刷新与恢复条件

2026-07-20 的 manifest 曾对全部 136 项使用认证检索并记录
`no-public-match-found`，但这些是历史证据。此检查点时 `GH_TOKEN` 与
`GITHUB_TOKEN` 均 unset，匿名 API rate remaining 为 0；因此不能执行新的、
可认证的 136 项 GitHub code-search 刷新。

要从 `post-stable-evidence-refresh-required` 恢复为可供独立审计的输入，后续
单独授权任务必须：

1. 在有授权且可用的认证 GitHub 搜索环境中，重新检索全部 136 项，并逐项保存
   查询、时间、结果、假阳性排除和仍然存在的私有可见性限制；不得用旧快照或
   匿名零额度替代。
2. 保留并复核 Issue #60 全部反馈；`ok` 作为 non-consumer acknowledgement
   仍要显示在证据中。SRBiz-bukit 的已确认 CLI/config/theme/process-plugin
   消费须保持其候选级限制；`silkroute.cc` 与 `ali365.com.my` 也必须先补齐
   usage mode。任何实质消费者报告必须逐类型分类和处置。
3. 将稳定发布、刷新后的公开证据和反馈处置交给独立只读复审。该复审才能判断
   是否有足够输入讨论 `eligibleAfterRelease`，而不是自动设置它。

即使上述条件满足，G-04C 仍须单独的、明确的兼容性决策与访问级别变更授权；
本报告**不能授权 G-04C**。

## 7. 检查点结论

| 生命周期问题 | 结论 |
|---|---|
| 窗口已打开？ | 是；Issue #60 仍 open。 |
| 后续稳定发布前提已满足？ | 是；`v1.0.10` 合格。 |
| RC 或窗口前发布可替代它？ | 否。 |
| 136 项当前外部消费者证据已刷新？ | 否；认证刷新被未配置 token 与匿名 rate limit 阻断。 |
| 自愿构建消费者声明已完成候选级分类？ | 否；SRBiz-bukit 为 `none-observed-in-audited-repo`（非全域不存在证明），另外两个为 `unresolved-usage-mode`。 |
| 可关闭窗口、标记 eligible 或授权 G-04C？ | 否。 |
| 当前状态 | **`post-stable-evidence-refresh-required`**。 |
