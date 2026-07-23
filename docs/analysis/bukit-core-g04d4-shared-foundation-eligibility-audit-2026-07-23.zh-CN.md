# Bukit Core G-04D4 Shared foundation 资格审计

> 日期：2026-07-23
> 范围：G2 Task 13，只做资格判定，不实施 Task 14/15
> 状态：eligibility complete / group-verification-pending

## 1. 执行摘要

Task 13 的当前公共面基线为：

| 项目 | 当前值 |
|---|---:|
| Core assemblies | 14 |
| public types | 496 |
| `2.0-candidate` types | 84 |

`Bukit.Shared` 的 17 项候选不是一个可以批量 internalize 的平面集合，必须拆成三个
独立决议单元：

| 决议单元 | 数量 | 资格结论 |
|---|---:|---|
| legacy Notion model/record 继承图 | 13 | retain-by-design，并重分类 |
| legacy `HtmlTokenizer` 及两个 nested types | 3 | 原子 canonical migration，删除 legacy identities |
| `ValueCoercion` | 1 | 独立 eligible internalize |

13 个 model 类型继续 public，但应从悬空 candidate 改为：

- `classification: cross-assembly-implementation`
- `compatibility: 1.x-do-not-narrow`
- `migrationHorizon: 2.0-review`

它们是 retained public `HtmlToNotionBlockConverter.Convert(string)` 的必要 companion
types，不能在当前 Task 14 边界内收窄。

Task 14 若按本审计结论实施，预计 current baseline 为 `14/493/68`：三个 tokenizer
identity 消失，13 个 model 仍 public 但退出 candidate。Task 15 随后只 internalize
`ValueCoercion`，预计得到 `14/492/67`。

以上都是待实施、待 Task 20 组级验证的目标值，不代表测试、public API drift 或 Native
AOT 已通过。

## 2. 17 项拆分

### 2.1 13 项 model/record 图

定义集中在
`src/Bukit-Core/Bukit.Shared/Notion/NotionBlockTypes.cs`：

| 类型 | 定义与 public shape | 传播角色 |
|---|---|---|
| `NotionBlock` | abstract record；protected default/copy constructors | 所有 block 的 public base；允许外部 record 派生 |
| `Heading1Block` | sealed record `(string Text)` | `NotionBlock` derived |
| `Heading2Block` | sealed record `(string Text)` | `NotionBlock` derived |
| `Heading3Block` | sealed record `(string Text)` | `NotionBlock` derived |
| `ParagraphBlock` | `(List<RichTextSegment> Segments)`；另有 string constructor | 同时依赖 base 与 rich-text graph |
| `BulletedListItemBlock` | 与 `ParagraphBlock` 同形 | 同时依赖 base 与 rich-text graph |
| `NumberedListItemBlock` | 与 `ParagraphBlock` 同形 | 同时依赖 base 与 rich-text graph |
| `QuoteBlock` | 与 `ParagraphBlock` 同形 | 同时依赖 base 与 rich-text graph |
| `ImageBlock` | `(string Url, string? Caption = null)` | `NotionBlock` derived |
| `ToggleBlock` | `(string Heading, List<NotionBlock> Children)` | 形成递归 block graph |
| `CodeBlock` | `(string Code, string Language = "plain text")` | `NotionBlock` derived |
| `CalloutBlock` | `(string Text, string Icon = "📝")` | `NotionBlock` derived |
| `RichTextSegment` | `Text/Bold=false/Italic=false/LinkUrl=null` | 四种 rich-text block 的 public ctor/property/deconstruct 类型 |

canonical 定义位于
`src/Bukit-Core/Bukit.Notion/Blocks/NotionBlockTypes.cs`。两个文件除 namespace 外
定义相同，因此 canonical source migration 在构造器、默认值和属性 shape 上是机械的；
但这不表示两个 CLR identity 可互换。

### 2.2 3 项 tokenizer 图

`src/Bukit-Core/Bukit.Shared/Notion/HtmlTokenizer.cs` 定义：

1. `HtmlTokenizer`
2. `HtmlTokenizer.HtmlToken`
3. `HtmlTokenizer.HtmlTokenType`

三者是一个原子 public signature 图：

```text
HtmlTokenizer.Tokenize(string)
  -> List<HtmlToken>
       -> HtmlToken.Type
            -> HtmlTokenType
```

Shared 实现已经只调用
`Bukit.Notion.Conversion.HtmlTokenizer`，然后复制 token shape；canonical owner 已存在。
这三个 legacy identities 不被 13-model 图或 retained public
`HtmlToNotionBlockConverter.Convert` 使用，因此可以与 model retention 分开决定，但三项
之间不能半删。

### 2.3 `ValueCoercion`

`src/Bukit-Core/Bukit.Shared/ValueCoercion.cs` 是独立 static utility：

- `IsTruthy(object?)`
- `IsFalsy(object?)`
- `ToBooleanOrNull(object?)`

仓库生产代码没有消费者，当前直接引用只在 `Bukit.Shared.Tests`。它不依赖 Notion
model/tokenizer，也不应借 Task 14 改造成全局 conversion abstraction。

## 3. 13-model 图为何必须 retain-by-design

### 3.1 retained public signature 是硬边界

`Bukit.Shared.Notion.HtmlToNotionBlockConverter` 当前被治理为 retained public
compatibility facade。其：

```csharp
public static List<Bukit.Shared.Notion.NotionBlock> Convert(string html)
```

直接暴露 legacy base。若只把 `NotionBlock` 或 derived records 改为 internal，会产生
public API inconsistent accessibility；若删除它们，则必须同时删除或改变 `Convert`
的 public signature。

Task 14 当前只条件授权修改 `NotionBlockTypes.cs` 与 `HtmlTokenizer.cs`，没有授权改变
retained `HtmlToNotionBlockConverter.Convert`。因此 model 图在本轮的正确终态是保留并
重分类，不是偷偷扩大 Task 14。

### 3.2 mapper、writer 与行为传播

Shared public converter 先调用 canonical converter，再由 internal
`NotionCompatibilityMapper` 把 11 种 canonical derived blocks 逐项映射回 legacy
records。mapper 对 `Paragraph/Bulleted/Numbered/Quote` 的 segments 和 `Toggle` 的
children 递归创建新 list。

internal `NotionBlockJsonWriter` 则执行反向映射，再调用 canonical explicit JSON writer。
未知的 canonical 或 legacy derived block 会进入 mapper 的 `NotSupportedException`
分支。虽然 mapper/writer 不是 public，它们证明 13 个类型仍由真实静态路径到达，不是
只有 public metadata 的空壳。

### 3.3 不能使用 type forwarding 或 alias 规避

legacy 与 canonical 类型的 namespace 和 assembly identity 不同。C# alias 不会生成
可供外部消费者解析的 legacy CLR type，type forwarding 也不能把一个旧 full name
伪装成不同 namespace 的新 full name。因此若未来确实要移除 13-model 图，必须单独批准
`HtmlToNotionBlockConverter.Convert` 的 2.0 public source/binary break，并把 converter、
mapper、writer 和全部测试 roots 纳入同一个原子任务。

## 4. record equality、inheritance 与 mutable list 语义

每个 positional record 都生成自己的：

- constructor 和 init-only properties；
- `Deconstruct`；
- `Equals`、`GetHashCode` 和 equality operators；
- clone、`ToString`、`PrintMembers` 与 `EqualityContract`。

legacy 与 canonical record 即使字段同形，也因 CLR identity 和
`IEquatable<T>` 不同而不能跨 namespace 相等。现有 compatibility test 使用
`Assert.Equivalent(..., strict: true)` 验证 deep shape round-trip；它不是 record
`Equals` 契约证明。

还必须保留以下细节：

- `List<RichTextSegment>` 和 `List<NotionBlock>` 在 record equality 中使用 list 对象的
  equality，即 reference equality，不是元素序列 equality；
- mapper round-trip 会重建 list，因此 deep-equivalent graph 不等于 record-equal graph；
- `NotionBlock` 不是 sealed，protected default/copy constructors 和 record equality
  contract 允许外部 record 继承；没有公开搜索匹配不等于不存在 private subclass；
- 11 个已知 derived records 是 sealed，不能逐个作为外部继承基类；
- `ToggleBlock.Children` 把 base graph 递归嵌入 public shape；
- `RichTextSegment` 同时被四个 block 的 constructor、property 和 deconstruct 暴露。

这些传播关系决定了 13 项不能出现“base 保留、部分 derived 删除”或
“derived 保留、RichTextSegment 收窄”的半迁移。

## 5. tokenizer canonical migration 的契约

### 5.1 token shape 与默认值

legacy 与 canonical nested token shape 当前一致：

- `HtmlToken.Type` 默认是 enum 零值 `OpenTag`；
- `TagName` 默认 `""`；
- `Attributes` 默认 `""`；
- `TextContent` 默认 `""`；
- 所有属性均为 init-only。

`HtmlTokenType` 的数值序号必须固定：

| 名称 | ordinal |
|---|---:|
| `OpenTag` | 0 |
| `CloseTag` | 1 |
| `SelfClosingTag` | 2 |
| `Text` | 3 |

Shared wrapper 当前以 `(HtmlTokenType)(int)token.Type` 映射 enum。Task 14 删除 wrapper
以后，消费者迁往 canonical enum；不得借迁移重新排序 enum 或改变 token defaults。

### 5.2 行为与异常边界

canonical tokenizer 是行为 owner，当前关键行为包括：

- tag name trim 后以 invariant lowercase 输出；
- text trim 后解码受支持的 HTML entities；
- self-closing token 保留原 attribute string；
- 空或纯空白输入返回空 token list；
- 找不到 `>` 的未闭合 tag 会停止扫描，并返回之前已经形成的 tokens；
- public 参数按 non-null contract 实现；运行时传入 `null` 会在现有成员访问中抛出，
  Task 14 不应新增静默 null fallback 或新异常包装；
- enum cast 本身不验证未知 ordinal；Task 14 不能借删除 facade改变 canonical enum。

Task 14 的 canonical migration 应删除三个 legacy identities，并把仍需要 token-level
测试的用例迁往 canonical `Bukit.Notion.Tests` 或改为 canonical consumer fixture；不得
在 Shared 再保留第二套 token DTO。

## 6. `ValueCoercion` 独立资格

`ValueCoercion` 只进行 `ToString()?.Trim()` 后的固定字符串匹配。Task 15 internalize 时
必须保持：

- `null`：truthy `false`、falsy `true`、最终 boolean `false`；
- 原生 `true/false` 的直接分支；
- truthy 只接受当前列举的 `true/yes/1/on` 三种大小写形式；
- falsy 只接受当前列举的 `false/no/0/off` 三种大小写形式；
- 空或 whitespace：truthy `false`、falsy `true`；
- 非零 number 如 `42` 不是 truthy，也不是 falsy，最终返回 `null`；
- 不使用 culture-sensitive parse，不扩大大小写、数字或 fallback 语义；
- `value.ToString()` 的异常和副作用不新增 catch 或重复调用优化之外的语义改变。

Task 15 只把 class accessibility 从 public 收窄为 internal，并更新 current baseline 与
architecture guard；不移动文件、不抽象接口、不让 Task 14 的 Notion 决议影响它。

## 7. consumers、manifest 与 private unknown

### 7.1 仓库内消费者

- 13-model graph 的生产入口集中在 Shared public converter、internal mapper 和 internal
  writer；
- Content/Engine production 没有直接 model/record consumer；
- Content tests 对 `Bukit.Shared.Notion` 的使用属于 retained `NotionApiUrls`，不是
  model graph；
- Engine 的相关 test import 没有形成 model CLR root；
- Shared tests 覆盖 converter block shape、writer JSON、legacy/canonical mapper
  round-trip、tokenizer基本行为和 `ValueCoercion`；
- architecture test 使用 `Type.GetType`、`GetExportedTypes` 与 exact full-name 集合冻结
  legacy surface。

### 7.2 外部消费者证据

closed 136-entry manifest 对这 17 项均保留历史 candidate 记录。16 个 legacy Notion
身份的建议为 `replace-with-bukit-notion`，`ValueCoercion` 为独立 review；认证搜索的
最终状态均为 `no-public-match-found`。

simple-name 查询包含已审阅的 lexical false positives，不能把查询原始返回数当成真实
CLR consumer。private、未索引或未自愿声明的消费者继续是
`unknown-until-voluntary-declaration`。Task 14/15 只能更新 current baseline 和新决议
台账，不得重写历史 manifest。

## 8. serialization、reflection 与 Native AOT

### 8.1 serialization

没有发现 13-model、nested token 或 `ValueCoercion` 的 JSON attributes、
`JsonSerializerContext` registration 或产品 runtime generic serializer root。
block JSON 由 canonical `NotionBlockJsonWriter` 的 explicit type switch 生成，不依赖
record reflection serialization。

外部消费者仍可能自行使用 generic serializer；这属于 private unknown compatibility
风险，不能由仓库内缺少 serializer registration 推导为不存在。

### 8.2 reflection

仓库内 reflection roots 是治理/测试证据：

- 枚举 legacy/canonical concrete block types以检查 mapper cases；
- `Type.GetType` 检查旧 full names；
- `GetExportedTypes` 检查 legacy namespace exact surface；
- public API baseline generator 记录 generated record members。

这些 root 在 Task 14/15 必须按决议迁移，但不构成产品动态工厂。

### 8.3 AOT

13-model graph 当前由 public converter → canonical converter → compatibility mapper
静态到达；mapper switch 静态引用全部已知 derived types。它们不是只靠 reflection
保活的类型。

三个 tokenizer types 同样由 wrapper 的直接构造路径静态到达；删除 wrapper 后，
canonical tokenizer 必须由 real published consumer fixture证明仍可达。

`ValueCoercion` 没有 production reachability；internalize 不应新增动态注册。Task 20
仍必须以真实 Native AOT build、release-artifact smoke 和 published fixture 证明最终
G2 graph，不得只以普通 JIT 单元测试替代。

## 9. 测试缺口

现有测试不足以单独批准实施，Task 14/15 至少需要补齐：

### Task 14

- architecture：13 model 仍 public/exported且已重分类；3 legacy tokenizer identities
  不存在；canonical tokenizer/nested types仍 public；
- public signature：retained `HtmlToNotionBlockConverter.Convert` 仍精确返回 legacy
  `List<NotionBlock>`；
- record：constructor defaults、deconstruct shape、base/derived closure、mutable-list
  reference equality与deep-equivalence区别；
- tokenizer：四个 enum ordinals、new `HtmlToken()` 默认值、全部 token kinds、
  unmatched `<`/missing `>`、empty/whitespace、null异常类型；
- canonical converter：常见 HTML、FAQ/toggle、rich text、image/link safety、
  pre/code completion和JSON输出不变；
- governance：current baseline精确 `14/493/68`，closed manifest blob不变。

### Task 15

- `ValueCoercion` internal/not exported；
- null、boolean、大小写白名单、whitespace、number、custom `ToString`、unknown fallback；
- current baseline精确 `14/492/67`；
- 不增加新的 friend assembly 或 production consumer。

所有 owner tests、public API drift、唯一 G2 aggregate、AOT 与轻量只读复审统一留给
Task 20。本审计没有运行测试，也不预称这些证据已满足。

## 10. Task 14 精确边界

Task 14 只允许：

1. 原子删除 Shared `HtmlTokenizer`、`HtmlToken`、`HtmlTokenType` 三个 legacy identities；
2. 将仍需要的 tokenizer consumer/tests迁到 canonical owner；
3. 保留 13 个 model definitions 与 public accessibility；
4. 把 13 项 current baseline分类改为
   `cross-assembly-implementation / 1.x-do-not-narrow`；
5. 更新 architecture tests、现行治理文档与 Task 14 resolution ledger；
6. 得到目标 baseline `14/493/68`。

Task 14 不允许：

- 删除/internalize任一13-model identity；
- 修改 retained `HtmlToNotionBlockConverter.Convert` signature；
- 删除 mapper/writer或改变 block JSON；
- 处理 `ValueCoercion`；
- 改写1.x facade freeze为“2.0无条件可删除”；
- 进入 Content、Engine、schema、config、plugin 或 path 行为。

若未来要移除 model graph，必须另立明确批准的原子任务，同时纳入 converter public
signature、mapper、writer、13 identities、consumer migration与兼容版本策略。

## 11. Task 15 精确边界

Task 15 必须以 Task 14 的 `14/493/68` 为起点，仅：

1. 将 `Bukit.Shared.ValueCoercion` 从 public 改为 internal；
2. 保持三个方法及所有 coercion 语义不变；
3. 更新 owner tests、architecture guard、current baseline和 resolution ledger；
4. 得到目标 baseline `14/492/67`。

不得在 Task 15：

- 重命名、移动或抽象该 utility；
- 修改 string whitelist、culture、number 或 null 规则；
- 顺带处理 Notion model/tokenizer；
- 添加全局 conversion service、DI abstraction或新 friend assembly。

## 12. 禁止漂移

G-04D4 全程禁止顺带修改：

- canonical Notion block/tokenizer行为或 public shape；
- HTML link/image safety、entity decoding、JSON payload或截断语义；
- record constructors、defaults、inheritance、equality/deconstruct；
- Content/Engine业务逻辑；
- schema、config、plugin protocol、媒体、SEO、路径工具或build reports；
- CI、release或gate脚本；
- closed consumer-declaration manifest。

环境或测试失败不能授权改 production 行为迎合断言。

## 13. 停止条件

出现任一情况时必须停止对应实施，不得申请关闭：

1. 发现 direct external CLR consumer、private declaration、外部 `NotionBlock` subclass、
   public/protected signature、runtime serializer/reflection/AOT registration；
2. Task 14 若不改变 retained public `Convert` 就无法编译，说明发生了 model 越界收窄；
3. 13-model graph不能保持完整 public companion关系；
4. tokenizer migration改变 nested default、enum ordinal、token shape、异常或解析行为；
5. `ValueCoercion` internalize要求改变其语义或新增生产抽象；
6. Task 14 baseline不是精确 `14/493/68`，或 Task 15 baseline不是
   `14/492/67`；
7. historical manifest被修改或 blob漂移；
8. Task 20 owner tests、public API drift、唯一 aggregate、Native AOT或独立复审存在
   未关闭 failure/finding。

若第1项发生，相关类型必须改为 retained 或进入独立 obsolete/declaration window；
不得用扩大 Task 14/15 修复面来消除阻断。
