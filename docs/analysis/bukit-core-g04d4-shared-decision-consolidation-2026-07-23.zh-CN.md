# Bukit Core G-04D4 Shared decision consolidation

> 日期：2026-07-23
>
> 范围：G2 Task 16
>
> 状态：decisions consolidated / group-verification-pending

## 1. 汇总结论

G-04D4 对 `Bukit.Shared` 的 17 项候选已经形成完整终态：

| 决议图 | 数量 | 终态 |
|---|---:|---|
| legacy Notion model/record graph | 13 | retained public，重分类并退出 candidate |
| legacy Shared tokenizer graph | 3 | 删除 legacy identities，迁移到 `Bukit.Notion.Conversion` |
| `ValueCoercion` | 1 | 原位 internalized |

当前 current public API baseline 已精确达到：

```text
14 assemblies / 492 public types / 67 candidates
```

变化链为：

```text
Task 13 start     14 / 496 / 84
Task 14 D4A       14 / 493 / 68
Task 15 D4B       14 / 492 / 67
```

17 项均已有终态和 Task 20 待验证断言，不存在“仍是 candidate 但没有下一动作”的
Shared 悬空项。

本任务只汇总决策，不运行 test、public API drift、targeted gate、Native AOT 或复审。
组级关闭必须等待 Task 20。

## 2. 十七项逐类型终态与 Task 20 断言

### 2.1 十三项 retained model/record

十三项均继续位于 `Bukit.Shared.Notion`，保持 public/exported，并统一重分类为：

```text
cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review
```

| # | 类型 | 终态 | Task 20 必须验证 |
|---:|---|---|---|
| 1 | `NotionBlock` | retained/reclassified | exact identity仍 public abstract record；protected default/copy ctor与外部派生元数据不变 |
| 2 | `Heading1Block` | retained/reclassified | sealed record、`Text` ctor/property/`Deconstruct`与base identity不变 |
| 3 | `Heading2Block` | retained/reclassified | sealed record、`Text` ctor/property/`Deconstruct`与base identity不变 |
| 4 | `Heading3Block` | retained/reclassified | sealed record、`Text` ctor/property/`Deconstruct`与base identity不变 |
| 5 | `ParagraphBlock` | retained/reclassified | `List<RichTextSegment>` primary ctor、string ctor、record members及list reference equality不变 |
| 6 | `BulletedListItemBlock` | retained/reclassified | rich-text list/string构造、record shape及mapper round-trip不变 |
| 7 | `NumberedListItemBlock` | retained/reclassified | rich-text list/string构造、record shape及mapper round-trip不变 |
| 8 | `QuoteBlock` | retained/reclassified | rich-text list/string构造、record shape及mapper round-trip不变 |
| 9 | `ImageBlock` | retained/reclassified | `Url`、`Caption=null` default、deconstruct与JSON projection不变 |
| 10 | `ToggleBlock` | retained/reclassified | `Heading`、递归 `List<NotionBlock> Children`及递归mapping不变 |
| 11 | `CodeBlock` | retained/reclassified | `Code`、`Language="plain text"` default及JSON projection不变 |
| 12 | `CalloutBlock` | retained/reclassified | `Text`、`Icon="📝"` default及JSON projection不变 |
| 13 | `RichTextSegment` | retained/reclassified | `Text/Bold=false/Italic=false/LinkUrl=null`、record equality与四类block传播不变 |

还必须验证 retained public：

```csharp
HtmlToNotionBlockConverter.Convert(string)
```

继续精确返回：

```csharp
List<Bukit.Shared.Notion.NotionBlock>
```

compatibility mapper与writer必须覆盖全部已知 derived graph，未知 type仍保持既有
`NotSupportedException` 边界。

### 2.2 三项 removed tokenizer identities

| # | legacy identity | 终态 | Task 20 必须验证 |
|---:|---|---|---|
| 14 | `Bukit.Shared.Notion.HtmlTokenizer` | removed / canonical migration | Shared exact full name不存在；canonical `Bukit.Notion.Conversion.HtmlTokenizer` public/exported且可运行 |
| 15 | `Bukit.Shared.Notion.HtmlTokenizer+HtmlToken` | removed / canonical migration | legacy nested type不存在；canonical token仍有四个 init-only property与空字符串/default enum值 |
| 16 | `Bukit.Shared.Notion.HtmlTokenizer+HtmlTokenType` | removed / canonical migration | legacy enum不存在；canonical ordinal固定为 0/1/2/3 |

Task 20 还必须验证 canonical tokenizer：

- open/close/self-closing/text 四种 token；
- tag trim和 invariant lowercase；
- text trim与 HTML entity decode；
- attribute原文；
- empty/whitespace；
- unmatched `<`和missing `>`；
- `null`输入既有异常类型；
- canonical converter的link/image safety、FAQ/toggle、rich text、pre/code与JSON输出。

### 2.3 `ValueCoercion`

| # | 类型 | 终态 | Task 20 必须验证 |
|---:|---|---|---|
| 17 | `Bukit.Shared.ValueCoercion` | internalized | exact type仍可解析、`IsNotPublic`、不在 exported types；三个 static method签名和全部语义不变 |

Task 20 的行为矩阵必须包括：

- `null`：truthy false、falsy true、nullable conversion false；
- boolean原值；
- trim后的固定 truthy/falsy whitelist；
- mixed-case token仍是 unknown；
- unknown同时不是 truthy/falsy，并返回 null；
- number继续通过当前 culture的 `ToString()`判断；
- 非零数字不自动 truthy；
- custom `ToString()`值、null和异常传播；
- 没有新 Core/Labs/Plugin production consumer；
- Shared friend assembly集合不扩大。

## 3. “Shared 不再承载第二套 Notion 实现”的准确含义

G-04D4 的结果不能被简化为“Shared 已删除全部 legacy Notion 类型”。准确边界是：

### 已消除的重复实现

- Shared 不再定义第二套 `HtmlTokenizer`行为入口；
- Shared 不再定义第二套 `HtmlToken` DTO；
- Shared 不再定义第二套 `HtmlTokenType` enum；
- parsing、entity decode与tokenization行为只由
  `Bukit.Notion.Conversion.HtmlTokenizer`拥有。

### 仍保留的 compatibility companion graph

- 十三个 legacy model CLR identities仍 public；
- retained `HtmlToNotionBlockConverter.Convert`仍返回legacy `NotionBlock`；
- Shared compatibility mapper把 canonical blocks投影为legacy records；
- Shared writer把legacy records反向映射给canonical explicit writer。

所以 Shared 保留的是**受现有 public signature约束的兼容模型投影**，不是独立的第二套
tokenization、HTML parsing或JSON行为 owner。新 Notion能力必须落在 canonical
`Bukit.Notion`项目；Shared compatibility graph只接受兼容性、正确性和安全修复。

如果未来要删除十三项 model，必须另立原子任务，同时处理 retained converter signature、
mapper、writer、所有 model identities、外部 subclass风险与迁移版本策略。不能把本轮
tokenizer删除解释为后续可逐项静默删除 model的授权。

## 4. baseline 与治理分类

### 4.1 current baseline

Task 16确认 current baseline：

```text
schema: bukit-core-public-api-baseline-v1
assemblies: 14
types: 492
2.0-candidate: 67
```

Shared 17项的 current状态：

- 十三项 model仍存在，但不再计入 candidate；
- 三项 legacy tokenizer不再存在于 current public surface；
- `ValueCoercion`仍存在于 assembly内部，但不再 exported，也不计入 public baseline。

### 4.2 historical manifest

closed historical consumer-declaration manifest必须继续：

- `declarationState = closed`；
- `candidateCount = 136`；
- 136 entries完整；
- 十七项历史记录全部保留；
- Git blob `7b07d6890562387010b52301e9f8716e9bf10ed1` 不变。

current baseline描述当前治理终态；historical manifest描述声明窗口关闭时的 cohort。
retained、removed或internalized都不授权重写历史证据。

## 5. 消费者与 private unknown

仓内证据：

- 十三项 model有真实 public converter、mapper和writer静态路径；
- 三项 legacy tokenizer的直接 consumer集中于被迁移的test/governance fixture；
- production Core/Labs/Plugin没有直接 legacy tokenizer consumer；
- `ValueCoercion` production consumer为0，直接调用只在 Shared owner tests；
- canonical tokenizer/converter由 `Bukit.Notion` source和tests静态使用。

外部证据：

- 17项历史 authenticated exact search均为 `no-public-match-found`；
- simple-name结果中的通用类型/词法碰撞已排除；
- declaration仍为 `consumer-declaration-pending`；
- private、未索引、binary、reflection和未自愿声明consumer保持
  `unknown-until-voluntary-declaration`。

因此：

- 十三项 model选择retain，不夸大“无消费者”；
- 三项 tokenizer删除和 `ValueCoercion` internalize仍是2.0-only
  source/binary/reflection break；
- 若 Task 20 前出现真实声明，必须按停止条件重新评审。

## 6. 兼容性矩阵

| 图 | source | binary | reflection | runtime behavior |
|---|---|---|---|---|
| 13 model | identity/member不变 | token不变 | public/exported不变 | converter/mapper/writer不变 |
| 3 tokenizer | consumer迁往 `Bukit.Notion.Conversion` | legacy token失效，需重编 | legacy full names消失 | canonical tokenization行为不变 |
| `ValueCoercion` | 外部调用不再编译 | public type/member token不再可用 | exact type仍可解析但not exported | 三个方法体与语义不变 |

legacy与canonical model/token即使字段同形，也不是相同 CLR identity。不得以type alias、
reflection fallback或新wrapper伪造binary compatibility。

## 7. serialization、reflection 与 Native AOT

- model/token/`ValueCoercion`没有产品 `JsonSerializerContext`或generic serializer root；
- block JSON由canonical writer显式type switch生成；
- retained model由converter与mapper静态引用；
- canonical tokenizer由canonical converter静态引用；
- removed tokenizer和internalized `ValueCoercion`不需要 trimmer descriptor、
  `DynamicDependency`或service registration；
- architecture中的`Assembly.GetType`/`GetExportedTypes`只用于治理验证，不是产品factory。

Task 20必须通过真实 Native AOT publish和published artifact smoke验证：

1. retained converter/model mapper静态可达；
2. canonical tokenizer/converter静态可达；
3. 没有已删除 Shared tokenizer的dynamic root；
4. Shared visibility变化没有引入linker或runtime failure。

## 8. Task 20 统一待验证集合

G-04D4 必须加入 G2 aggregate 的证据：

- `Bukit.Shared.Tests`
- `Bukit.Notion.Tests`
- `Bukit.Content.Tests`
- `Bukit.Content.Notion.Tests`
- `Bukit.Engine.Tests`
- `Bukit.Architecture.Tests`
- public API drift
- `GROUP_BASE..HEAD` 唯一 aggregate targeted gate
- Native AOT publish和published artifact smoke
- `git diff --check`
- 一次 G2轻量只读复审

Shared architecture断言至少必须整体证明：

1. 十三项 model exact public/exported/reclassification；
2. converter exact return signature；
3. base/derived closure、defaults、record/deconstruct/equality与recursive/list语义；
4. 三项 legacy tokenizer exact absence；
5. 三项 canonical tokenizer exact public identity与行为；
6. `ValueCoercion` internal/not exported及语义矩阵；
7. current baseline `14/492/67`；
8. historical 136-entry manifest与blob不变；
9. IVT集合不扩大；
10. 没有跨入 Content/Engine/Labs/Plugin业务实现。

Task 20完成前，17项只能是“决策已汇总、实现待组级验证”，不能宣称 G-04D4完整关闭。

## 9. 禁止漂移

G-04D4后续验证与复审不得顺带：

- internalize/delete十三项 retained model；
- 修改 retained converter public signature；
- 修改model constructor/default/inheritance/equality/deconstruct；
- 修改compatibility mapper、writer或block JSON；
- 恢复Shared tokenizer wrapper/nested DTO；
- 修改canonical tokenizer enum ordinal、token defaults、解析、异常或安全行为；
- 修改 `ValueCoercion` method/member或null、number、boolean、culture、fallback语义；
- 建立第二套Notion parsing/tokenization/serialization owner；
- 新增global conversion abstraction；
- 修改Content/Engine/Labs/Plugin业务逻辑；
- 新增production IVT、reflection保活或serializer root；
- 修改schema、config、plugin protocol、媒体、SEO、路径工具；
- 修改CI、release、gate或closed historical manifest。

环境、tool或test failure不能授权扩大production diff。

## 10. 停止条件

出现任一情况时，G-04D4不能申请组级关闭：

1. 发现direct external/private CLR consumer、外部 `NotionBlock` subclass或动态identity
   root；
2. 十三项 model或converter signature发生可见性/member漂移；
3. 任一legacy tokenizer identity仍存在或canonical behavior不等价；
4. `ValueCoercion`不再是纯visibility变化；
5. current baseline不是精确 `14/492/67`；
6. historical manifest内容、136-entry计数或Git blob发生变化；
7. 需要新增production IVT、serializer/reflection/AOT保活或跨模块业务改动；
8. Task 20 owner tests、public API drift、aggregate targeted、Native AOT或轻量复审存在
   未关闭 failure/finding。

若真实 tokenizer/`ValueCoercion` consumer出现，应改为retained或独立
obsolete/declaration window；若 model graph未来要删除，必须另立完整public contract
migration任务。

## 11. 正式决策台账

| 范围 | 数量 | 终态 | current candidate状态 | Task 20前状态 |
|---|---:|---|---|---|
| legacy model/record | 13 | retained public / reclassified | 已退出 candidate | group-verification-pending |
| legacy Shared tokenizer | 3 | removed / canonical migration | 已退出 current baseline | group-verification-pending |
| `ValueCoercion` | 1 | internalized | 已退出 current baseline | group-verification-pending |

G-04D4 的 17项决策至此全部汇总。正式状态为：

```text
decisions consolidated / group-verification-pending
```
