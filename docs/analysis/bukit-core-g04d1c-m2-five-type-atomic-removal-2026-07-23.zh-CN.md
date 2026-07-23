# Bukit Core G-04D1C-M2：Content Notion five-type atomic 2.0 removal

日期：2026-07-23
基线：`2.0@f7b5bcf2fd9ad2deae71d90930bb7b286a8cc51c`
状态：实施中；最终状态以最新 handoff/controller 为准

## 1. 明确批准与原子范围

用户在 G-04D1C-M1 合并回本地 `2.0`、合并后定向验证通过且 M1 独立复审
达到 0 Critical / 0 Important / 0 Minor 后，明确批准进入 G-04D1C-M2。

批准只覆盖以下五个 `Bukit.Content.Notion` legacy renderer-extension CLR
identity 的 2.0 原子删除：

1. `Bukit.Content.Notion.INotionBlockRenderer`；
2. `Bukit.Content.Notion.NotionBlockTransformer`；
3. `Bukit.Content.Notion.NotionBlockRendererRegistry`；
4. `Bukit.Content.Notion.NotionRenderContext`；
5. `Bukit.Content.Notion.NotionBlocksRenderer`。

这五项构成同一个 callback / registry / context / renderer 扩展图。生产源码可以分布在
四个文件中，但删除提交必须同时移除全部五个 CLR identity，不得形成只删其中一部分的
可发布状态。

M2 不授权删除、internalize、重命名、obsolete 或修改以下类型的 public signature：

- `Bukit.Content.Notion.NotionApiClient`；
- `Bukit.Content.Notion.NotionProviderOptions`；
- `Bukit.Content.Notion.NotionClientStats`。

M2 也不授权修改 canonical transport、HTTP/retry、exception 语义、schema、plugin
protocol、CLI、config、asset URL、path tool、report contract、CI、release 或
verification policy。

## 2. Canonical replacement

五个已批准删除项的 canonical owner 均为 `Bukit.Notion`：

| legacy CLR identity | canonical CLR identity |
|---|---|
| `Bukit.Content.Notion.INotionBlockRenderer` | `Bukit.Notion.Rendering.INotionBlockRenderer` |
| `Bukit.Content.Notion.NotionBlockTransformer` | `Bukit.Notion.Rendering.NotionBlockTransformer` |
| `Bukit.Content.Notion.NotionBlockRendererRegistry` | `Bukit.Notion.Rendering.NotionBlockRendererRegistry` |
| `Bukit.Content.Notion.NotionRenderContext` | `Bukit.Notion.Rendering.NotionRenderContext` |
| `Bukit.Content.Notion.NotionBlocksRenderer` | `Bukit.Notion.Rendering.NotionBlocksRenderer` |

M1 已用可编译 fixtures 固化 namespace、client/options、callback、request semantics、
exception、cancellation 和 disposal 的迁移差异。M2 不通过 adapter 保留旧 full name，
也不向 canonical owner 重新引入 legacy `ContentException` 翻译。

## 3. 删除前新证据检查

检查基线为 M1 合并后的 `f7b5bcf2`。当前检查结果：

- 仓内 Core 主链、Labs 和官方插件未发现五个完整 legacy CLR identity 的新消费；
- M1 合并点以后生产目录无差异；
- `Bukit.Content` 中的生产引用仍只存在于待删除 compatibility graph 自身；
- M1 新增的 legacy 引用位于明确的迁移/架构 fixtures，不是产品消费者；
- SRBiz-bukit、sitegen 和 ALi365WebSiteBuilder 的既有证据仍只是 CLI、配置、主题、
  process 或随站可执行文件使用，不构成五个 CLR identity 的消费。

这些事实只支持当前仓内原子删除资格，不证明私有、未索引、未披露、reflection、
serialization、Native AOT、source generator 或 binary plugin consumer 不存在。

若删除提交前出现以下任一具体证据，必须停止直接删除，回到 retain/obsolete window：

- 可识别程序集实现 legacy `INotionBlockRenderer`；
- public/protected signature 暴露任一 legacy identity；
- delegate、reflection、serialization、Native AOT、source generator 或 binary
  plugin 绑定旧 full name/member signature；
- consumer 无法安全迁移 `context.Client.PostAsync` 的 request semantics；
- 同一个 binary plugin 必须同时在 1.x 与 2.0 运行。

## 4. 治理不变量

删除前 current public API baseline 为 14 assemblies / 514 types / 110
`2.0-candidate`；原子删除完成后，唯一允许的 current baseline 变化为
14 / 509 / 105。

闭合文件
`docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`
必须保持：

- 136 entries；
- `declarationState=closed`；
- 五项历史记录继续为 `consumer-declaration-pending`；
- private consumer 继续为 `unknown-until-voluntary-declaration`；
- public search 继续为 `no-public-match-found`；
- Git blob
  `7b07d6890562387010b52301e9f8716e9bf10ed1`。

闭合 manifest 是历史 cohort，不是 current CLR surface。M2 只更新 current public API
baseline、active governance 和本 removal ledger。

## 5. 测试迁移边界

仍通过 legacy wrapper 间接验证 canonical renderer 的分页、列表、registry、context
和 edge-case 测试迁移到 `Bukit.Notion.Tests`，并改为直接构造 canonical
`NotionClient` / `NotionBlocksRenderer`。

legacy `ContentException` 翻译和旧 client ownership fixtures 随 compatibility graph
删除，不复制到 canonical owner。M1 guide 继续保留为历史迁移说明，但不再作为
“legacy 类型仍必须存在”的 live invariant。

## 6. 验收边界

M2 完成前必须取得：

- Architecture、Content、Notion、Content.Notion 四个相关测试项目通过；
- public API drift self-test 与真实 Release check 通过；
- Core、Labs、plugins 三个跨边界 Release build 通过；
- current baseline 为 14 / 509 / 105；
- 闭合 manifest blob 不变；
- 一次 parent `post-change-targeted.sh` aggregate 通过；
- 完整 aggregate diff 独立只读复审为 0 Critical / 0 Important / 0 Minor；
- `git diff --check` 与范围审计通过。

本任务不预授权 full/release gate、`test-all`、`smoke-all`、Native AOT 或
release-artifact smoke。

## 7. Verification ledger

| 检查 | 当前证据 |
|---|---|
| M1 合并后定向验证 | Notion 27 / Content 15 / Architecture 8，全部通过 |
| M2 独立工作树基线 | Notion 27 / Content 15 / Architecture 8，全部通过 |
| 删除前仓内 CLR 搜索 | 未发现 compatibility graph 以外的完整 legacy CLR identity 生产引用 |
| closed manifest | 136 entries；blob `7b07d6890562387010b52301e9f8716e9bf10ed1` |
| Task 1 focused | 文档 focused check 通过 |
| canonical test ownership migration | focused：Notion 323 / Content 460，全部通过 |
| five-type atomic removal | focused：Architecture 119 / Content 460，全部通过 |
| current public API baseline | snapshot 已生成 14 assemblies / 509 types / 105 candidates |
| owner tests / cross-boundary builds | 待执行 |
| parent aggregate | 待执行 |
| independent whole-diff review | 最终状态由最新 handoff/controller 决定；本台账不冻结瞬时 review 状态 |
