# Bukit Core G-04D6 Rendering 决策汇总

> 日期：2026-07-23
>
> 任务：G-04D6 / master plan Task 21～24
>
> 状态：implementation-complete / group-verification-complete

## 1. 范围

G-04D6 只治理两个 `Bukit.Rendering` historical `2.0-candidate`：

1. `Bukit.Rendering.Scriban.FileTemplateLoader`
2. `Bukit.Rendering.Scriban.ScribanModelBinder`

用户边界是 Bukit Core-only。Labs 与外部插件不属于修复范围；本组没有修改它们。D6 也
没有修改 Theme、schema、插件协议、asset URL、全局路径工具、Scriban template syntax
或 public Rendering models。

## 2. 最终决策

| 类型 | Task 21 资格 | 当前终态 | 决策 |
|---|---|---|---|
| `FileTemplateLoader` | eligible internalize | `internal sealed class : ITemplateLoader` | D6A 仅收窄 type accessibility |
| `ScribanModelBinder` | eligible internalize | `internal static class` | D6B 仅收窄 type accessibility |

两项都是 2.0 source、binary 与 reflection breaking change。公开搜索未发现精确 Bukit
CLR consumer，但 private、未索引或未声明的 consumer 继续为
`unknown-until-voluntary-declaration`。

没有新增：

- `InternalsVisibleTo`；
- replacement public facade；
- type forwarding；
- public loader abstraction；
- reflection mapper；
- test-only production API。

## 3. D6A FileTemplateLoader 终态

Production 只发生：

```diff
- public sealed class FileTemplateLoader : ITemplateLoader
+ internal sealed class FileTemplateLoader : ITemplateLoader
```

以下保持不变：

- constructor；
- Scriban `ITemplateLoader` 的 `GetPath`、`Load`、`LoadAsync`；
- override → child/root → parent/fallback 优先级；
- 三层全缺失时返回 primary path；
- absolute/relative path 与 traversal 拒绝；
- sync/async missing-file empty string；
- `LastWriteTimeUtc + Length` cache signature；
- cache key comparison、exception type与文本；
- public `ScribanTemplateRenderer` 的 include/component/section dispatch。

Owner fixtures 补齐“override 已配置但目标缺失”继续命中 child/parent，以及 all-missing
返回 primary path。Architecture guard 固定 internal/sealed/not-exported、constructor、
interface map、friend set、current baseline 与 historical manifest。

实现与决议证据：

- `4635fc9de823fbf947f0d32c3014020d4c2c81bf`
- [G-04D6A decision ledger](bukit-core-g04d6a-file-template-loader-resolution-2026-07-23.zh-CN.md)

## 4. D6B ScribanModelBinder 终态

Production 只发生：

```diff
- public static class ScribanModelBinder
+ internal static class ScribanModelBinder
```

以下保持不变：

- facade 本身继续存在；
- `ToScriptObject(PageModel)` 与 `ToScriptObject(ListPageModel)`；
- 两个 expression bodies 与 `ScribanRootModelMapper` calls；
- public renderer 中的两个 direct static AOT roots；
- root/site/page/list/SEO/canonical trust/dynamic mapper graph；
- literal template keys、snake/camel aliases、derived-list aliases；
- `readOnly: true`；
- null 与 optional object 语义；
- read-only/mutable dictionary 和 `IEnumerable<object>` projection；
- unsupported object 的 `ToString()` fallback；
- `ToString()` exception propagation。

Owner fixtures 只补：

- Page/List facade 与 root mapper keys 等价；
- read-only dictionary；
- mutable-only dictionary；
- nested object list、null 与 blank key；
- unsupported custom object 不反射 member；
- custom `ToString()` exception 原样传播。

Architecture guard 固定 internal static shape、两个 overload、public renderer direct roots、
friend set、current baseline 与 historical manifest。

实现与决议证据：

- `d1ef937ba51445810a7072f83737953030726864`
- [G-04D6B decision ledger](bukit-core-g04d6b-scriban-model-binder-resolution-2026-07-23.zh-CN.md)

## 5. 公共面与历史证据

D6 开始前阶段值：

```text
14 assemblies / 488 public types / 62 candidates
```

D6A 后：

```text
14 assemblies / 487 public types / 61 candidates
```

D6B 后当前阶段值：

```text
14 assemblies / 486 public types / 60 candidates
```

current public API baseline 不再包含两个类型。所有现行 G-04 architecture current-count
guards、`docs/governance` 与 `guide/dev` 的当前计数已经同步到 `486/60`。

closed historical consumer manifest：

```text
candidateCount = 136
candidates.length = 136
Git blob = 7b07d6890562387010b52301e9f8716e9bf10ed1
```

两个 historical candidate entries 均保留，文件内容未修改。

## 6. 明确无范围漂移

D6 没有借公共面治理处理：

- symlink/realpath policy；
- case-sensitive filesystem policy；
- cache invalidation 算法；
- template key 重命名或 linter 对齐；
- generic value-type sequence 扩展；
- arbitrary safe-object projection SDK；
- public `ScriptObject` SDK；
- Engine/Theme dependency 重构；
- Native AOT/trimmer 配置；
- Labs 或插件行为。

这些相邻事项若需要变化，必须另立 Core 任务。

## 7. G3 验证集合

Task 21～24 的实现阶段没有单独运行 tests、aggregate、Native AOT 或独立复审；当时
D6 只能标记为 `implementation-complete`。Task 30 随后对完整 G3 diff 完成下列验证：

1. `Bukit.Rendering.Tests` 169/169 通过；
2. D6A fallback matrix 与 path safety 通过；
3. D6B Page/List、template keys、null/container/safe-object fixtures 通过；
4. `Bukit.Theme.Tests` 74/74、`Bukit.Engine.Tests` 1595/1595，证明旧/新主题路径、
   public renderer、friend access 和 render pipeline 不回归；
5. `Bukit.Architecture.Tests` 215/215，证明两项 internal shape、overloads/interface、
   friends、current baseline 与 historical manifest；
6. public API drift 通过，G3 最终 baseline 为 `14/484/56`；
7. 经明确批准的最终 replacement aggregate targeted gate 完整通过；
8. real Native AOT package 与 release artifact smoke 通过，证明 Scriban interface
   dispatch、binder direct roots 与 template projection 可达；
9. 独立轻量只读复审结果为 Critical/Important/Minor `0/0/0`，确认没有范围漂移。

最终值使用 Task 29 后的完整 G3 状态，没有把 D6 阶段值 `486/60` 固定为组终值。

## 8. 关闭条件

D6 只有在 Task 30 同时满足下列条件后才可关闭：

- fallback、path、cache、exception contract 无变化；
- template object shape、keys、aliases、null/container/fallback 无变化；
- public renderer 与 Engine/Theme 集成无回归；
- no new IVT/public shim/reflection fallback；
- Native AOT 与 aggregate gate 通过；
- 复审无 Critical/Important finding。

若任一条件失败，应回到对应 D6A 或 D6B 单类型任务复审；不能顺带修改 Labs、插件或相邻
Core 行为。
