# Bukit Core AD-03C Notion 兼容清偿最终汇总关闭台账

> 日期：2026-07-24
>
> 父基线：`2.0@e16142331111060a09385fb29fdf72c28da260c4`
>
> C6 入场：`954a1fcb545605b3ebc4310fcf9cd6628e40dd4c`
>
> 范围：Bukit Core；Labs 与外部插件业务实现不在实施范围
>
> 状态：AD-03C 实现与治理范围正式关闭；父任务最终 aggregate/full 审计证据另行追加

## 1. 关闭结论

AD-03C0 到 AD-03C5 已按独立回退边界顺序完成并通过各自完整 owner suites 与独立只读
复审。C6 只收敛 governed baseline、active governance、migration notice 和正式台账，
没有新增 runtime 变更。

最终事实为：

- governed surface：**14 / 425 / 0**
  （14 assemblies / 425 public types / 0 `2.0-candidate`）；
- legacy public inventory：**19 -> 1**；
- 唯一保留项：
  `Bukit.Content.Notion.NotionPropertyParser` in `Bukit.Content.dll`，
  `retain-by-design`；
- 两个 internal test-only forwarding helpers 已删除；
- `Bukit.Shared -> Bukit.Notion` compatibility project reference 已删除；
- canonical `Bukit.Notion` graph、Notion transport/content behavior与 parser runtime
  均未在 C6 改动。

相对 C0 的精确汇总 delta：

| surface | aggregate delta |
|---|---:|
| public types | -18 |
| test-only helpers | -2 |
| compatibility project references | -1 |
| retained legacy public types | 1 |

## 2. 分任务关闭链

| 任务 | 提交 | 精确 delta | 完整测试 | 独立复审 | 状态 |
|---|---|---|---:|---|---|
| AD-03C0 | `38dbc0fb` + contract correction `7f700d99` | 冻结 19 项 inventory、原子边界、迁移最低合同；runtime 0 | 3036 | clean | closed |
| AD-03C1 | `fafed2bd` | 删除 2 个 internal test-only helpers；public 0 | 1403 | clean | closed |
| AD-03C2 | `ed226179` | 删除 Shared URL facade；public -1 | 1404 | clean | closed |
| AD-03C3 | `9ef16a6a` | 原子删除 Shared converter + 13 models；删除 mapper 与 Shared→Notion reference；public -14 | 2570 | clean | closed |
| AD-03C4 | `6d053a13` | 原子 internalize Content client/provider/options；public -3 | 2734 | clean | closed |
| AD-03C5 | `1caa0482` + review fix `954a1fcb` | parser retain-by-design；runtime/public count 0；classification 修正 1 | 734 | clean after fix | closed |

上述测试数是各任务完成时的 unfiltered Release owner-suite 计数，不可相加为 unique
test 数；相同 suite 在不同任务边界被重复验证。C6 自身的六套完整 owner-suite、
public API drift 和文档门禁结果记录在本台账第 6 节，并由任务报告保留原始命令证据。

## 3. Governed baseline 精确收敛

C6 在替换 baseline 前先运行 stale baseline check。结果只出现 18 条
`exported type removed`：`Bukit.Shared.dll` 15 项、`Bukit.Content.dll` 3 项；
没有 added type、member/signature、metadata 或其他 assembly drift。

随后用仓库 snapshot 工具在 system temporary directory 生成 candidate，并在内存中把它
与“C5 baseline 减去精确 18 项”作完整 JSON 语义比较。结果：

- assemblies：`14 -> 14`；
- public types：`443 -> 425`；
- added：0；
- removed set：精确 18；
- all retained entries：语义不变；
- parser 的 classification/compatibility/horizon：不变；
- semantic exact compare：true。

最终分类分布：

| dimension | distribution |
|---|---|
| classification | cross-assembly-implementation 256；implementation-public 41；serialized-contract 96；plugin-wire-contract 23；persisted-internal-format 6；aot-serialization-surface 3 |
| compatibility | 1.x-do-not-narrow 260；1.x-shape-stable 119；not-a-clr-contract 40；1.x-migration-safe 6 |
| migration horizon | 2.0-review 303；retain-1.x 122 |

## 4. 精确移除与保留

### `Bukit.Shared.dll`：15 项移除

1. `Bukit.Shared.Notion.BulletedListItemBlock`
2. `Bukit.Shared.Notion.CalloutBlock`
3. `Bukit.Shared.Notion.CodeBlock`
4. `Bukit.Shared.Notion.Heading1Block`
5. `Bukit.Shared.Notion.Heading2Block`
6. `Bukit.Shared.Notion.Heading3Block`
7. `Bukit.Shared.Notion.HtmlToNotionBlockConverter`
8. `Bukit.Shared.Notion.ImageBlock`
9. `Bukit.Shared.Notion.NotionApiUrls`
10. `Bukit.Shared.Notion.NotionBlock`
11. `Bukit.Shared.Notion.NumberedListItemBlock`
12. `Bukit.Shared.Notion.ParagraphBlock`
13. `Bukit.Shared.Notion.QuoteBlock`
14. `Bukit.Shared.Notion.RichTextSegment`
15. `Bukit.Shared.Notion.ToggleBlock`

### `Bukit.Content.dll`：3 项不再导出

1. `Bukit.Content.Notion.NotionApiClient`
2. `Bukit.Content.Notion.NotionContentProvider`
3. `Bukit.Content.Notion.NotionProviderOptions`

### 唯一保留项

`Bukit.Content.Notion.NotionPropertyParser` 继续由 `Bukit.Content.dll` public export，
元数据精确保持：

- classification：`implementation-public`；
- compatibility：`1.x-do-not-narrow`；
- migration horizon：`2.0-review`。

## 5. 迁移与不可知风险

正式 2.0 breaking/migration contract 见
[Bukit Core 2.0 Notion compatibility migration](../governance/bukit-core-2.0-notion-compatibility-migration.md)。
它要求 direct CLR consumer 更新 assembly reference、迁移 namespace/type、重新编译；
reflection、serializer 与 assembly-qualified binding 必须显式迁移；外部 legacy
`NotionBlock` subclass 没有机械迁移保证。Content 三类型 internalization 不表示
canonical adapter 已成为 drop-in 或 productized public SDK。

已检查范围没有发现 direct current-Core CLR consumer，但 private、unindexed、
binary-only、reflection、serializer、external subclass 与 undisclosed consumers
仍未知。该风险被版本边界和 migration notice 管理，不能宣称为零。

## 6. C6 验证状态

C6 的有效 TDD RED 为新 aggregate contract 6 项中 5 fail / 1 pass：失败原因精确为
stale 443 baseline、缺 migration notice、缺 closure ledger 与 active current-count
未收敛；immutable 136-entry manifest 检查已通过。

C6 aggregate contract 已 GREEN：6 passed / 0 failed / 0 skipped。六个
unfiltered Release owner suites 在同时清除 `NOTION_TOKEN` 与
`BUKIT_IMPORT_TEST_NOTION_TOKEN` 的环境中通过：

| project | passed | failed | skipped |
|---|---:|---:|---:|
| `Bukit.Shared.Tests` | 299 | 0 | 0 |
| `Bukit.Notion.Tests` | 376 | 0 | 0 |
| `Bukit.Content.Notion.Tests` | 6 | 0 | 0 |
| `Bukit.Content.Tests` | 456 | 0 | 0 |
| `Bukit.Engine.Tests` | 1628 | 0 | 0 |
| `Bukit.Architecture.Tests` | 278 | 0 | 0 |
| **total** | **3043** | **0** | **0** |

direct-owner checks 结果：

- `public-api-drift-self-test.sh`：passed；
- `public-api-drift.sh check Release`：passed，0 drift；
- `docs-consistency.sh`：passed；
- staged `git diff --check`：在 C6 提交前完成并由任务报告记录。

这些结果是 C6 direct-owner proof，不替代父任务的最终 aggregate/full 验证。

父任务授权的真实 aggregate targeted、full/release 与最终独立 aggregate 审计不在 C6
实现子任务内运行；它们必须以 C6 独立复审后冻结的 HEAD 为输入。任何后续 tracked 修复
都会使该父级证据失效并需要显式 replacement authorization。

## 7. 历史不可变证据

历史 136-entry candidate manifest 保持 closed：

- entries：136；
- Git blob：`7b07d6890562387010b52301e9f8716e9bf10ed1`；
- SHA-256：
  `d75710d9a7e4f006bdd83f9f583425b5efd9b0f9be17a35f48843b59ad78ea78`。

G-04 与 AD-01 历史 analysis/plan/spec/ledger 中的 443、136 和当时决议均未改写。
current surface 的唯一事实来源是
`docs/governance/bukit-core-public-api-baseline.v1.json` 与编译产物。

## 8. 结论边界

AD-03C 关闭的是 2.0 Notion legacy compatibility cleanup，不是一次新的 Notion
产品能力交付。它没有改变 API/TLS/retry/cache/schema、plugin protocol、assets、SEO、
global path tools、Labs 或外部插件业务实现。parser 的未来删除、internalization 或
public SDK productization 仍须另立任务并满足 C5 的无条件重审触发合同。
