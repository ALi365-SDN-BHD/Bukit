# Bukit Core G-04D6B ScribanModelBinder 受控收窄决议

> 日期：2026-07-23
>
> 范围：G3 Task 23；只处理 Bukit Core
>
> 基线：`4635fc9de823fbf947f0d32c3014020d4c2c81bf`
>
> 状态：implemented / group-verification-pending

## 1. 决议

G-04D6B 只把：

```text
Bukit.Rendering.Scriban.ScribanModelBinder
```

从 public static class 收窄为 internal static class。production diff 只有一个
accessibility token：

```diff
- public static class ScribanModelBinder
+ internal static class ScribanModelBinder
```

static facade 没有删除；两个 `ToScriptObject(PageModel/ListPageModel)` overload、expression
body 和 `ScribanRootModelMapper` 调用均未改变。没有修改 mapper graph、template keys、
aliases、null、dictionary/list、unsupported-object fallback、AOT roots 或
`InternalsVisibleTo`。

本任务是 2.0 source、binary 与 reflection breaking change。private、未索引或未声明的
consumer 继续为 `unknown-until-voluntary-declaration`。public
`ScribanTemplateRenderer.RenderPage/RenderList` 仍是模型转 HTML 的支持入口。

## 2. 精确 production 影响

唯一 production 文件变化：

```text
src/Bukit-Core/Bukit.Rendering/Scriban/ScribanModelBinder.cs
```

调用图保持：

```text
ScribanTemplateRenderer.RenderPage
  -> ScribanModelBinder.ToScriptObject(PageModel)
     -> ScribanRootModelMapper.ToScriptObject(PageModel)

ScribanTemplateRenderer.RenderList
  -> ScribanModelBinder.ToScriptObject(ListPageModel)
     -> ScribanRootModelMapper.ToScriptObject(ListPageModel)
```

`ScribanTemplateRenderer` 内仍有两个直接静态 binder roots；未改为分别直接调用 root
mapper，未新增 replacement facade、adapter、reflection import 或 public
`ScriptObject` SDK。

internal static class 在 metadata 中仍是 abstract + sealed。其两个方法继续是 public
static，使 owner tests 可通过既有 `InternalsVisibleTo("Bukit.Rendering.Tests")` 验证
原 shape；concrete binder type 不再由 `Assembly.GetExportedTypes()` 导出。

## 3. 模板 object shape 未变

G-04D6B 没有修改任何 mapper 文件。以下显式投影合同保持：

- root：`site`、`page`、top-level `seo`；
- lists：`pages`、`items`、`pagination`、`collection`、`taxonomy`、`filter`；
- site：name/title/url/description/base/language/year/params/modules/data/data-index；
- page：title/url/content/summary/TOC/date/fields/canonical trust/SEO；
- SEO：OG、Twitter、Article、alternates、JSON-LD；
- snake_case 与已存在的 camelCase aliases；
- derived list aliases；
- 所有既有 `readOnly: true`；
- optional object 的现有省略/保留-null 区别。

mapper graph 不使用 `ScriptObject.Import`、member renamer、member filter、
`GetProperties/GetFields` 或任意 CLR member reflection。模板字段继续由 literal
`SetValue` 明确建立；本任务没有借可见性治理重命名、删除或新增字段。

## 4. 动态值安全投影 fixtures

现有 `ScribanModelBinderTests` 已广泛覆盖 page、list、site、SEO、canonical trust、
modules、nested data、fields、pagination、taxonomy、filter 和 aliases。本任务不复制
这些测试，只补资格报告列出的未覆盖边界。

### 4.1 Page/List facade 等价

已有 PageModel fixture 的方法名从 `PublicFacade` 改为 `Facade`，断言不变。新增一项
ListPageModel fixture，证明 internal facade 与 `ScribanRootModelMapper` 的 root keys
一致，并保持 `site/page/pages/items` 的 Scriban types。

### 4.2 read-only 与 mutable dictionary

新增组合 fixture：

- outer `ReadOnlyDictionary<string, object>` 命中
  `IReadOnlyDictionary<string, object>` 分支；
- nested `ExpandoObject` 以 `IDictionary<string, object>` 视图进入 mutable-only 分支；
- nested `object?[]` 命中当前 `IEnumerable<object>` 分支；
- nested mutable dictionary 在 list 内继续递归为 `ScriptObject`；
- null 保持 null；
- whitespace-only keys 被跳过。

没有扩大为支持任意 value-type generic sequence；没有更改当前 pattern-match 顺序、
dictionary copy、key comparer或 iteration order。

### 4.3 unsupported safe object

新增 custom object，带一个可读取的 `HiddenMember`，但只断言输出是 override
`ToString()` 的 `"safe-display"` string。该 fixture 防止未来误用 reflection 暴露
arbitrary CLR members。

另一个 custom object 的 `ToString()` 抛
`InvalidOperationException("custom display failed")`，测试固定现有异常原样传播。没有
新增 catch、包装、重复调用或 silent fallback。

## 5. Architecture guard

新增：

```text
tests/Bukit.Architecture.Tests/G04D6BScribanModelBinderTests.cs
```

契约覆盖：

1. exact full name 仍存在；
2. type 是 internal、abstract、sealed，且不在 exported types；
3. 两个 public static non-generic overload 精确保留；
4. 参数集合仍为 `ListPageModel` 与 `PageModel`；
5. 返回类型仍为 `Scriban.Runtime.ScriptObject`；
6. public renderer source 仍保留两个 direct binder roots；
7. Rendering friend set 仍精确为 `Bukit.Engine` 与
   `Bukit.Rendering.Tests`；
8. current baseline 为 `14/486/60` 且不再包含 binder；
9. closed manifest 仍为 136 项，保留 binder historical candidate；
10. historical manifest Git blob 仍为
    `7b07d6890562387010b52301e9f8716e9bf10ed1`。

Architecture assembly 不需要新增 friend；它从 public
`ScribanTemplateRenderer` 定位 assembly，再通过 exact full name 检查 internal type。

## 6. Baseline 与现行治理同步

修改前阶段值：

```text
14 assemblies / 487 public types / 61 candidates
```

修改后阶段值：

```text
14 assemblies / 486 public types / 60 candidates
```

current baseline 只删除 `ScribanModelBinder` entry；相邻 public
`PageModel`、`ScribanTemplateRenderer`、`SectionDataResolverAccessor` entries 未改。

同步范围：

- 所有当前 G-04 architecture count guards 更新为 `486/60`；
- `docs/governance/bukit-core-2.0-consumer-declaration.md` 的 current count 与
  remaining-candidate wording；
- `guide/dev/public-api-governance.md` 的镜像 current count 与 D6B 决议；
- 新增 D6B governance 段，明确 facade/overloads/shape 保持。

未更新 D6/D6A eligibility/resolution 报告中的阶段快照；它们是对应任务的历史证据，不是
现行 count source。

closed
`docs/governance/bukit-core-2.0-public-surface-candidates.v1.json` 保持不变，预期静态证据：

```text
candidateCount = 136
candidates.length = 136
ScribanModelBinder historical entries = 1
Git blob = 7b07d6890562387010b52301e9f8716e9bf10ed1
```

## 7. 明确未修改

- binder overload/body/facade；
- `ScribanRootModelMapper` 和全部子 mapper；
- template key、alias 与 global push order；
- `readOnly: true`；
- null 与 optional object 语义；
- dictionary/list pattern order；
- scalar preservation；
- `ModuleInfo` explicit projection；
- unsupported object `ToString()` fallback；
- `ToString()` exception propagation；
- public Page/List models；
- public renderer、Engine adapter 与 Theme；
- Native AOT/trimmer configuration；
- IVT；
- Labs、插件、schema、协议与 asset URL。

## 8. 验证状态

Task 23 按 master plan 禁止运行 tests、aggregate、AOT 或 review。本任务只执行静态
检查，不把未运行项目写成通过：

| 检查 | 状态 |
|---|---|
| production diff | 仅 type accessibility 单 token |
| baseline JSON parse/count | 已确认 `14/486/60` |
| binder current entry | 已确认为 0 |
| historical manifest/blob | `136/136`、binder 1 项、blob 精确不变 |
| old current G-04 counts | docs/governance、guide/dev 与 architecture guards 无残留 |
| tests / aggregate / AOT / review | **未运行；Task 30 pending** |

## 9. Task 30 待验证集合

D6B 必须进入 G3 统一验证：

- `Bukit.Rendering.Tests`；
- `Bukit.Theme.Tests`；
- `Bukit.Engine.Tests`；
- `Bukit.Cli.Tests`；
- `Bukit.Architecture.Tests`；
- public API drift；
- G3 唯一 aggregate targeted gate；
- real Native AOT package/smoke；
- 一次独立轻量只读复审。

直接验收：

1. Page/List facade 与 mapper root shape 一致；
2. read-only/mutable dictionary、nested list、blank key、null 通过；
3. unsupported custom object 不反射成员；
4. `ToString()` exception 原样传播；
5. binder internal shape、overloads 与 renderer direct roots 通过；
6. final current baseline 使用 Task 29 后 G3 终值；
7. historical manifest 与 blob 不变；
8. AOT 不出现 missing method/type、reflection fallback 或 trimmer regression。

## 10. 停止条件复核

本次静态实施若提交前确认上述证据，将不触发 D6 停止条件。若 Task 30 发现：

- template key/alias/null/container/fallback shape 变化；
- renderer direct binder root 丢失；
- source generator、trimmer 或 AOT 回归；
- 需要新增 IVT、public shim 或 reflection mapper；
- 需要修改 Labs、插件或其他非 Core scope；

则 G-04D6B 必须停止关闭并进入独立复审，不能通过扩大 public surface、删除 facade、
改变 mapper 或禁用 trimming 超限修复。
