# Bukit Core G-04D7 Routing 决策汇总

> 日期：2026-07-23
>
> 任务：G-04D7 / master plan Task 25～27
>
> 状态：implementation-complete / group-verification-complete

## 1. 范围

G-04D7 只治理 `Bukit.Routing` 唯一的 current historical
`2.0-candidate`：

```text
Bukit.Routing.RouteGenerator+RouteGenerationResult
```

用户边界是 Bukit Core-only。Labs、外部插件、配置 schema、插件协议、asset URL 和全局
路径工具均不属于修复范围；D7 没有修改它们。

该候选与已删除的 `Bukit.Engine.RouteInventoryInspectEntry` 不同：它是 public
`RouteGenerator.GenerateWithSource(...)` 的真实返回类型，并由 Routing 创建、Engine
跨程序集消费。因此 D7 没有复用 G-04C 的零职责 DTO 删除结论。

## 2. 最终决策

| 候选 | Task 25 资格 | 当前终态 | 决策 |
|---|---|---|---|
| `RouteGenerator.RouteGenerationResult` | `eligible-migrate-and-remove`；禁止 visibility-only internalize | public identity 已删除；返回承载为 named tuple | D7A 原子迁移方法返回合同 |

最终 public method 形状：

```csharp
public static (
    RouteInfo Route,
    RouteGenerator.RouteSource Source)
    RouteGenerator.GenerateWithSource(...);
```

Task 26 同时完成：

- 删除 nested record identity；
- public/private `GenerateWithSource` 返回同一命名 tuple；
- 三个 result construction 改为 tuple literal；
- 保持 Engine `var result`、`.Route/.Source` 消费源码；
- 不新增 replacement DTO、facade、type forwarding、friend assembly、serializer root
  或程序集依赖。

实现与决议证据：

- `2200cb2f450dc7f39c5ed52753975a98443d9095`
- [G-04D7 eligibility audit](bukit-core-g04d7-route-result-eligibility-audit-2026-07-23.zh-CN.md)
- [G-04D7A decision ledger](bukit-core-g04d7a-route-result-resolution-2026-07-23.zh-CN.md)

## 3. 兼容性终态

这是明确的 2.0 binary break。所有消费者必须重新编译。

重新编译后可保持的典型调用：

```csharp
var result = RouteGenerator.GenerateWithSource(document);
var route = result.Route;
var source = result.Source;

var (deconstructedRoute, deconstructedSource) =
    RouteGenerator.GenerateWithSource(document);
```

需要迁移的旧依赖包括：

- 显式 `RouteGenerationResult` 类型声明；
- record constructor；
- `with` expression；
- null、reference identity 或 record equality；
- clone、record `ToString()`；
- full-name reflection/activation；
- 直接按 record properties 序列化。

公开搜索没有观察到精确 Bukit CLR consumer，但 private、未索引或未声明 consumer 继续为
`unknown-until-voluntary-declaration`。D7 没有把阴性搜索解释为外部消费者为零。

## 4. 行为与返回合同

D7A 保持四种 `RouteSource`：

| source | 触发合同 |
|---|---|
| `FullOverride = 0` | URL + template 完整 route override |
| `PartialOverride = 1` | collection/permalink 基础 route 后应用部分 override |
| `Collection = 2` | matching collection rule |
| `Permalink = 3` | matching type permalink |

保持不变：

- `Generate(...) -> RouteInfo`；
- `GenerateWithSource` 方法名、参数顺序与 optional defaults；
- named tuple 元素名 `Route`、`Source`；
- `RouteInfo` 与 public `RouteSource` identity；
- required collection 检查；
- full override、collection、permalink、partial override 的 precedence；
- URL/outputPath normalization 与 encoding；
- route/path security validation；
- exception 与 diagnostic；
- Engine validator 的 `(RouteInfo Route, string Source)` 投影。

Engine golden 中原名为 partial 的 fixture 实际同时给出 URL/template，因而命中
`FullOverride`。D7A 只把该测试输入改成 URL-only，snapshot 相应冻结
`PartialOverride` 和继承的 collection template；production 行为、`routes.v1` schema
与 version 未改。

## 5. Public surface 与历史证据

D7 开始前阶段值：

```text
14 assemblies / 486 public types / 60 candidates
```

D7A 后当前阶段值：

```text
14 assemblies / 485 public types / 59 candidates
```

Routing current candidate 数：

```text
0
```

current public API baseline：

- 不再包含 `RouteGenerationResult`；
- `RouteGenerator.GenerateWithSource` member 使用
  `System.ValueTuple<RouteInfo, RouteSource>`；
- `RouteGenerator`、`CollectionRouteRule`、`RouteSource` 和相邻 Routing types 保持。

所有现行 G-04 architecture current-count guards、`docs/governance` 与 `guide/dev` 已同步
到阶段值 `485/59`。

closed historical consumer manifest 保持：

```text
candidateCount = 136
candidates.length = 136
RouteGenerationResult historical entries = 1
Git blob = 7b07d6890562387010b52301e9f8716e9bf10ed1
```

文件内容未修改。历史条目继续保留公开搜索与 private-consumer 限制证据。

## 6. 实现阶段建立的验证合同

Routing owner tests 已建立：

- `FullOverride`；
- `PartialOverride`；
- `Collection`；
- `Permalink`；
- named tuple `.Route/.Source`；
- explicit deconstruction。

Engine tests 已建立：

- 真实 partial golden snapshot；
- `RouteInventoryValidator.GenerateRouteWithSource` 的 URL、outputPath 与 exact source
  string 投影。

专项 architecture guard 已建立：

- old nested identity 不存在、不导出；
- public method 返回 exact `ValueTuple<RouteInfo, RouteSource>`；
- `TupleElementNamesAttribute` 为 `Route`、`Source`；
- 参数/defaults 不变；
- enum public shape 与 ordinals 不变；
- Routing friend set 为空；
- current baseline/member 与 historical manifest/blob。

这些 tests/guards 在 Task 27 实现阶段没有运行；其通过状态由 Task 30 的组级验证确认。

## 7. 明确无范围漂移

D7 没有修改：

- route precedence 或 source assignment；
- collision/inventory algorithm；
- locale/i18n；
- URL/outputPath normalization、encoding 与安全；
- `RouteInfo`、`RouteSource`、`CollectionRouteRule`；
- Engine route pipeline；
- config schema、build report、CLI；
- serializer context、Native AOT/trimmer 配置或 reflection fallback；
- Labs、PluginHost 或外部插件；
- historical candidate manifest；
- D6/D7 历史报告的阶段证据。

任何相邻需求必须另立 Core 任务，不得借 Task 30 失败扩大修复范围。

## 8. Task 30 组级验证结果

Task 25～27 的实现阶段没有单独运行 tests、aggregate、Native AOT 或独立复审；Task 30
随后对完整 G3 diff 完成闭环：

1. `Bukit.Routing.Tests` 27/27，通过四种 source 与 tuple/deconstruction；
2. `Bukit.Engine.Tests` 1595/1595，通过 golden、validator 投影、route pipeline、
   collision、locale 与 security 回归；
3. `Bukit.Architecture.Tests` 215/215，通过 old identity absence、tuple metadata、
   parameters/defaults、enum ordinals、friend set、baseline 与 historical manifest；
4. public API drift 通过，只接受批准的 old type removal 与 return type replacement；
5. G3 最终 replacement aggregate targeted gate 完整通过；
6. real Native AOT package 与 release artifact smoke 通过，没有 missing type、
   reflection 或 trimmer regression；
7. 独立轻量只读复审结果为 Critical/Important/Minor `0/0/0`，确认没有 schema、
   插件、Labs 或相邻 Core 漂移。

code-analysis 首次识别到 Engine 消费点可使用命名 tuple 解构；唯一 production follow-up
把临时 result 改为 `(route, source)`，没有改变返回值、source 文本或路由行为。最终值
使用 Task 29 后的完整 G3 状态 `14/484/56`，没有把 D7 阶段值 `485/59` 固定为组终值。

## 9. 关闭条件

D7 只有在 Task 30 同时满足下列条件后才可关闭：

- route precedence、source、collision、locale、encoding 与安全合同不变；
- named tuple return metadata 与 Engine projection 通过；
- public API drift 与最终 current baseline 一致；
- closed manifest/blob 不变；
- no new IVT/public shim/reflection/serializer fallback；
- Native AOT 与 aggregate gate 通过；
- 复审无 Critical/Important finding。

若任一条件失败，应回到 D7A 原子迁移复审；不能顺带修复 Labs、插件、schema 或其他
Routing/Engine 行为。
