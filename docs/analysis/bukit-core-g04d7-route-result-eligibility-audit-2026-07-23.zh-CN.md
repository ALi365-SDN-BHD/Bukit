# Bukit Core G-04D7 Routing result 资格审计

> 日期：2026-07-23
>
> 范围：G3 Task 25，只审计 Bukit Core，不实施 Task 26/27
>
> 状态：eligibility complete / resolution-and-group-verification-pending

## 1. 执行摘要

`Bukit.Routing.RouteGenerator.RouteGenerationResult` **不能按 G-04C
`RouteInventoryInspectEntry` 的“零职责类型直接删除”路径处理，也不能只把类型改成
`internal`**。它是公开
`RouteGenerator.GenerateWithSource(...)` 的返回类型，Routing 在每次带来源的路由生成中
创建它，`Bukit.Engine` 再跨程序集消费并投影来源字符串。

本次资格结论为：

```text
eligible-migrate-and-remove
not eligible for visibility-only internalization
not eligible for unpaired deletion
```

推荐 Task 26 采用一个原子返回契约迁移：

```csharp
public static (RouteInfo Route, RouteSource Source) GenerateWithSource(...)
```

即保持方法名、参数、tuple 元素名、`RouteInfo`、`RouteSource`、路由计算与来源枚举语义，
只把返回承载从 nested record 改成命名 `ValueTuple`，随后删除
`RouteGenerationResult` identity。该方案与同模块私有
`GenerateBaseRouteWithSource(...)` 以及 Engine 已公开的
`RouteInventoryValidator.GenerateRouteWithSource(...)` tuple 形状一致，不需要新增 public
facade、程序集依赖或 `InternalsVisibleTo`。

这仍然是明确的 2.0 CLR breaking change：

- 二进制方法返回类型改变；
- 显式声明 `RouteGenerationResult`、调用其构造器、使用 `with`、依赖 reference/null
  语义、反射 full name 或直接序列化该 record 的消费者必须迁移；
- 使用 `var` 后访问 `.Route/.Source`，或直接 deconstruct 的典型 C# 源码可继续保持相同
  调用形状，但必须重新编译，不能据此声称完全 source/binary compatible。

仓库和已认证公开搜索没有观察到具体候选消费者，但 private consumer 仍未知。因此这是
2.0 migration-contract 资格，不是“无人使用”证明。

## 2. 当前 identity 与公开签名传播

定义集中在
`src/Bukit-Core/Bukit.Routing/RouteGenerator.cs`：

```text
RouteGenerator
├── RouteSource
│   ├── FullOverride = 0
│   ├── PartialOverride = 1
│   ├── Collection = 2
│   └── Permalink = 3
├── RouteGenerationResult(RouteInfo Route, RouteSource Source)
├── Generate(...) -> RouteInfo
└── GenerateWithSource(...) -> RouteGenerationResult
```

`RouteGenerationResult` 是 public sealed positional record。除 `Route` 和 `Source`
属性外，编译器还生成 public constructor、deconstructor、clone、equality operators、
`Equals`、`GetHashCode` 和 `ToString`。其 CLR nested full name 是：

```text
Bukit.Routing.RouteGenerator+RouteGenerationResult
```

当前 public API baseline 同时记录：

```text
public static RouteGenerator.RouteGenerationResult GenerateWithSource(...)
public sealed class RouteGenerator.RouteGenerationResult ...
```

因此只把 record 改成 `internal` 会立即形成 public method 的 inconsistent
accessibility，编译即失败。只删除 record 而不改变返回签名同样不可行。任何决议都必须
把 public method 返回签名与承载 identity 作为一个原子图处理。

## 3. 运行时创建、返回和 Core 消费链

### 3.1 Routing 内部创建

`RouteGenerator.Generate(...)` 本身通过 `GenerateWithSource(...).Route` 取得普通
`RouteInfo`。私有 overload 根据路由优先级创建 result：

1. 完整 route override：`RouteSource.FullOverride`；
2. collection/permalink 基础路由后存在部分 override：
   `RouteSource.PartialOverride`；
3. collection rule：`RouteSource.Collection`；
4. type permalink：`RouteSource.Permalink`。

所有分支都先经过现有 route/path security validation，再创建 result。record 没有额外
校验、缓存、生命周期、异常转换或业务方法；其职责只是在方法边界携带两个值。

### 3.2 Engine 跨程序集消费

唯一 Core 跨程序集 production consumer 位于：

```text
RouteInventoryValidator.GenerateRouteWithSource
  -> BuildCollectionRules
  -> RouteGenerator.GenerateWithSource
  -> result.Route
  -> result.Source.ToString()
  -> (RouteInfo Route, string Source)
```

`RouteGenerationResult` 没有继续传播到 Engine 的 public signature。Engine 对外暴露的
是另一个 tuple，并把 enum 转成字符串。其余三个 Engine production 路由调用点只使用
`RouteGenerator.Generate(...) -> RouteInfo`，不接触候选 identity。

这说明：

- 候选不是零 production reference；
- Core 跨程序集依赖是真实的，但只依赖 `Route`/`Source` 两值；
- 改为 `(RouteInfo Route, RouteSource Source)` 后，当前 `var result`、属性访问和
  deconstruction 调用形状均可保留；
- 不需要让 Engine 成为 Routing internals friend，也不应把来源判断复制进 Engine。

### 3.3 Labs 与仓内插件边界

只读搜索未发现 `src/Bukit-Labs/` 或 `src/Bukit-Plugins/` 直接引用：

- `RouteGenerationResult`；
- `RouteGenerator.GenerateWithSource`；
- `RouteGenerator.RouteSource`；
- `Bukit.Routing` project/namespace。

本任务不修改、不测试 Labs 或插件。该阴性结果只说明当前仓内直接引用未观察到，不能
替代 private consumer 声明，也不能扩大 Task 26 的 Core-only 修复范围。

## 4. 序列化、反射与持久化契约

### 4.1 候选没有仓内 serializer root

Core 与相关测试未发现候选 identity 被以下机制注册或按 full name 解析：

- `JsonSerializable` / source-generated JSON context；
- `JsonDerivedType`；
- `DataContract` / `KnownType`；
- `YamlMember`；
- `Type.GetType` / `Activator.CreateInstance`；
- 手写候选 full name 的反射装载。

`RouteGenerationResult` 本身也没有 JSON/YAML/data-contract attribute。

### 4.2 route golden snapshot 不序列化候选

`RouteGeneratorGoldenTests` 对 `GenerateWithSource` 结果执行 deconstruction，然后构造
测试私有 `RouteInventoryItem`。测试读取的
`tests/Bukit.Engine.Tests/Snapshots/RouteGenerator/route-generator.golden.json`
持久化的是：

```text
url / outputPath / template / routeSource / collection / type
```

其中 `routeSource` 来自 `source.ToString()`。被 JSON 反序列化的是测试私有 snapshot
DTO，不是 `RouteGenerationResult`。因此仓内没有需要保持 record JSON property shape
的正式持久化证据。

但外部消费者若曾直接用 `System.Text.Json` 序列化 public record，迁移到
`ValueTuple` 会改变其默认 JSON 行为；这正是必须在 2.0 migration note 中写明的破坏
面，不能把仓内无 root 扩张为外部无序列化。

### 4.3 AOT 风险

候选不在 source-generation root，不是 Native AOT 反射 root。删除它不应要求新增
serializer metadata。Task 26 不得为“兼容”引入反射 serialization、dynamic
activation 或新 JSON schema。真实 AOT 证明留到 G3 组级验证，Task 25 不运行。

## 5. 现有测试盘点

| 测试面 | 当前证据 | 缺口 |
|---|---|---|
| `Bukit.Routing.Tests` | 仅通过 `Generate` 验证一个 collection permalink | owner tests 没有直接覆盖 `GenerateWithSource`、返回 shape 或四种来源 |
| `Bukit.Engine.Tests/RouteGeneratorTests` | 直接断言 `Permalink` source 与 route URL；大量 `Generate` 行为测试覆盖 precedence、安全和 encoding | 只直接断言一个 source enum 分支；没有显式返回类型迁移契约 |
| `RouteGeneratorGoldenTests` | deconstruct 结果并冻结 route/source 字符串 snapshot | 名为 `partial-override` 的 fixture 同时给出 URL 与 template，实际判定为 `FullOverride`；snapshot 没有覆盖 `PartialOverride` |
| `RouteInventoryValidator` tests | 现有 Engine 路由测试覆盖 inventory、collision 等邻接行为 | 没有直接冻结 public `GenerateRouteWithSource` 的 tuple/source-string 投影 |
| Architecture tests | dependency matrix 加载 Routing assembly；public API drift 会观察 breaking change | 没有 G-04D7A 专项断言候选 identity 消失、返回 tuple shape、enum ordinals及 baseline 分类 |

Task 26 的最小补测集合应为：

1. 在 `Bukit.Routing.Tests` 通过 public `GenerateWithSource` 分别验证
   `FullOverride`、`PartialOverride`、`Collection`、`Permalink`；
2. 验证命名 tuple 的 `Route`、`Source` 和 deconstruction，且
   `RouteSource` 四个 ordinal 不变；
3. 在 Engine tests 冻结
   `RouteInventoryValidator.GenerateRouteWithSource` 的 route 与 exact source string；
4. 修正 golden fixture，使真正的 partial override 只覆盖部分字段，并保持现有
   `routes.v1` snapshot schema；
5. 新增 `G04D7ARouteGenerationResultTests`，断言：
   - Routing assembly 不再导出 nested result；
   - `GenerateWithSource` 仍 public，参数/defaults 不变；
   - 返回类型为 `ValueTuple<RouteInfo, RouteSource>`；
   - tuple element metadata 为 `Route`、`Source`；
   - `RouteSource` 仍 public 且成员与 ordinal 不变；
   - 没有新增 `InternalsVisibleTo`；
   - public API baseline 与候选清单只发生批准的单项闭环。

这些测试只冻结返回承载和现有行为，不授权改变 route precedence、collision、locale、
path normalization、output encoding 或 security validation。

## 6. 与已删除 `RouteInventoryInspectEntry` 的明确区别

| 维度 | `RouteInventoryInspectEntry` | `RouteGenerationResult` |
|---|---|---|
| Assembly / identity | `Bukit.Engine` top-level record | `Bukit.Routing.RouteGenerator` nested record |
| Production 实例化 | 无 | Routing 每次带来源生成均创建 |
| Core 跨程序集消费 | 无 | `Bukit.Engine.RouteInventoryValidator` 真实消费 |
| public signature 传播 | 无真实 API 使用链 | `RouteGenerator.GenerateWithSource` 直接返回 |
| 行为承载 | 与实际 validator 私有 entry 无关 | 承载 route 与 source 返回值 |
| 删除前置条件 | 证明零职责后可单类型删除 | 必须先迁移 public 返回契约 |
| 兼容性 | 删除一个悬空 public identity | 同时改变 public method 二进制返回类型 |
| 可复用 G-04C 结论 | 不适用 | 明确禁止 |

G-04C 的关键证明是 public DTO 不被生产实现使用，实际 validator 使用另一个 private
entry。当前候选恰好相反：它在公开方法和真实 Core 调用链上。若 Task 26 仅复制 G-04C
的“删定义、改 baseline”步骤，编译或公共契约必然失真。

## 7. 决策方案比较

### 方案 A：命名 tuple 原子迁移并删除 record（推荐）

```csharp
public static (RouteInfo Route, RouteSource Source) GenerateWithSource(...)
```

优点：

- 删除唯一 Routing candidate，不增加替代 public type；
- 保持方法、参数、元素名、deconstruction 和 route/source 语义；
- Engine 当前消费方式只需重新编译，通常不需改调用源码；
- 与现有私有 tuple helper 和 Engine public tuple facade 一致；
- 不新增 IVT、程序集、schema、序列化或业务 abstraction。

代价：

- 明确 binary break；
- 显式 record consumer 和依赖 reference/record 语义的源码需要迁移；
- 必须 deliberate 更新 public API baseline 和消费者迁移说明。

### 方案 B：保留 record 并纠正治理分类（安全回退）

若 Task 26 发现 private consumer、正式 record serialization contract 或迁移窗口条件
不足，则保留当前 public record，并把它纠正为：

```text
cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review
```

这会让候选获得明确 retained 终态，但不会缩减 public type count。不得为了“候选归零”
隐瞒其真实 public signature 角色。

### 方案 C：internalize record 和方法、增加 IVT（不推荐）

该方案会删除一个对外方法，扩大 friend assembly，令测试或 Engine 依赖 internals，并
迫使外部调用迁往 Engine facade。它比方案 A 破坏更大，也没有功能或架构收益。

### 方案 D：新增另一个 public DTO/facade（不推荐）

移动或重命名 public identity 不能减少公共面，只会制造双轨迁移。当前两个值已有
`RouteInfo`、`RouteSource` 和命名 tuple 足够表达，不应新建抽象。

## 8. Task 26 兼容性与受控实施边界

若采用方案 A，Task 26 只应：

- 删除 `RouteGenerationResult` 定义；
- 把 public/private `GenerateWithSource` 返回类型改为同一个命名 tuple；
- 把三个 `new RouteGenerationResult(...)` 改成 tuple 返回；
- 保持 `Generate(...)`、Engine consumer 与所有参数/defaults 不变，除非编译器要求
  机械调整；
- 增加第 5 节列出的 Routing、Engine 与 architecture contract tests；
- 对这一个 breaking change deliberate 更新 baseline/candidate governance；
- 提供 2.0 迁移示例：

```csharp
// before: explicit identity
RouteGenerator.RouteGenerationResult result = RouteGenerator.GenerateWithSource(...);

// after: inferred or explicit named tuple
var result = RouteGenerator.GenerateWithSource(...);
(RouteInfo Route, RouteGenerator.RouteSource Source) explicitResult =
    RouteGenerator.GenerateWithSource(...);
```

不得顺带：

- 修改 `RouteInfo`、`RouteSource`、`CollectionRouteRule` 或 enum ordinal；
- 改方法名、参数、optional defaults、precedence 或 source assignment；
- 修改 route/outputPath normalization、encoding、安全检查或 collision 策略；
- 修改 locale/i18n、config schema、build report、插件协议或 CLI 输出；
- 引入新 JSON schema、serializer root、IVT、公共 facade 或程序集依赖；
- 处理 Labs、外部插件或任何与 Core Routing 无关的失败。

## 9. 停止条件

Task 26 遇到任一条件必须停止并回退到方案 B，不得超限修复：

1. 发现仓内或已声明 private consumer 显式依赖 record constructor、`with`、null、
   reference identity、record equality、reflection full name 或直接序列化；
2. public `GenerateWithSource` 无法保持现有参数/defaults、tuple 元素名或
   `RouteSource` 类型；
3. 编译要求新增 IVT、反向 project reference、新 public DTO 或跨模块复制路由逻辑；
4. 任一路由 precedence、`FullOverride/PartialOverride/Collection/Permalink` 来源、
   collision、locale、encoding 或安全验证发生行为漂移；
5. route golden snapshot 的 schema/业务内容除修正真实 partial fixture 外发生非预期
   变化；
6. public API drift 除批准的 result identity removal 和方法 return type replacement 外
   出现其他 breaking change；
7. Native AOT、Routing/Engine/Architecture 定向测试或 G3 aggregate 不能给出真实通过
   证据；
8. 修复需要进入 Labs、外部插件、配置 schema、插件协议或其他 owner 模块。

停止后应保留 public record、纠正分类并记录证据，不得以扩大修改范围换取门禁通过。

## 10. Task 25 验证声明

本任务只执行源码、public baseline、候选治理记录、测试与文档的只读检索，并新增本报告：

- 未修改生产代码、测试、snapshot、baseline 或候选 manifest；
- 未修改 Labs 或仓内插件；
- 未运行测试、coverage、public API drift、AOT、aggregate、full 或 release gate；
- 没有把已认证公开搜索的阴性结果解释为 private consumer 为零；
- 最终资格是“原子返回契约迁移后可删除”，不是 visibility-only 收窄授权。
