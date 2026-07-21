# Bukit Core G-04B1 外部消费者声明准备与证据报告

> 生命周期说明：本报告的 G-04B1 证据快照截止于
> `prepared-not-open`；当前生命周期已由 G-04B2 更新为 `open`。下文
> G-04B1 的查询计数、证据状态、风险与历史结论均按当时快照保留。

日期：2026-07-20

状态：`prepared-not-open`

## 执行摘要

G-04B1 已把受治理公共 API 基线中的 136 个 `2.0-candidate` 类型整理为
机器可读候选清单，并为每个类型附加已认证、只读的 GitHub 公开代码检索
证据。候选集与基线精确对账，迁移目标为 `2.0.0`，136 项均保持
`consumer-declaration-pending / review-only`，1.x CLR 可见性没有因此改变。

本次候选级公开检索的最终状态均为 `no-public-match-found`。这只表示当前
已认证、可索引的公开检索面未发现经复核的外部消费证据，不表示不存在
私有、未索引或未主动声明的消费者。所有 136 项的私有消费者状态仍为
`unknown-until-voluntary-declaration`。

消费者声明材料目前只是本地准备工件。专用 GitHub Issue 的状态为
`not-created`，窗口没有开启；发布材料和打开窗口属于需单独批准的 G-04B2。
本报告不批准弃用、收窄或删除任何类型，也不授权 G-04C。

机器证据见 [2.0 公共面候选清单](../governance/bukit-core-2.0-public-surface-candidates.v1.json)，
消费者边界说明见 [2.0 消费者声明](../governance/bukit-core-2.0-consumer-declaration.md)。

## 范围与非目标

本次范围仅包括：从当前受治理基线派生候选身份、执行已认证 GitHub 只读
检索、复核公开命中、形成候选清单与声明准备证据，以及建立维护者入口。

本次没有修改 Core、Labs 或插件源码、测试、程序集引用、访问级别、CLR
签名、公共 API 基线、配置或报告 schema、插件协议、持久化格式或 asset
URL；没有创建或编辑 GitHub Issue、Pull Request、Discussion、Release、
分支、标签、评论或仓库文件，也没有推送本地提交。

`public` CLR 可见性不自动构成受支持的通用 Core SDK 承诺，但这也不构成
移除授权。受支持的 CLI、配置、主题、模板、报告和 `bukit-plugin-v1`
进程协议契约继续按各自现行规则治理。

## 136 项候选对账

候选身份唯一来源是
`docs/governance/bukit-core-public-api-baseline.v1.json` 中满足
`compatibility == 2.0-candidate` 的条目。manifest 内候选数组重算结果如下。

| Assembly | 候选数 |
|---|---:|
| `Bukit.Cli.Shared` | 5 |
| `Bukit.Content` | 35 |
| `Bukit.Engine` | 57 |
| `Bukit.PluginHost` | 16 |
| `Bukit.Rendering` | 2 |
| `Bukit.Routing` | 1 |
| `Bukit.Shared` | 17 |
| `Bukit.Theme` | 3 |
| **合计** | **136** |

| Owner | 候选数 |
|---|---:|
| `Build engine` | 57 |
| `CLI contract infrastructure` | 5 |
| `Content acquisition` | 35 |
| `External plugin host` | 16 |
| `Rendering and theme model` | 2 |
| `Routing` | 1 |
| `Shared foundation` | 17 |
| `Theme runtime` | 3 |
| **合计** | **136** |

G-04A 已纠正的类型没有重新进入候选清单。
`Bukit.Engine.RouteInventoryInspectEntry` 恰好存在一项；其候选级完整名称与
简单名称查询均返回 0，公开状态是 `no-public-match-found`，私有消费者仍
未知，并继续保持 `consumer-declaration-pending / review-only`。它没有被
提前批准进入 G-04C。

## 已认证 GitHub 检索方法

GitHub 认证资料查询成功后，对每个候选执行一个完整 CLR 名称查询和一个
simple type name 查询，均为公开跨仓库、只读检索，`topn` 固定为 20。
因此共有 272 个主查询：136 个完整名称查询和 136 个简单名称查询。

完整名称查询共返回 0 个文件；简单名称查询共返回 1870 个文件。凡主查询
返回 20 个结果就视为可能截断，共有 87 个。每个截断项都执行了一个成功、
未截断的 namespace 或 assembly 上下文补充查询；补充查询共 87 个，其中
`narrow-context` 为 64 个、`truncation-resolution` 为 23 个，合计返回 2 个
文件。候选级查询最终没有 `search-incomplete`。

连接器只提供已索引公开文件的仓库、路径和 URL 元数据，不提供私有仓库
可见性，也不能证明未索引代码不存在。命中复核因此结合了完整名称、简单
名称、namespace/assembly 上下文、仓库身份、文件路径和依赖语境，而不是
仅依据返回数量或同名词作结论。

## 外部命中与误报复核

manifest 按固定状态词汇重算如下。

| 公开检索状态 | 数量 | 本次含义 |
|---|---:|---|
| `no-public-match-found` | 136 | 已认证公开查询成功，未发现经复核的外部命中 |
| `owner-repository-only` | 0 | 没有候选仅剩 Bukit 自身仓库命中 |
| `fork-or-mirror-observed` | 0 | 没有观察到可归类为 fork、镜像或复制源码的候选 |
| `external-match-needs-review` | 0 | 没有尚待澄清的可能外部消费者 |
| `confirmed-external-consumer` | 0 | 没有确认的外部消费者 |
| `search-incomplete` | 0 | 没有未完成候选检索 |

简单名称及补充查询产生的命中经过逐仓库和路径复核，共记录 1857 个误报
排除条目，分布在 106 个候选上。两个非零补充命中分别是：

- `Bukit.Theme.SchemaValidationError` 的同所有者 BukitJalil 文档措辞命中；
  它不是 CLR 类型消费，完整名称查询为 0。
- `Bukit.Theme.ThemeDoctorCommand+DoctorResult` 的无关牙科 PHP 应用词义碰撞；
  它没有 Bukit 类型或依赖语境。

因此，确认外部消费者、可能外部命中和 fork/镜像的精确列表在本次均为空。
这三个空列表只描述本次公开证据，不应改写为“没有外部消费者”。

## 仓库级依赖信号

仓库级证据执行了 5 个已认证主查询和 7 个补充查询。`Bukit.Engine`、
`Bukit.Content` 与 `Bukit.PluginHost` 主查询均返回 0；两个 Bukit 仓库 URL
查询各返回 20 并被标记为截断。针对 URL、`PackageReference`、
`.gitmodules`、`uses` 和 assembly 引用语境的 7 个补充查询全部未截断。

仓库级复核涉及 `ALi365-SDN-BHD/BukitJalil` 和
`ALi365-SDN-BHD/ali365-sdn-bhd.github.io`。前者只出现 README 链接或引用，
后者只出现已生成 HTML 链接或引用；补充检索没有返回 package、源码、
submodule、workflow 或 dependency manifest 证据。因此两者均被排除为依赖
消费者，仓库级确认外部消费者列表为空。

这些同所有者或发布站链接没有被强行归入候选级
`owner-repository-only`：该状态要求候选类型搜索本身只留下 Bukit 自身命中，
而本次候选级复核没有这种记录。仓库链接信号与类型消费信号保持分层。

## 私有消费者与证据限制

以下未知项不能由本次公开检索关闭：

- 私有仓库、企业内网源码和未主动声明的消费者不可见；
- GitHub 索引延迟或未索引文件不在当前证据面内；
- 连接器不返回代码片段，判断依赖完整名称、上下文补充、仓库与路径复核；
- 反射、序列化器、Native AOT、继承和公共签名传播可能不会形成直接类型名
  命中；
- 公开搜索未命中不能证明删除安全，也不能替代主动消费者声明窗口。

因此所有 136 个候选都保留
`privateConsumerStatus = unknown-until-voluntary-declaration`。任何后续发现的
外部使用都必须停止把对应类型视为表面零消费者，并转入保留、facade 迁移
或单独审核的 obsolete 路线。

## 声明窗口状态

当前声明状态严格为 `prepared-not-open`，目标版本为 `2.0.0`。专用 GitHub
Issue 尚未创建，Issue 编号、打开时间、公告 URL 和可评估稳定版本均为空。
现有文档是准备材料，不是公开征集通知。

打开窗口需要另行批准 G-04B2，由该任务发布材料、创建专用 Issue，并写入
真实编号、URL 和开始时间。日历时间本身不能关闭窗口；窗口开启后还必须
至少经过一个后续稳定发布周期、分类处理全部反馈并完成独立证据复审，才能
讨论某个类型是否具备 G-04C 评估资格。具备讨论资格也不等于批准收窄。

## 风险与处置

| 风险 | 当前控制 | 后续处置 |
|---|---|---|
| 把公开零命中解释为零消费者 | 明确保留私有状态未知 | G-04B2 开启主动声明渠道 |
| 同名词误报污染结论 | 记录排除项并执行 namespace、assembly 与依赖语境复核 | 新证据出现时按候选重新分类 |
| 截断查询制造假阴性 | 所有 87 个截断主查询都有未截断补充查询 | 后续发布前复查索引和查询限制 |
| 公共类型被过早收窄 | 1.x 可见性不变，全部为 `review-only` | 任何变更另立 major-version 决策 |
| 声明材料被误认为已发布 | 状态固定为 `prepared-not-open`，Issue 为 `not-created` | 只有经批准的 G-04B2 可打开窗口 |
| 特殊试点身份被提前放行 | `RouteInventoryInspectEntry` 仍为 pending | 等完整声明周期后再独立评估 |

## 验证记录

- 候选数组重算为 136 个唯一身份；assembly、owner 和状态计数分别合计为
  136。
- 候选级查询从 manifest 重算为 272 个主查询和 87 个补充查询；87 个截断
  主查询均有未截断补充证据，`search-incomplete` 为 0。
- 误报排除项重算为 1857，确认外部、可能外部和 fork/镜像列表均为空。
- 仓库级证据为已认证只读查询：5 个主查询、7 个未截断补充查询、0 个确认
  外部消费者。
- `RouteInventoryInspectEntry` 唯一且仍为
  `consumer-declaration-pending / review-only`；全部 136 项私有消费者状态
  仍为未知。
- 报告编写只使用临时工件中从 manifest 与仓库级证据推导的统计；没有以
  手工估算替代机器对账。

G-04B1 的公共面 drift、Architecture Tests、聚合定向门禁与独立只读复审由
Task 6 统一执行。本节不预先宣称这些后续检查已经通过。

## G-04B2 前置条件

申请 G-04B2 前必须满足以下条件：

1. G-04B1 的 manifest、声明、报告和 active guide 通过聚合定向门禁与独立
   只读复审，且没有未关闭 finding。
2. 外部写操作获得单独、明确批准；当前本地准备不构成该授权。
3. 含声明材料的提交先按批准流程公开可访问，再创建专用 GitHub Issue。
4. Issue 创建后把真实编号、URL、打开时间和公告位置回写治理记录，不使用
   推测值。
5. 继续保持 1.x 访问级别和受支持契约不变；任何新消费者证据先分类处置。

G-04B2 仅负责发布并打开声明窗口。它不自动启动 G-04C；只有在窗口开启、
至少完成一个后续稳定发布周期、所有反馈处置完毕并通过独立审计后，才能
另行申请单类型的 2.0 兼容性决策。

## G-04B2 后续状态（2026-07-21）

G-04B2 已于 `2026-07-21T02:19:46Z` 打开消费者声明窗口。专用反馈渠道为
[GitHub Issue #60](https://github.com/ALi365-SDN-BHD/Bukit/issues/60)，当前
生命周期状态为 `open`，`eligibleAfterRelease = null`，需等待至少一个后续
非预发布正式稳定版本后才可能设置。

候选集合仍为原有 136 项，全部保持
`consumer-declaration-pending / review-only`；1.x CLR 可见性和受支持产品
契约均未改变。公开检索无命中仍不是删除安全证明，窗口开启也不批准弃用、
收窄或删除任何类型，不授权 G-04C。
