# Bukit Core G-04D4A Shared Notion graph resolution

> 日期：2026-07-23
>
> 范围：G2 Task 14
>
> 状态：implementation complete / group-verification-pending

## 1. 执行摘要

Task 14 按
[G-04D4 Shared foundation 资格审计](bukit-core-g04d4-shared-foundation-eligibility-audit-2026-07-23.zh-CN.md)
将 16 个 legacy Notion 候选拆成两个原子结论：

| 图 | 数量 | 最终处置 |
|---|---:|---|
| legacy model/record 继承图 | 13 | retain-by-design，继续 public/exported，并退出 candidate |
| legacy `HtmlTokenizer` 图 | 3 | 迁移到 canonical owner，删除 Shared legacy identities |

Task 14 的目标 current baseline 为：

| 项目 | Task 13 起点 | Task 14 目标 |
|---|---:|---:|
| Core assemblies | 14 | 14 |
| public types | 496 | 493 |
| `2.0-candidate` types | 84 | 68 |

计算关系是：

- 删除三个 legacy tokenizer identity：public types `496 - 3 = 493`；
- 三项 tokenizer 删除、十三项 model 重分类：
  candidates `84 - 3 - 13 = 68`。

这不是对 16 项的批量删除。十三项 model 是 retained public compatibility graph；
只有不参与其 public signature 的 tokenizer 三项迁移到 canonical owner。

Task 14 未处理 `ValueCoercion`。该类型严格留给 Task 15，并应以
`14/493/68` 为实施起点。

## 2. 十三项 model graph：retain-by-design

以下类型继续位于 `Bukit.Shared.Notion`，保持 public/exported：

1. `NotionBlock`
2. `Heading1Block`
3. `Heading2Block`
4. `Heading3Block`
5. `ParagraphBlock`
6. `BulletedListItemBlock`
7. `NumberedListItemBlock`
8. `QuoteBlock`
9. `ImageBlock`
10. `ToggleBlock`
11. `CodeBlock`
12. `CalloutBlock`
13. `RichTextSegment`

current baseline 中十三项统一从：

```text
implementation-public / 2.0-candidate / 2.0-review
```

重分类为：

```text
cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review
```

重分类表达当前真实契约：它们不是悬空的实现泄漏，而是 retained public
`HtmlToNotionBlockConverter.Convert(string)` 的必要 companion graph。

### 2.1 public signature 阻断

retained converter 的签名继续是：

```csharp
public static List<Bukit.Shared.Notion.NotionBlock> Convert(string html)
```

`NotionBlock` 因此不能在本任务中删除或 internalize。其 derived records、
`RichTextSegment` 与递归 `ToggleBlock.Children` 又构成不可半拆的 public graph：

```text
HtmlToNotionBlockConverter.Convert
  -> List<NotionBlock>
     -> 11 concrete block records
     -> Paragraph/Bulleted/Numbered/Quote
        -> List<RichTextSegment>
     -> ToggleBlock
        -> List<NotionBlock>
```

只收窄 base、部分 derived type 或 `RichTextSegment` 都会形成 inconsistent
accessibility、不可构造返回值或 source/binary compatibility 断裂。

### 2.2 record、继承和集合语义

Task 14 不改变：

- `NotionBlock` 的 abstract record identity、protected default/copy constructor 与
  外部派生可能性；
- 十一个已知 derived record 的 sealed 状态；
- positional constructor、init property、`Deconstruct`、record equality、
  `GetHashCode`、clone 和 `ToString`；
- `ParagraphBlock` 等类型的 string convenience constructor；
- `ImageBlock.Caption = null`；
- `CodeBlock.Language = "plain text"`；
- `CalloutBlock.Icon = "📝"`；
- `RichTextSegment` 的 `Bold=false`、`Italic=false`、`LinkUrl=null`；
- `List<RichTextSegment>` 与 `List<NotionBlock>` 的引用相等语义。

legacy 与 canonical records 即使字段同形，也具有不同 assembly/namespace identity、
不同 `IEquatable<T>` 和不同 record equality contract，不能被 type alias 或 forwarding
无损替换。

### 2.3 runtime ownership

Shared public converter继续调用 canonical converter，再通过 internal compatibility
mapper建立 legacy graph；internal block writer继续反向映射到 canonical block并使用
canonical explicit JSON writer。

因此十三项具有真实静态运行路径，不是只为反射保留的 public metadata。新功能仍应使用
`Bukit.Notion.Blocks` canonical model；retained legacy graph只承担兼容责任。

## 3. 三项 tokenizer graph：canonical migration

Task 14 原子删除：

- `Bukit.Shared.Notion.HtmlTokenizer`
- `Bukit.Shared.Notion.HtmlTokenizer+HtmlToken`
- `Bukit.Shared.Notion.HtmlTokenizer+HtmlTokenType`

canonical owner保持：

- `Bukit.Notion.Conversion.HtmlTokenizer`
- `Bukit.Notion.Conversion.HtmlTokenizer+HtmlToken`
- `Bukit.Notion.Conversion.HtmlTokenizer+HtmlTokenType`

删除必须是三项原子操作，因为：

```text
HtmlTokenizer.Tokenize(string)
  -> List<HtmlToken>
     -> HtmlToken.Type
        -> HtmlTokenType
```

保留任一 nested identity 都会形成没有 owner 或没有可用入口的半图。

### 3.1 canonical shape

迁移后继续由 canonical owner保证：

| member | 保持值 |
|---|---|
| `HtmlToken.Type` default | `OpenTag` |
| `TagName` default | `""` |
| `Attributes` default | `""` |
| `TextContent` default | `""` |
| `OpenTag` ordinal | 0 |
| `CloseTag` ordinal | 1 |
| `SelfClosingTag` ordinal | 2 |
| `Text` ordinal | 3 |

所有 token properties继续 init-only。Task 14 不引入 token adapter、type forwarding、
第二套 DTO 或 enum ordinal translation layer。

### 3.2 canonical behavior

迁移只改变 CLR owner，不改变：

- tag name trim 与 invariant lowercase；
- text trim 和既有 HTML entity decode；
- open/close/self-closing/text token分类；
- self-closing attribute原文；
- 空或纯空白输入返回空列表；
- 未闭合 tag返回此前已完成 tokens；
- `null` 输入的既有异常类型；
- converter 的 link/image safety、rich text、toggle、FAQ、pre/code 和 JSON 输出。

Shared retained converter已经直接消费 canonical conversion行为，不需要 legacy
tokenizer identity才能继续工作。

## 4. 消费者迁移与证据边界

### 4.1 仓内消费者

调查确认：

- production Core、Labs、Plugin没有直接依赖 Shared legacy tokenizer；
- Shared tokenizer直接 consumer集中在 legacy test/architecture fixture；
- canonical tokenizer与 converter已有 `Bukit.Notion.Tests` owner测试；
- 十三项 model 的 production入口仍是 Shared converter、compatibility mapper和writer；
- Content/Engine production没有新增直接 legacy model consumer。

因此 tokenizer migration不要求修改 Content、Engine、Labs或Plugin业务逻辑。

### 4.2 外部消费者

closed consumer-declaration manifest 中 16 个 legacy Notion identity仍保留历史记录：

- authenticated exact search状态为 `no-public-match-found`；
- simple-name结果中的词法碰撞已经排除；
- private、未索引或未自愿声明的 consumer仍是
  `unknown-until-voluntary-declaration`。

这只能证明没有已确认的公开直接命中，不能证明私人 consumer不存在。三项 tokenizer
删除仍是 2.0-only source/binary/reflection breaking change。

## 5. 兼容性说明

### 5.1 十三项 retained model

十三项只改变治理分类，不改变类型或成员，因此：

- source identity不变；
- binary type/member token不变；
- reflection full name和export状态不变；
- record equality、inheritance和deconstruction不变；
- block JSON与runtime mapping不变。

对新代码的迁移建议仍是使用 `Bukit.Notion.Blocks`；这不是删除 legacy graph 的时限承诺。

### 5.2 三项 tokenizer

直接使用 Shared tokenizer的 consumer必须迁移 namespace：

```diff
- Bukit.Shared.Notion.HtmlTokenizer
+ Bukit.Notion.Conversion.HtmlTokenizer
```

canonical nested types虽保持相同字段和 ordinal，也不是旧 CLR identity：

- 旧 source需要改 namespace并重新编译；
- 旧 binary type/member token无法继续解析；
- 旧 reflection full name不再存在；
- 以旧 nested type为 serializer model的外部 consumer必须自行迁移。

本任务不提供 obsolete window或 type forwarding，因为这会继续保留重复 public DTO，
也无法在不同 full name之间实现真正的 CLR identity等价。

## 6. baseline 与 historical manifest

current baseline只允许两类变化：

1. 删除三项 Shared tokenizer entries；
2. 将十三项 model entries重分类为
   `cross-assembly-implementation / 1.x-do-not-narrow`。

目标必须精确为：

```text
14 assemblies / 493 public types / 68 candidates
```

closed historical manifest必须继续满足：

- `declarationState = closed`；
- `candidateCount = 136`；
- 136 entries全部保留；
- 三项 tokenizer与十三项 model的历史记录不删除、不改写；
- Git blob `7b07d6890562387010b52301e9f8716e9bf10ed1` 保持不变。

current baseline描述当前终态；historical manifest描述声明窗口关闭时的 cohort。两者用途
不同，不能为追求当前计数而重写历史证据。

## 7. serialization、reflection 与 Native AOT

- legacy/canonical block和token types没有产品 `JsonSerializerContext` registration；
- block JSON仍由 canonical writer的 explicit type switch处理；
- retained model graph由 converter和mapper静态引用，不依赖 reflection保活；
- canonical tokenizer由 canonical converter和owner tests静态引用；
- 删除 legacy wrapper不需要 `DynamicDependency`、trimmer descriptor或新反射注册；
- architecture测试中的 `Assembly.GetType`/`GetExportedTypes`只用于治理证明，不是产品
  runtime factory。

Native AOT不能在 Task 14 单项阶段预称通过。Task 20必须以最终 G2 graph运行真实
Native AOT publish及published fixture，证明：

- retained legacy converter和model mapper仍可达；
- canonical tokenizer/converter仍可达；
- 不存在对已删除 Shared tokenizer full name的动态依赖。

## 8. Task 20 待验证证据

以下证据统一留给 Task 20，不在 Task 14 重复运行：

- `Bukit.Shared.Tests`
- `Bukit.Notion.Tests`
- `Bukit.Content.Tests`
- `Bukit.Content.Notion.Tests`
- `Bukit.Engine.Tests`
- `Bukit.Architecture.Tests`
- public API drift
- G2 `GROUP_BASE..HEAD` 唯一 aggregate targeted gate
- Native AOT publish与published artifact smoke
- `git diff --check`
- 一次 G2 轻量只读复审

architecture/owner tests应至少证明：

1. 十三项 model仍 public/exported且分类精确；
2. retained converter仍返回 exact `List<Bukit.Shared.Notion.NotionBlock>`；
3. 三项 Shared tokenizer full name不再存在；
4. 三项 canonical tokenizer identity仍 public/exported；
5. token defaults、四个 enum ordinal与边界行为不变；
6. model constructor/default、inheritance、record/deconstruct和mutable-list语义不变；
7. current baseline精确 `14/493/68`；
8. historical 136-entry manifest与Git blob不变；
9. 没有新增 production IVT或跨模块业务依赖。

在 Task 20 完成前，本任务只能标记
`implementation complete / group-verification-pending`，不能申请 G2关闭。

## 9. 禁止漂移

Task 14 不得顺带：

- internalize或删除十三项 model；
- 修改 `HtmlToNotionBlockConverter.Convert` public signature；
- 修改 compatibility mapper、block writer或block JSON；
- 修改 canonical block/tokenizer public shape或行为；
- 修改 record constructor、default、inheritance、equality或deconstruct；
- 修改 token default、enum ordinal、entity decode或截断语义；
- 处理 `ValueCoercion`；
- 修改 Content/Engine/Labs/Plugin业务逻辑；
- 修改 schema、config、plugin protocol、媒体、SEO、路径工具、CI、release或gate；
- 新增 production IVT、service locator、serializer root或trimmer bypass；
- 修改 closed consumer-declaration manifest。

## 10. 停止条件

出现任一情况时，Task 14不能申请验证关闭：

1. 发现已确认的 direct external/private CLR consumer或外部 `NotionBlock` subclass；
2. retained converter无法在不改变 public signature的前提下继续编译；
3. 十三项 model不能保持完整 public companion graph；
4. 任一 Shared tokenizer legacy identity仍存在或只删除部分 nested graph；
5. canonical migration改变 token defaults、enum ordinal、异常、解析或安全行为；
6. current baseline不是精确 `14/493/68`；
7. historical manifest内容、136-entry计数或Git blob发生变化；
8. 需要新增 production IVT、reflection/AOT保活配置或跨模块业务改动；
9. Task 20 owner tests、public API drift、aggregate targeted、Native AOT或独立复审存在
   未关闭 failure/finding。

若发现真实 tokenizer consumer，必须停止删除并选择 retained或独立 obsolete/declaration
window；若未来要移除十三项 model，必须另立原子任务同时审理 converter public
signature、mapper、writer、全部 identities与迁移策略，不能扩大本 Task 14。

## 11. 正式关闭台账

| 图 | 类型数 | Task 14状态 | 最终分类/owner | 后续 |
|---|---:|---|---|---|
| legacy model/record | 13 | retained/reclassified | Shared compatibility facade；`cross-assembly-implementation / 1.x-do-not-narrow` | Task 20验证 |
| legacy tokenizer | 3 | canonical migration/delete complete | canonical owner为 `Bukit.Notion.Conversion` | Task 20验证 |
| `ValueCoercion` | 1 | not in Task 14 | 独立 `2.0-candidate` | Task 15 |

Task 14 的实现决策至此完整，但 G2 组级测试、AOT和只读复审尚未执行，状态保持
`group-verification-pending`。
