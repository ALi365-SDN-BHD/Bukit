# Bukit Core AD-03C5 NotionPropertyParser 保留决议

> 日期：2026-07-24
>
> 入场提交：`6d053a1359b8a8da1f7122ec827e971ddf411868`
>
> 范围：Bukit Core 治理合同；不修改 parser runtime
>
> 决议：`retain-by-design`

## 1. 决议

`Bukit.Content.Notion.NotionPropertyParser` 继续作为
`Bukit.Content.dll` 中的 public static implementation facade 存在。本任务不删除或
internalize 该类型，不修改其实现或签名，也不创建新的 public canonical
replacement。

治理元数据确认为：

| 字段 | 决议值 |
|---|---|
| `classification` | `implementation-public` |
| `compatibility` | `1.x-do-not-narrow` |
| `migrationHorizon` | `2.0-review` |

`implementation-public` 表示 CLR 可见的实现细节，不将该 facade 提升为通用 CLR
SDK 承诺。保留现有 visibility 则避免在没有迁移出口和消费者全量证据时制造新的
source、binary 或 reflection break。

## 2. 冻结的 assembly identity 与 API shape

保留合同精确为：

- assembly：`Bukit.Content.dll`；
- CLR identity：`Bukit.Content.Notion.NotionPropertyParser`；
- type shape：public static class；
- public declared methods 精确为：
  - `ExtractFields(JsonElement)`；
  - `ExtractAllFields(JsonElement)`；
- 两个方法均返回
  `IReadOnlyDictionary<string, Bukit.Engine.Abstractions.Content.ContentField>`。

本决议不把现有 internal methods 转为 public，也不改变支持的 Notion property
types、解析顺序、字段投影、异常或返回值行为。

## 3. 为什么当前不具备删除或收窄资格

canonical `Bukit.Content.Notion` adapter 是 non-packable monorepo Core component，
当前 no public canonical replacement 能承担上述 facade 的同用途迁移合同。它已有的
`NotionContentPropertyParser` 与 `NotionPropertyTypeParser` 都是 internal
implementation identities。把 legacy facade 删除或 internalize 会留下 public
消费者无明确 public 迁移目标；把 canonical implementation 临时公开则会未经
productization 审计制造新的 SDK surface。

因此，“仓库内没有发现 production caller”只能支持继续观察，不能单独授权 breaking
change。当前选择 retain-by-design，而不是从 public type 数量目标反推收窄。

## 4. 消费者证据边界

C0 对 Core、Labs、仓库内插件、已知本地 site repositories 和公开 GitHub 网页搜索的
调查没有发现 direct current-Core CLR consumer。该证据不能覆盖：

- private 或 unindexed repositories；
- binary-only consumer；
- reflection、assembly-qualified type name 或 serializer binding；
- 未披露的源码或部署内 consumer。

因此本台账不宣称“没有 consumer”或“保留没有兼容风险”。它只确认在已检查范围内没有
出现足以推翻本次保留结论的新证据。

## 5. 未来重审触发条件

任何以下事件都无条件启动单独重审，不附加预先筛选：

1. Any real security or correctness defect starts a re-review.
2. Any direct consumer declaration starts a re-review.
3. Any separately approved CLR SDK productization decision with a migration and versioning plan starts a re-review.

重审启动后，才评估缺陷能否兼容修复、consumer evidence 是否改变迁移结论，以及
productization 是否应包含 public replacement。上述评估内容不是阻止三类事件启动重审的
前置条件。重审还须验证 assembly identity、binary/reflection 风险、消费者迁移路径和
版本策略。
不得以搜索无结果、文件行数、public type 数量或 canonical internal implementation
已经存在为由静默改变可见性。

## 6. 受控影响

本任务唯一治理数据变化是该 baseline entry 的
`classification: cross-assembly-implementation -> implementation-public`。
`compatibility`、`migrationHorizon`、entry 数量、public surface、runtime source、
project references、friend assemblies、package metadata 与历史 136-entry candidate
manifest 均保持不变。AD-03C2 至 C4 的 stale baseline entries 由 C6 aggregate
convergence 处理，本任务不顺带清理。

## 7. 残余风险

未知 private、binary-only、reflection 和 undisclosed consumers 仍不可观测。这一风险
通过保留原 assembly identity 与 public method shape 被控制，但没有被证明为零。
若未来执行删除、internalize 或 public SDK productization，必须另立迁移与版本化任务，
不能把本 C5 retain-by-design 决议解释成预授权。
