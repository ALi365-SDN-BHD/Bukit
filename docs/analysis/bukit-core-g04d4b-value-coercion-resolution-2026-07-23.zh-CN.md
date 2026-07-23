# Bukit Core G-04D4B ValueCoercion resolution

> 日期：2026-07-23
>
> 范围：G2 Task 15
>
> 状态：implementation complete / group-verification-pending

## 1. 执行摘要

Task 15 只对
[`Bukit.Shared.ValueCoercion`](../../src/Bukit-Core/Bukit.Shared/ValueCoercion.cs)
执行一个 token 的可见性变化：

```diff
-public static class ValueCoercion
+internal static class ValueCoercion
```

三个方法及其方法体保持原样：

- `IsTruthy(object?)`
- `IsFalsy(object?)`
- `ToBooleanOrNull(object?)`

本任务不删除、移动、重命名或抽象该工具，不修改 member modifier，不新增调用方、
转换服务、DI abstraction 或 friend assembly。

Task 15 以 Task 14 的 `14/493/68` 为起点，目标 current baseline 为：

```text
14 assemblies / 492 public types / 67 candidates
```

计算关系是：

- `ValueCoercion` 不再 exported：public types `493 - 1 = 492`；
- 该类型退出 `2.0-candidate`：candidates `68 - 1 = 67`。

源码实现已完成，但所有 owner tests、public API drift、Native AOT、G2 aggregate gate 和
只读复审统一留给 Task 20。因此本任务不能提前标记为组级关闭。

## 2. 资格与 owner

Task 13 的全仓扫描确认：

| 证据面 | 结果 |
|---|---|
| Core production直接调用 | 0 |
| Labs production直接调用 | 0 |
| Plugin production直接调用 | 0 |
| test直接调用 | 仅 `Bukit.Shared.Tests/ValueCoercionTests.cs` |
| active guide/API承诺 | 0 |
| public/protected signature传播 | 0 |
| protected members | 0 |
| serializer/source-generator注册 | 0 |
| product reflection/AOT full-name root | 0 |

该类型由 `Bukit.Shared` owner管理，当前 baseline分类是：

```text
implementation-public / 2.0-candidate / 2.0-review
```

它没有被其它 public constructor、parameter、return type、base type、generic constraint
或 nested public graph传播。`Bukit.Shared.Tests` 已通过既有精确
`InternalsVisibleTo`访问 Shared internals；internalize不需要扩大 assembly边界。

## 3. 精确语义合同

Task 15 是 visibility-only change。实现继续先对非 boolean输入执行：

```text
value.ToString()?.Trim()
```

然后只匹配固定 ASCII token。不得把它重写为 invariant conversion、typed numeric
conversion、`bool.TryParse` 全替代或任意大小写匹配。

### 3.1 null、boolean 与空值

| 输入 | `IsTruthy` | `IsFalsy` | `ToBooleanOrNull` |
|---|---:|---:|---:|
| `null` | false | true | false |
| `true` | true | false | true |
| `false` | false | true | false |
| `""` | false | true | false |
| whitespace | false | true | false |

`ToBooleanOrNull(null)` 返回 false，而不是 null。这是既有行为，即使方法名可能让调用者
产生其它直觉，本任务也不调整。

### 3.2 truthy whitelist

仅以下 token返回 true：

```text
true, True, TRUE
yes, Yes, YES
1
on, On, ON
```

前后 whitespace在匹配前被 trim。

### 3.3 falsy whitelist

仅以下 token返回 false：

```text
false, False, FALSE
no, No, NO
0
off, Off, OFF
```

前后 whitespace在匹配前被 trim。

### 3.4 unknown fallback

不在两个 whitelist的值具有以下三态语义：

```text
IsTruthy(value)         == false
IsFalsy(value)          == false
ToBooleanOrNull(value)  == null
```

例如 `"maybe"`、`42` 和混合大小写 `"tRuE"` 都是 unknown。现有实现不是完整的
`OrdinalIgnoreCase` parser；Task 15 不得借内部化扩大 token集合。

### 3.5 number 与 culture

数字没有 typed truthiness规则，而是经过运行时 `ToString()`：

- 字符串结果为 `"1"` 时 truthy；
- 字符串结果为 `"0"` 时 falsy；
- 其它结果通常为 unknown；
- 非零数字不是普遍 truthy；
- 当前 culture可能影响 decimal/floating-point的字符串结果；
- 本任务不改为 `InvariantCulture` 或 `Convert.ToBoolean`。

同样，自定义对象的 `ToString()`结果继续参与判断；若 `ToString()` 返回 null，则按空值
处理；若其抛出异常，异常继续原样传播。

## 4. 为什么不删除或建立 canonical abstraction

仓库存在多个用途特定的 boolean parser，但都不是等价替代：

- `ContentFieldReader.GetBool` 对 null、非零数字和大小写的语义不同；
- taxonomy sort parser返回非 nullable bool，token与fallback不同；
- Environment/Wechat helper只处理环境字符串；
- Config/YAML parser受 schema约束，只接受各自配置合同。

把这些实现合并会建立新的全局 conversion contract，并改变现有调用方语义。Task 15
的目标只是缩减意外 public CLR surface，不是设计通用转换框架。

删除 `ValueCoercion` 虽然当前没有 production调用，但会丢失已由 owner tests固定的语义，
也不符合“null、number、boolean、culture和fallback不变”的验收条件。原位 internalize
是最小且可逆的治理动作。

## 5. 消费者与 historical evidence

closed 136-entry consumer-declaration manifest继续保留
`Bukit.Shared.ValueCoercion` 的历史记录：

- authenticated full-name搜索返回 0；
- simple-name搜索结果均为其它语言/项目的词法碰撞；
- truncation-resolution搜索未发现 Bukit exact consumer；
- `declarationStatus = consumer-declaration-pending`；
- `privateConsumerStatus = unknown-until-voluntary-declaration`。

“没有已确认公开命中”不等于“没有消费者”。private repository、未索引源码、
预编译 binary、反射和未自愿声明的 consumer始终不可完全观测。

因此 internalize仍是明确发生在 2.0 分支的 source/binary/reflection breaking change，
而不是因为仓内引用为零就宣称该类型从未构成可观察 API。

## 6. 兼容性边界

### 6.1 source

外部 source consumer不能再引用：

```csharp
Bukit.Shared.ValueCoercion.IsTruthy(...)
Bukit.Shared.ValueCoercion.IsFalsy(...)
Bukit.Shared.ValueCoercion.ToBooleanOrNull(...)
```

仓内 owner tests通过既有 IVT继续编译。没有 production consumer需要迁移。

### 6.2 binary

旧 binary中的 public type/member token不能在新 assembly public surface上继续使用；
consumer必须重新编译或自行保留原逻辑。本任务不提供 forwarding或 obsolete facade。

### 6.3 reflection

类型仍存在于 `Bukit.Shared` assembly，但：

- `Assembly.GetType("Bukit.Shared.ValueCoercion")` 仍可解析；
- `Type.IsNotPublic` 为 true；
- `Assembly.GetExportedTypes()` 不再包含它；
- 三个 static method仍保持原签名。

这应描述为“internal/not exported”，不能描述为“full name不可反射”。

## 7. serialization 与 Native AOT

`ValueCoercion` 没有：

- JSON/YAML serialization attribute；
- `JsonSerializerContext` registration；
- reflection factory或dynamic activation；
- source-generator input；
- trimmer descriptor或`DynamicDependency`；
- production static caller。

internalize不要求添加任何 AOT保活配置。由于没有 production reachability，Native AOT
可以正常裁剪该工具；测试中的直接 static调用和 architecture reflection只属于测试
证据，不是产品 runtime root。

Task 20仍必须运行真实 Native AOT publish和published artifact smoke，以证明最终 G2
整图没有因 Shared public surface变化产生意外链接或 trimming回归。普通 JIT单元测试
不能替代该证据。

## 8. baseline 与 historical manifest

current baseline只允许删除一项：

```text
Bukit.Shared.ValueCoercion
```

不得借 Task 15 重分类或移除 Notion、CLI Shared或其它 Shared类型。目标必须精确为：

```text
14 assemblies / 492 public types / 67 candidates
```

closed historical manifest必须保持：

- `declarationState = closed`；
- `candidateCount = 136`；
- 136 entries全部存在；
- `ValueCoercion` 历史 entry不删除、不改写；
- Git blob `7b07d6890562387010b52301e9f8716e9bf10ed1` 不变。

current baseline记录当前可见面，historical manifest记录声明窗口关闭时的 cohort；
internalize不授权重写历史。

## 9. Task 20 待验证证据

Task 15 不单独运行测试。Task 20必须在 G2 aggregate中验证：

1. `ValueCoercion` exact full name仍能从 Shared assembly解析；
2. 类型为 internal且不在 exported types；
3. 三个 static method签名保持不变；
4. null、boolean、whitespace、truthy/falsy whitelist与unknown fallback不变；
5. mixed-case token仍是 unknown；
6. number继续按 `ToString()`结果而不是“非零即 true”解释；
7. culture变化没有被静默改成 invariant conversion；
8. custom `ToString()`结果、null与异常传播不变；
9. current baseline精确 `14/492/67`；
10. historical manifest和Git blob不变；
11. Shared IVT没有新增 assembly；
12. Core、Labs、Plugin production没有新增 consumer；
13. `Bukit.Shared.Tests`、`Bukit.Architecture.Tests`、public API drift、G2唯一
    aggregate targeted gate与Native AOT全部通过。

Task 20还必须完成一次 G2轻量只读复审。上述证据完成前，本任务状态保持
`group-verification-pending`。

## 10. 禁止漂移

Task 15不得顺带：

- 修改三个方法体或 member modifier；
- 修改 null、boolean、whitespace、number、culture或fallback语义；
- 将匹配改为完整 case-insensitive；
- 扩大 truthy/falsy token集合；
- 把任意非零数字解释为 true；
- 改用 `Convert.ToBoolean`、`bool.TryParse`、invariant parser或泛型转换；
- 删除、重命名、移动该类型；
- 新增 global conversion service、interface、DI或extension API；
- 修改其它相似 parser以“统一行为”；
- 处理 Notion model/tokenizer、CLI Shared或其它候选；
- 新增 production/test friend assembly；
- 修改 schema、config、plugin protocol、媒体、SEO、路径、CI、release或gate；
- 修改 closed historical manifest。

## 11. 停止条件

出现任一情况时，Task 15不能申请验证关闭：

1. 发现已确认的 production、external/private CLR consumer；
2. 发现 public/protected signature、serializer、reflection factory、source generator或
   AOT full-name root；
3. internalize需要新增 production IVT或修改调用方；
4. 语义 characterization不能在不改方法体的前提下通过；
5. 实施要求新增 canonical/global conversion abstraction；
6. current baseline不是精确 `14/492/67`；
7. historical manifest、136-entry计数或Git blob发生变化；
8. Task 20 owner tests、public API drift、aggregate targeted、Native AOT或只读复审存在
   未关闭 failure/finding。

若出现真实 consumer或动态 identity root，应停止收窄，选择 retained-by-design或独立
obsolete/declaration window；不得扩大 Task 15 来迁移无关模块。

## 12. 正式关闭台账

| 类型 | production diff | current状态 | contract owner | 后续 |
|---|---|---|---|---|
| `Bukit.Shared.ValueCoercion` | `public` → `internal`，仅一个 token | implementation complete / group-verification-pending | Bukit.Shared | Task 20完整验证与复审 |

Task 15 实现边界至此完整；其关闭申请必须等待 Task 20。
