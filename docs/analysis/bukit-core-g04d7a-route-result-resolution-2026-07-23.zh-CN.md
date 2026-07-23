# Bukit Core G-04D7A RouteGenerationResult 原子迁移决议

> 日期：2026-07-23
>
> 范围：G3 Task 26；只处理 Bukit Core
>
> 基线：`9752286642246e84b9e4ee517ec6dac07f63df06`
>
> 状态：implemented / group-verification-pending

## 1. 决议

G-04D7A 从 Bukit 2.0 public CLR surface 删除：

```text
Bukit.Routing.RouteGenerator+RouteGenerationResult
```

该类型不是按 G-04C 的零职责 DTO 路径直接删除，而是与 public
`RouteGenerator.GenerateWithSource(...)` 返回签名形成一个原子迁移：

```diff
- public static RouteGenerationResult GenerateWithSource(...)
+ public static (RouteInfo Route, RouteSource Source) GenerateWithSource(...)
```

方法名、四个参数、optional defaults、tuple 元素名、`RouteInfo`、
`RouteGenerator.RouteSource` 和业务语义保持。没有新增 replacement DTO、facade、
`InternalsVisibleTo`、程序集依赖或 serializer root。

本变更是明确的 2.0 binary break。使用 `var` 后访问 `.Route/.Source` 或 deconstruct
的 C# 调用形状可在重新编译后保持；显式使用旧 record identity 的消费者必须迁移。

## 2. 精确 production diff

唯一 production 文件：

```text
src/Bukit-Core/Bukit.Routing/RouteGenerator.cs
```

只执行：

1. 删除 positional public sealed record 定义；
2. public `GenerateWithSource(ContentDocument, ...)` 返回命名 tuple；
3. private `GenerateWithSource(RouteContentSource, ...)` 返回同一命名 tuple；
4. 三个 `new RouteGenerationResult(...)` 改为 tuple literal。

保持不变：

- `Generate(...) -> RouteInfo`；
- `ExpandPermalinkPattern(...)`；
- `CollectionRouteRule`；
- `RouteSource` 名称与 ordinal；
- required collection 检查；
- full override 优先级；
- collection 先于 type permalink；
- partial override 应用位置；
- URL/outputPath normalization 与 encoding；
- `RouteSecurityValidator` 调用；
- exception 类型、diagnostic code 与消息；
- Engine 调用源码。

`Bukit.Engine.RouteInventoryValidator.GenerateRouteWithSource` 继续：

```text
RouteGenerator.GenerateWithSource
  -> result.Route
  -> result.Source.ToString()
  -> (RouteInfo Route, string Source)
```

named tuple element metadata 让跨程序集 `var result` 的 `.Route/.Source` 访问继续成立；
因此没有借迁移修改 Engine 实现。

## 3. 四种 route source 的 owner contract

`Bukit.Routing.Tests` 新增 public method owner tests，分别冻结：

| 场景 | 预期 source | 关键 route 断言 |
|---|---|---|
| URL + template 完整 override | `FullOverride` | URL、派生 outputPath、显式 template |
| URL-only partial override | `PartialOverride` | URL/outputPath 覆盖，继承 collection template |
| collection rule | `Collection` | collection permalink 与 template |
| type permalink | `Permalink` | 无 matching collection rule 时使用 type pattern |

collection 场景还显式使用：

```csharp
(RouteInfo route, RouteGenerator.RouteSource source) = result;
```

以编译期方式冻结命名 tuple 的 deconstruction。测试没有复制或重写 production source
判定逻辑。

## 4. Engine golden 与 facade 投影

原 `RouteGeneratorGoldenTests` 中名为 `partial-override` 的 fixture 同时给出 URL 与
template。按当前 production contract，它满足 full override 条件，所以 snapshot
正确记录成 `FullOverride`，但 fixture 名称没有真正覆盖 partial 分支。

本任务只修正测试输入：

- 保留 URL；
- 删除显式 template；
- 让 collection rule 提供 `pages/post.html`。

对应 snapshot 只改：

```diff
- "template": "pages/partial.html"
- "routeSource": "FullOverride"
+ "template": "pages/post.html"
+ "routeSource": "PartialOverride"
```

`routes.v1` schema、version、字段名、顺序、其余三项 route 和 JSON reader 均未改。
这不是 production 行为修复，而是把伪 partial fixture 改成真实 partial contract。

Engine tests 另补
`RouteInventoryValidator.GenerateRouteWithSource` 投影断言，冻结：

- permalink route URL；
- output path；
- exact source string `"Permalink"`。

## 5. Architecture guard

新增：

```text
tests/Bukit.Architecture.Tests/G04D7ARouteGenerationResultTests.cs
```

专项断言：

1. Routing assembly 不包含或导出旧 nested full name；
2. public `GenerateWithSource` 仍只有一个 overload；
3. 返回类型精确为
   `ValueTuple<RouteInfo, RouteGenerator.RouteSource>`；
4. return parameter 的 `TupleElementNamesAttribute` 精确为
   `Route`、`Source`；
5. 参数类型、顺序和 `"none"/null/null` defaults 不变；
6. `RouteSource` 仍为 public nested enum；
7. enum 名称与 ordinal 精确为：
   `FullOverride=0`、`PartialOverride=1`、`Collection=2`、
   `Permalink=3`；
8. Routing friend assembly 集合继续为空；
9. current baseline 为 `14/485/59`，旧 identity 不再存在；
10. `RouteGenerator` baseline member 使用 `System.ValueTuple`；
11. active governance 使用 current `485/59` statement；
12. closed manifest 保持 136 项、历史候选和 exact Git blob。

tuple 元素名不是 CLR field name；运行时字段仍是 `Item1/Item2`。因此 guard 读取 return
parameter attribute，而不是错误地寻找名为 `Route`/`Source` 的 tuple fields。

## 6. Public baseline 与治理同步

实施前：

```text
14 assemblies / 486 public types / 60 candidates
```

实施后：

```text
14 assemblies / 485 public types / 59 candidates
```

current baseline 的原子变化：

- 删除一个 `RouteGenerationResult` type entry；
- `RouteGenerator.GenerateWithSource` public member 从旧 record return 改为
  `System.ValueTuple<RouteInfo, RouteSource>`；
- `RouteGenerator`、`CollectionRouteRule`、`RouteSource` 及相邻 Routing entries 不改。

同步范围：

- 所有现行 G-04 architecture current-count guards 改为 `485/59`；
- `docs/governance/bukit-core-2.0-consumer-declaration.md`；
- `guide/dev/public-api-governance.md`；
- 两份 active governance 增加相同 D7A 决议与 migration wording。

没有回写 D6/D7 eligibility 或历史 resolution 报告的阶段数字。

closed
`docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`
保持不变：

```text
candidateCount = 136
candidates.length = 136
RouteGenerationResult historical entries = 1
Git blob = 7b07d6890562387010b52301e9f8716e9bf10ed1
```

## 7. 兼容性与迁移

典型迁移：

```csharp
// before
RouteGenerator.RouteGenerationResult result =
    RouteGenerator.GenerateWithSource(document);

// after
var result = RouteGenerator.GenerateWithSource(document);

(RouteInfo Route, RouteGenerator.RouteSource Source) explicitResult =
    RouteGenerator.GenerateWithSource(document);
```

必须重新编译。以下旧用法不兼容：

- 显式旧类型声明或构造器；
- `with` expression；
- `null` result；
- 依赖 record class/reference identity；
- 依赖旧 record equality、clone 或 `ToString()`；
- 通过 full name 反射或 activation；
- 直接按 record properties 序列化。

仓内没有旧 identity 的 serializer/reflection root，已认证公开搜索也没有确认具体候选
consumer；private、未索引或未声明 consumer 仍为
`unknown-until-voluntary-declaration`。本任务没有把阴性搜索解释为外部消费者为零。

## 8. 明确未修改

- `RouteInfo` 和 `RouteSource`；
- route precedence/source assignment；
- collection/permalink/override 业务逻辑；
- collision 与 inventory validation；
- locale/i18n；
- URL/outputPath normalization、encoding 与安全；
- config schema；
- build report；
- CLI；
- 插件协议、插件实现与 PluginHost；
- Labs；
- serializer context、AOT root 或 reflection fallback；
- historical candidate manifest；
- asset URL 或全局路径工具。

## 9. 静态验证状态

Task 26 按 master plan 不运行 tests、aggregate、AOT 或 review。提交前只执行静态检查：

| 检查 | 状态 |
|---|---|
| production diff | 仅 Routing 返回承载 |
| baseline JSON parse/count | 已确认 `14/485/59` |
| old identity/current entry | 已确认为 0 |
| current-count guard 残留 | 现行 guards 无旧 `486/60` 断言 |
| closed manifest/blob | `136/136`、历史候选 1 项、blob 精确不变 |
| JSON snapshot parse | schema/version 与真实 partial entry 已确认 |
| whitespace | `git diff --check` 通过 |
| staged scope | 28 个已批准路径，无 unstaged/untracked 或 historical manifest |
| tests / aggregate / AOT / review | **未运行；Task 30 pending** |

静态检查不是测试通过声明。

## 10. Task 30 待验证集合

D7A 进入 G3 唯一组级验证：

- `Bukit.Routing.Tests`；
- `Bukit.Engine.Tests`；
- `Bukit.Architecture.Tests`；
- 与 master plan 相邻的 Core 项目测试；
- public API drift；
- G3 唯一 aggregate targeted gate；
- real Native AOT package/smoke；
- 一次独立轻量只读复审。

直接验收：

1. 四种 source owner contracts 通过；
2. Engine golden 与 validator projection 通过；
3. route precedence、collision、locale、安全与 encoding 不回归；
4. named tuple return metadata 精确；
5. baseline 为 Task 26 阶段值 `14/485/59`，并在后续 D8/D9 后按组终值更新；
6. historical manifest/blob 不变；
7. AOT 不出现 missing type、reflection 或 metadata regression。

若后续验证发现 named tuple 迁移要求新增 IVT、新 public DTO、复制 Engine 路由逻辑或产生
未批准 public drift，必须停止并回退到 retain/reclassify；不得扩大修复范围。
