# Bukit Core G-04D6 Rendering 公共面资格审计

> 日期：2026-07-23
>
> 范围：G3 Task 21；只审计 Bukit Core，不实施 Task 22/23
>
> 基线：`b4a60b7ebeef34eda9f53e72a10a76ebc10c8544`
>
> 状态：eligibility complete / group-verification-pending

## 1. 执行摘要

G-04D6 的两个 historical `2.0-candidate` 都属于 Rendering 同程序集实现节点，
没有进入 Core 的 public/protected signature，也没有被 Engine 或 Theme Core 直接命名。
逐类型资格结论如下：

| 类型 | internalize 资格 | retain 资格 | migrate 资格 | Task 21 结论 |
|---|---|---|---|---|
| `Bukit.Rendering.Scriban.FileTemplateLoader` | **eligible**；只收窄 type accessibility | 无当前硬传播要求 | 无 canonical 替代类型，禁止迁移 | Task 22 可按单类型、单 token visibility 变更实施 |
| `Bukit.Rendering.Scriban.ScribanModelBinder` | **eligible**；只收窄 type accessibility | 无当前硬传播要求 | 已经是 internal mapper graph 的薄 facade，不需要另建 facade | Task 23 可按单类型、单 token visibility 变更实施 |

这里的 `eligible` 不是“没有兼容性风险”。两项从 public 改为 internal 都是 2.0
source、binary 与 reflection breaking change；private、未索引或未声明的 CLR consumer
仍然未知。结论仅表示：

1. 当前 Core 生产图不需要 public identity；
2. 没有 public signature 传播阻止收窄；
3. owner tests 可通过既有 `InternalsVisibleTo("Bukit.Rendering.Tests")` 继续验证；
4. 不需要新增 friend assembly、adapter、type forwarding 或替代 public facade。

当前 public API baseline 是：

```text
14 assemblies / 488 public types / 62 candidates
```

若 Task 22、Task 23 严格按本报告分别实施，预期投影为：

```text
Task 22: 14 / 487 / 61
Task 23: 14 / 486 / 60
```

该投影不是已实施结果，也不代表 Rendering、Engine、Theme、public API drift 或 Native
AOT 已通过。Task 21 不修改 production、tests、baseline、closed candidate manifest，
也不运行测试。

## 2. 审计边界与证据口径

本报告检查：

- `FileTemplateLoader` 的 Scriban `ITemplateLoader` 接口传播；
- `ScribanModelBinder` 的两个 overload 与 internal mapper graph；
- `Bukit.Rendering` 的 friend assembly 边界；
- Engine、Theme 与其他 Core assembly 的真实源码消费者；
- Rendering 和 Theme owner tests；
- template override/child/parent fallback、路径安全和缓存行为；
- 模板成员命名、null、dictionary/list 与 unsupported safe-object fallback；
- reflection 与 Native AOT 风险。

Labs 和外部插件不在本轮修复范围。只读搜索没有发现它们直接命名这两个候选；即使将来
发现匹配，也只能作为消费者证据重新触发停止条件，不能在 Task 22/23 修改 Labs 或插件。

closed 136-entry candidate manifest 是历史证据，不得重写。两项在该 manifest 中均为：

- `classification = implementation-public`；
- `compatibility = 2.0-candidate`；
- `proposedAction = review-only`；
- `declarationStatus = consumer-declaration-pending`；
- `privateConsumerStatus = unknown-until-voluntary-declaration`；
- 认证公开搜索未发现精确 Bukit CLR consumer。

`FileTemplateLoader` 的 simple-name 搜索存在大量其他语言/框架同名 false positive；
`ScribanModelBinder` 的 full/simple-name 查询均无公开匹配。公开搜索为零不是 private
consumer 为零，因此本报告只批准 2.0 独立决议，不回写“无外部消费者”。

## 3. 当前依赖与影响图

### 3.1 production 调用图

```text
Bukit.Engine
  -> ScribanTemplateRendererAdapter                 (internal)
     -> ScribanTemplateRenderer                     (retained public)
        -> new FileTemplateLoader(...)              (same assembly)
        -> ScribanModelBinder.ToScriptObject(...)   (same assembly)

FileTemplateLoader
  -> Scriban.Runtime.ITemplateLoader
  -> TemplateContext.TemplateLoader
  -> TemplateContextBuilder
  -> ComponentRenderFunction
  -> ThemeComponentRenderFunction
  -> RenderComponentFunction
  -> SectionRenderHelper

ScribanModelBinder
  -> ScribanRootModelMapper
     -> ScribanSiteModelMapper
     -> ScribanPageModelMapper
     -> ScribanListModelMapper
     -> ScribanSeoModelMapper
     -> ScribanCanonicalTrustModelMapper
     -> ScribanDynamicValueMapper
     -> ScribanDerivedListAliasProjector
```

所有 loader 的直接 production 参数/字段消费者都在 `Bukit.Rendering` 内，且相关 helper
均为 internal。所有 binder 的 production 调用也只在同程序集
`ScribanTemplateRenderer.RenderPage/RenderList` 内。

### 3.2 Engine friend access

`Bukit.Rendering/InternalsVisibleTo.cs` 当前只声明：

```text
Bukit.Rendering.Tests
Bukit.Engine
```

Engine 使用 friend access 的真实原因是 internal `ScribanTemplateRenderer` constructor
和 `ITemplateContextContributor` 等 Rendering 内部扩展路径。Engine 没有直接引用
`FileTemplateLoader` 或 `ScribanModelBinder`。

因此：

- 两项收窄后既有 Engine friend 已足够；
- 不得新增 `Bukit.Engine.Tests`、`Bukit.Theme` 或其他 friend；
- 不得把候选移动到 Engine 来“消除” friend；
- 不得借本任务删除既有 Engine friend；它仍承载其他内部合同。

### 3.3 Theme Core 与 tests

依赖方向是 `Bukit.Rendering -> Bukit.Theme`，不是 Theme Core 消费这两个候选。
`Bukit.Theme` production 没有直接引用它们。

`Bukit.Theme.Tests` 通过 public `ScribanTemplateRenderer` 验证旧主题、新主题及两条路径并存，
不直接构造 loader 或调用 binder。`Bukit.Rendering.Tests` 是两项的 owner test assembly，
已经由现有 friend 访问 Rendering internals。可见性收窄不要求改 Theme production，
也不要求新增 test-only public shim。

## 4. `FileTemplateLoader` 资格判定

### 4.1 public Scriban interface 不要求 implementation public

当前类型是：

```csharp
public sealed class FileTemplateLoader : Scriban.Runtime.ITemplateLoader
```

其四项 public surface 是 constructor、`GetPath`、`Load`、`LoadAsync`。
`ScribanTemplateRenderer` 把实例保存在 private field，并将其赋给
`TemplateContext.TemplateLoader`，该 property 的静态类型是 public Scriban
`ITemplateLoader`。没有 public/protected Bukit API：

- 返回 `FileTemplateLoader`；
- 接收 `FileTemplateLoader`；
- 以它作为 base、generic constraint、attribute 或 serialized type；
- 从 `ScribanTemplateRenderer` 暴露 loader field/property。

internal class 可以正常实现 public interface。Scriban 在运行时通过 interface virtual
dispatch 调用三个接口方法，不需要 implementation type 是 public。因此第三方接口传播
不是 retain 阻塞条件。

Task 22 推荐的最小实现只有：

```text
public sealed class -> internal sealed class
```

constructor 和接口方法不改签名、不改方法体。尤其不得顺带：

- 改成 explicit interface implementation；
- 新增自有 `ITemplateLoader` abstraction；
- 把 loader 移入 Engine 或 Theme；
- 改成继承、可替换或 DI public extension point；
- 删除 `sealed`；
- 调整 sync/async、cache 或异常行为。

### 4.2 CG-005 fallback 是行为合同，不是 CLR identity 合同

当前相对模板解析顺序是：

```text
overrideDir existing file
  -> rootDir/child existing file
     -> fallbackDir/parent existing file
        -> rootDir/child path（即使文件不存在）
```

`docs/compatibility-governance.md` 与中文版本把该 override/child/parent 链登记为
`CG-005 supported`。这要求 Task 22 冻结行为，但不要求 loader 的 concrete CLR
identity 保持 public。

必须保持：

- absolute template path 的当前处理；
- `/` 到当前平台 separator 的相对路径规范化；
- override 优先于 child，child 优先于 parent；
- 三层都不存在时返回 primary path，由上层产生现有 not-found 诊断；
- `Load`/`LoadAsync` 对不存在文件返回空字符串；
- 文件签名为 `LastWriteTimeUtc + Length`；
- sync/async 共享 case-insensitive cache；
- unchanged cache hit 返回相同 string instance；
- 当前异常类型和文本不变。

### 4.3 路径安全边界

`GetPath`、`Load` 和 `LoadAsync` 都在读取前把路径限制到 override/root/fallback 三个
logical root。安全 root 以 directory separator 结尾，避免简单 sibling-prefix
误接受；越界抛 `InvalidOperationException`。

Task 22 不得借可见性治理修改：

- root comparison 的现有 `OrdinalIgnoreCase`；
- symlink/realpath 策略；
- absolute-path policy；
- 路径异常分类或用户文本；
- component/section 的独立路径检查。

这些若需要安全强化，应另立 Core 安全任务。若 Task 22 的 visibility-only diff 需要改动
上述任一行为才能通过测试，应停止，而不是扩大修复。

### 4.4 owner test 现状与缺口

现有 `FileTemplateLoaderTests` 已覆盖：

- relative、absolute-inside、relative traversal、absolute-outside；
- sync/async existing、missing、outside-root；
- unchanged sync/async cache；
- override/child/parent 三种命中；
- forward-slash 输入。

`RenderComponentFunctionTests` 和 renderer tests 还从真实 Scriban 渲染路径覆盖 include、
component、section、路径逃逸与模板修改。

Task 22 应新增 architecture guard，并补齐下列 visibility/fallback 定向断言：

1. exact full name 存在但不在 `GetExportedTypes()`；
2. 类型仍为 sealed，仍实现 exact Scriban `ITemplateLoader`；
3. constructor 与三个接口方法 shape 不变；
4. override directory 已配置但目标缺失时，仍按 child、parent 顺序回退；
5. override/child/parent 全缺失时仍返回 primary path；
6. sync/async 继续拒绝三种 root 之外的路径；
7. public `ScribanTemplateRenderer` include 路径继续触发真实 interface dispatch。

不属于 Task 22 的现有证据缺口包括 symlink realpath、同 length/同 timestamp 内容变化、
跨平台 case-sensitive filesystem 和并发 cache invalidation。它们不能被 visibility
任务顺带“修复”，也不能以未覆盖为由扩大 Task 22。

## 5. `ScribanModelBinder` 资格判定

### 5.1 当前已是薄 facade

当前文件不再是旧审计中提到的 591 行热点；它只有两个 overload：

```csharp
ScriptObject ToScriptObject(PageModel model)
ScriptObject ToScriptObject(ListPageModel model)
```

二者都直接委托给 internal `ScribanRootModelMapper`。实际投影已经按 root/site/page/list/
SEO/canonical trust/dynamic value 拆分。另建第二个 public facade、移动 mapper 或继续以
“降低文件行数”为目标都没有架构价值。

public `ScribanTemplateRenderer` 的 `RenderPage`/`RenderList` 是外部支持入口；它们接收
`PageModel`/`ListPageModel` 并返回 HTML string，没有返回 `ScriptObject`，也没有暴露
binder identity。因此 binder 可 internalize，renderer 与模板 object shape 继续保留。

Task 23 推荐的最小实现只有：

```text
public static class -> internal static class
```

两个 overload 和 mapper 方法体保持不变。不得删除 facade 并让 renderer 分别调用
internal root mapper；那会使“visibility 变更”漂移成调用图重排，也会削弱单点架构 guard。

### 5.2 member naming 与 reflection

当前 mapper graph 没有使用：

- `ScriptObject.Import`；
- `StandardMemberRenamer` 或 custom `MemberRenamer`；
- `MemberFilter`；
- `Type.GetProperties/GetFields`；
- generic object reflection projection。

模板键由 `SetValue("literal_key", ..., readOnly: true)` 显式建立。snake_case、
兼容 camelCase alias（例如 `table_of_contents`/`tableOfContents`、
`route_prefix`/`routePrefix`）和 derived-list alias 都是源码明确合同，不由 CLR type
或 property visibility 推导。

所以 internalize binder 不应改变任何模板字段。Task 23 禁止借机：

- 重命名、删除或统一 snake/camel keys；
- 把 explicit mapping 改成 reflection import；
- 自动暴露新的 CLR property；
- 改变 `readOnly: true`；
- 改变 root global push 顺序；
- 改变 `page`、`site`、`seo`、`pages/items` 等 alias identity。

### 5.3 null、dictionary/list 与 safe-object fallback

当前显式投影语义包括：

- optional SEO/modules/data/data-index 缺失时按现有规则省略；
- 部分已声明 key 即使值为 null 仍存在；
- null dynamic value 保持 null；
- `IReadOnlyDictionary<string, object>` 与 `IDictionary<string, object>` 递归转为
  `ScriptObject`；
- 空白 dictionary key 被跳过；
- `IEnumerable<object>` 递归转为 `ScriptArray`；
- scalar string、bool、number、`DateTime`、`DateTimeOffset` 保留原 runtime value；
- `ModuleInfo` 使用 explicit safe projection；
- 其他不支持的 CLR object 只使用 `ToString()`，不会通过 reflection 暴露其 members。

最后一项是当前 safe-object fallback：它避免任意 content value 变成反射对象图。
Task 23 不得扩展支持类型集合、改变 enumerable covariance 行为、捕获 `ToString()`
异常、引入 culture conversion 或暴露未知 object members。

现有 `ScribanModelBinderTests` 对 page/list/site/SEO/canonical trust、nested data、
fields、null/empty optional、pages/items、pagination/collection/taxonomy/filter 和 aliases
有广泛 shape 覆盖，但以下边界仍应在 Task 23 定向冻结：

1. exact binder type internal、static、非 exported；
2. 两个 overload 的 exact parameter/return shape；
3. facade 与 internal root mapper 对 Page/List 两条路径等价；
4. null dynamic value；
5. read-only 与 mutable dictionary 两条递归路径；
6. nested dictionary/list 与空白 key；
7. 满足当前 `IEnumerable<object>` 分支的 object/reference-type array/list 所形成的
   `ScriptArray` shape，并明确 value-type generic sequence 仍走现有 fallback；
8. unsupported custom object 只暴露 `ToString()`，不暴露 property；
9. `ToString()` 抛异常时保持现有传播；
10. exact snake_case、兼容 camelCase 与 derived aliases，不新增或删除键。

非 null 标注的 `PageModel`/`ListPageModel` 顶层参数若被运行时强行传 null，目前没有独立
参数校验合同。Task 23 不得顺带新增 `ArgumentNullException` 或静默 fallback。

## 6. Native AOT 与 trimming 判定

### `FileTemplateLoader`

调用 root 是：

```text
ScribanTemplateRenderer constructor
  -> new FileTemplateLoader
  -> assign TemplateContext.TemplateLoader
  -> Scriban interface dispatch
```

constructor 是直接静态 root，接口实现由静态 metadata 连接，不依赖
`Activator.CreateInstance`、字符串类型名或 reflection factory。internalize 不应让
trimmer 移除实现。

### `ScribanModelBinder`

调用 root 是：

```text
ScribanTemplateRenderer.RenderPage/RenderList
  -> ScribanModelBinder.ToScriptObject
  -> explicit mapper graph
```

所有映射是直接静态调用和 literal key 写入，没有 reflection member discovery。
internalize 反而减少 public Scriban type 泄漏，不需要新增
`DynamicDependency`、`DynamicallyAccessedMembers` 或 trimmer descriptor。

但是“静态分析认为安全”不能替代产品证明。Task 30 必须执行总计划要求的 real Native
AOT package/smoke，并真实走 page/list 与 CLI build render 路径。若出现：

- missing method/type；
- Scriban interface dispatch 失败；
- 模板字段缺失或重命名；
- reflection/trimmer warning 新增；
- public renderer 无法从 Engine 到达 internal candidate；

应停止 G-04D6 关闭，不得通过恢复 reflection fallback、扩大 public surface 或禁用
trimming 来绕过。

## 7. 兼容性与迁移判定

### 7.1 2.0 compatibility

两项收窄会影响直接构造/调用它们的未声明 CLR consumer：

- `FileTemplateLoader` consumer 应迁到 retained public `ScribanTemplateRenderer`，而不是
  依赖 Core 内部路径/缓存策略；
- `ScribanModelBinder` consumer 应使用 public renderer 得到最终 HTML；Bukit Core
  当前没有承诺 public `ScriptObject` projection SDK。

这只是迁移说明，不要求在 Task 22/23 新增兼容 facade、obsolete forwarding type 或
public replacement API。若发现真实外部场景必须独立取得 `ScriptObject` 或注入 custom
loader，应先设计正式 Rendering extension contract，再决定 retain；不能把本任务变成
临时 SDK 设计。

### 7.2 1.x 与 docs

本决议只适用于 2.0 branch。不得回移到 1.x，不得宣称 1.x binary compatible。
CG-005 和主题模板文档描述的是模板行为，不是 concrete loader 的外部 CLR 支持承诺；
Task 22/23 不应修改 template syntax、theme manifest、schema、asset URL、插件协议或
用户主题文件。

## 8. Task 22/23 受控实施建议

### Task 22：G-04D6A

允许：

- 只把 `FileTemplateLoader` type 从 public 改为 internal；
- 增加 owner/architecture tests；
- 更新 current baseline 和现行治理台账；
- 创建 G-04D6A resolution 报告。

禁止：

- 方法体、fallback、cache、path policy、异常或 interface shape 变更；
- 新增 friend；
- 修改 Theme、Labs、插件或外部项目；
- 改 closed candidate manifest。

### Task 23：G-04D6B

允许：

- 只把 `ScribanModelBinder` type 从 public 改为 internal；
- 增加 owner/architecture tests；
- 更新 current baseline 和现行治理台账；
- 创建 G-04D6B resolution 报告。

禁止：

- 删除 facade、合并 mapper、重命名模板键；
- reflection import、safe-object 扩面或 null 语义修改；
- 新增 friend；
- 修改 public renderer/models、Theme、Labs、插件或 closed manifest。

Task 22 与 Task 23 是两个独立决议。任一项失败不授权把另一项一起回滚、一起改造或扩大
范围；Task 24 只汇总结论，Task 30 才运行 G3 统一测试与一次 aggregate gate。

## 9. 明确停止条件

Task 22 或 Task 23 遇到以下任一条件必须停止并重新审计：

1. 发现 Core public/protected signature 直接暴露候选 identity；
2. 发现 Engine、Theme 或其他 Core assembly 在没有现有 friend 可满足的情况下必须直接
   命名候选；
3. 需要新增 `InternalsVisibleTo`、public shim、type forwarding 或替代 public facade；
4. 需要改变 Scriban interface、constructor、overload 或返回类型才能编译；
5. 任一 fallback、路径安全、缓存、异常或模板 object shape 发生变化；
6. snake/camel keys、null、dictionary/list、unsupported object fallback 发生变化；
7. owner test、public API drift、Engine/Theme integration 或 Native AOT 出现候选相关回归；
8. 发现经过认证的真实外部 CLR consumer，或 private consumer 提交可验证声明；
9. 为通过测试必须修改 Labs、外部插件、schema、插件协议、asset URL 或全局路径工具；
10. 必须借机修复 symlink、cache invalidation、Scriban SDK 设计等相邻问题。

停止只表示当前 internalization 资格失效或证据不足，不自动得出 retain 或 migrate 结论。
应记录触发证据，另立窄任务，不允许超限修复。

## 10. Task 21 终态

| 检查项 | 结果 |
|---|---|
| `FileTemplateLoader` Scriban interface propagation | 不进入 Bukit public signature；internal implementation 合法 |
| `FileTemplateLoader` fallback/path behavior | CG-005 必须冻结；不构成 concrete type retain 理由 |
| `ScribanModelBinder` member naming | explicit literal projection；不依赖 CLR member reflection |
| binder null/dictionary/list/safe-object | 当前行为可冻结；Task 23 不得扩面 |
| Engine friend access | 已有 friend 足够；Engine 不直接命名两项 |
| Theme Core consumer | 无 direct candidate consumer |
| owner tests | 覆盖广，但需补 visibility、完整 fallback matrix 与 safe-object guards |
| Native AOT | 静态调用图 eligible；仍须 Task 30 实测 |
| `FileTemplateLoader` | **eligible internalize in Task 22** |
| `ScribanModelBinder` | **eligible internalize in Task 23** |

Task 21 到此关闭；production、tests、baseline 与 historical manifest 均未修改，测试与
aggregate gate 均留给总计划后续任务。
