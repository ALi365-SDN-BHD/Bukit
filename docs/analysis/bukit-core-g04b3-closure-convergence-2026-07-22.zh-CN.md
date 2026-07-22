# Bukit Core G-04B3 关闭收敛记录

日期：2026-07-22

状态：`local-lifecycle-documentation-converged / public-convergence-pending-merge-and-publication`

## 1. 不可变执行记录

本记录只收敛版本控制中的 G-04B3 生命周期与其人类可读投影。它不重写历史
[eligibility／消费者声明窗口关闭审计](bukit-core-g04b3-eligibility-window-closure-audit-2026-07-22.zh-CN.md)，
也不改变候选层级状态、Core、公开 API 基线、schema、协议、序列化格式、访问级别或项目引用。

| 项目 | 已观察值 |
|---|---|
| local base | `main@82485e4efef5357c5560733c0dc3e758f0b93eaf` |
| remote main observed | `3ceb096a3ae2cdff145a49798460671261968b04` |
| Issue #60 | `closed_at = 2026-07-22T07:08:30Z`；close event = `2026-07-22T07:08:31Z`；actor = `ClrsDream` |
| v1.0.10 | `draft=false`；`prerelease=false`；`published_at=2026-07-22T04:24:34Z` |
| comments | 2；均已分类；没有声明任何候选层级 CLR reference |
| scope | 仅 lifecycle/documentation convergence |

## 2. 受控生命周期变更

候选 manifest 只使用既有字段完成以下转换：

| 字段 | 关闭后值 |
|---|---|
| `declarationState` | `closed` |
| `feedbackChannel.state` | `closed` |
| `windowPolicy.eligibleAfterRelease` | `v1.0.10` |

未加入 `closedAtUtc`、新字段或新 schema version。136 项候选的身份、分类、兼容性、
迁移 horizon、proposed action、外部搜索证据、`declarationStatus` 和
`privateConsumerStatus` 都不在本次变更范围内。

候选保护投影的 SHA-256：

| 时点 | SHA-256 |
|---|---|
| 修改前 | `764b48edb51c126b9925e88b02e6d23844457c8819d6c57886c370b351711b56` |
| 修改后 | `764b48edb51c126b9925e88b02e6d23844457c8819d6c57886c370b351711b56` |

## 3. 人类可读投影

| 文件 | 变更范围 |
|---|---|
| `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json` | 三个既有生命周期字段 |
| `docs/governance/bukit-core-2.0-consumer-declaration.md` | `closed` 状态、Issue close event、`v1.0.10`、历史反馈说明与授权边界 |
| `guide/dev/public-api-governance.md` | 关闭生命周期、发布与 Issue 证据、私有消费者限制和 G-04C 边界 |
| 本文件 | 本地收敛的不可变执行记录 |

所有 136 项仍为 `consumer-declaration-pending`，所有 private-consumer status
仍为 `unknown-until-voluntary-declaration`。`no-public-match-found` 不能证明
私有、未索引或未自愿声明的消费者不存在；新证据必须在单独打开的 channel 或 task
中处理，而不是写入已经关闭的 Issue。

## 4. 授权与发布边界

所有 1.x CLR visibility 保持不变。关闭生命周期只允许讨论 G-04C eligibility；
G-04C 未由本任务授权，仍需要单独、单类型、仅 2.0 的决策。

本地收敛不等于公开收敛。只有变更合并并发布后，才能验证远端 manifest 和人类可读
投影的 hash/内容与本地一致；在此之前不得将本记录表示为 public convergence。
