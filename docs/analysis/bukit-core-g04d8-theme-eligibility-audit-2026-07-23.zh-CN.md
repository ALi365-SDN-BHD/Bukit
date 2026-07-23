# Bukit Core G-04D8 Theme 公共面资格审计

> 审计日期：2026-07-23
>
> 源码事实基点：`2.0@b4a60b7ebeef34eda9f53e72a10a76ebc10c8544`
>
> 对应总计划：Task 28 / G-04D8 eligibility audit
>
> 审计性质：只读资格审计；本任务不修改生产代码、测试、当前 public API baseline 或历史消费者 manifest

## 1. 范围与边界

本报告只审计 Bukit Core 中下列三个 `Bukit.Theme` 候选：

- `Bukit.Theme.SchemaValidationError`；
- `Bukit.Theme.SchemaValidationException`；
- `Bukit.Theme.ThemeDoctorCommand.DoctorResult`。

审计覆盖：

- public/protected signature 与父类型传播；
- `Bukit.Theme`、`Bukit.Rendering`、`Bukit.Engine`、`Bukit.Cli` 的 Core 调用链；
- owner tests；
- schema 错误产生顺序、strict/warn 传播和 doctor 文本输出；
- JSON/source generator/Native AOT 风险；
- 当前治理 baseline、历史候选搜索证据和后续原子任务停止条件。

`Bukit-Labs` 和外部插件不属于修复范围。本报告只读确认了一处 Labs 编译期消费者，用于防止 Core 收窄造成已知破坏；不修改 Labs 源码、测试或行为。外部插件协议、theme schema、Core CLI 命令、持久化格式和 JSON schema 均不在本任务中修改。

## 2. 执行结论

| 候选 | 当前事实 | 资格结论 | 后续最小动作 |
|---|---|---|---|
| `SchemaValidationError` | `SectionSchemaValidator.Validate(...)` 的公开返回元素类型；record 的 `Section`、`Message` 与 `ToString()` 是 warn 模式的可观察结果 | **保留 public**；从 `implementation-public / 2.0-candidate` 纠正为 `cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review` | Task 29 只更新治理分类与行为断言，不改访问级别或 shape |
| `SchemaValidationException` | strict 模式从公开 `Validate(...)` 抛出；不出现在任何 public/protected member signature 中，也不包含 `SchemaValidationError` 成员；Core 没有精确类型 catch | **可在 2.0 单独 internalize** | Task 29 仅改 `public sealed class` 为 `internal sealed class`；异常类型仍存在，首次失败时机与 `Message` 必须不变 |
| `ThemeDoctorCommand.DoctorResult` | 同时是公开 `Diagnose(...)` 的返回类型和公开 `PrintReport(...)` 的参数类型；Core CLI 并不消费该图；另有一个只读观察到的、范围外 Labs 编译期消费者 | **保留 public**；从 `implementation-public / 2.0-candidate` 纠正为 `cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review` | Task 30 不改生产可见性，只补结果/文本边界断言和治理终态 |

因此，G-04D8 不应被实现为“三类型批量 internalize”。推荐终态是：

1. 一个类型收窄：`SchemaValidationException`；
2. 两个 companion 类型保留并重分类：`SchemaValidationError`、`DoctorResult`；
3. 不扩展父类型 `SectionSchemaValidator` 或 `ThemeDoctorCommand`，不建立新的 Core CLI/JSON 输出链。

如果后续按此结论实施，Theme owner 的净变化应为：

- public type 数量 `-1`；
- `2.0-candidate` 数量 `-3`：一个被 internalize，两个转为明确 retained；
- 历史 136-entry 消费者 manifest 保持不变。

以上是 Task 29～30 的条件投影，不是本只读任务已经完成的 baseline 变化。

## 3. Validation exception graph

### 3.1 公开签名与 Core 传播

```text
Bukit.Engine.ThemeBootstrapper
  └─ creates SectionSchemaValidator
       └─ exposed by public ThemeBootstrapResult.SchemaValidator
            └─ passed into Bukit.Rendering.ScribanTemplateRenderer
                 └─ SectionRenderHelper
                      └─ public SectionSchemaValidator.Validate(...)
                           ├─ warn/off: List<SchemaValidationError>
                           └─ strict: throws SchemaValidationException(message)
```

源码证据：

- [`SectionSchemaValidator.Validate`](../../src/Bukit-Core/Bukit.Theme/SectionSchemaValidator.cs) 是公开方法，返回 `List<SchemaValidationError>`；
- [`ThemeBootstrapResult`](../../src/Bukit-Core/Bukit.Engine/ThemeBootstrapper.cs) 的公开 primary constructor/property 传播 `SectionSchemaValidator`；
- [`ScribanTemplateRenderer`](../../src/Bukit-Core/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs) 的公开构造函数接收 `SectionSchemaValidator?`；
- [`SectionRenderHelper`](../../src/Bukit-Core/Bukit.Rendering/Scriban/SectionRenderHelper.cs) 在两个 section 渲染入口调用 `Validate(...)`，但丢弃返回列表。

即使当前跨程序集调用点没有读取返回列表，CLR 元数据仍把
`SchemaValidationError` 固定在公开 method signature 中。只把该 record 改为
`internal` 会直接产生 inconsistent accessibility，不能编译；把
`Validate(...)` 改成 `void`、internal 或换 DTO 则会扩大为父 API 迁移，超出
Task 29。

### 3.2 exception member 传播判定

当前 `SchemaValidationException` 只有一个公开字符串构造函数：

```csharp
public SchemaValidationException(string message) : base(message) { }
```

它没有：

- `SchemaValidationError Error`；
- `IReadOnlyList<SchemaValidationError> Errors`；
- 任何 public/protected companion member；
- JSON、DataContract 或 source-generation 标记。

因此当前不存在“public exception 包含 internal error member”的图。两个候选不能被误当成同一访问级别原子：

- `SchemaValidationError` 受 `Validate(...)` 公开返回签名约束，必须保留；
- `SchemaValidationException` 只通过运行时 throw 传播，可以在不改变 method signature 的情况下 internalize。

Task 29 禁止为了把二者“绑成一组”而给 exception 新增 `Error`/`Errors` 属性。那会新增公共契约、改变异常序列化/反射 shape，并把原本可独立收窄的 exception 重新绑定到 retained record。

### 3.3 strict/warn/off 与错误顺序

当前行为次序由 [`SectionSchemaValidator.cs`](../../src/Bukit-Core/Bukit.Theme/SectionSchemaValidator.cs) 中的控制流决定：

| 模式/输入 | 当前可观察行为 |
|---|---|
| `Off` | 在加载 schema 前返回新的空列表 |
| schema 缺失、不可解析或没有 `Props`，且输入 props 非空 | 产生单条 `Section has props but no schema defined` |
| props 为 null/空 | 按 `schema.Props` 枚举顺序产生 required 缺失项 |
| props 非空 | 先按 `schema.Props` 顺序产生 required 缺失项，再按输入 props 枚举顺序产生 unknown/type/value 错误 |
| `Warn` | 每条错误先加入返回列表，再以 `[schema] {Section}: {Message}` 记录 logger；列表与日志顺序一致 |
| `Strict` | 第一条错误先加入方法内局部列表，随后立即抛出；调用者只观察到首条错误文本，后续项不再验证 |

顺序目前没有显式排序。`SectionSchema.Load(...)` 使用 AOT source-generated
`JsonContext` 反序列化 schema model，但依赖字典枚举顺序；输入参数又只是
`IReadOnlyDictionary`，并未承诺任意实现的稳定顺序。后续任务必须冻结“当前遍历顺序”，不得借公共面治理新增排序、聚合全部 strict 错误或改变 fail-fast。

### 3.4 Core 异常传播

Core Rendering 不按 `SchemaValidationException` 类型分支：

- JSON section 路径和 script-object section 路径的外层均 catch
  `Exception`；
- 非 `RenderException` 会转为 `theme.render_section.failed`，文本包含原异常
  `Message`；
- strict diagnostic 随后仍按现有 Rendering 规则决定是否抛出
  `RenderException`。

因此把 class 本身改为 internal 不改变 Core 的 catch 选择、错误文本或执行路径。
它会有意移除外部 CLR 消费者精确 `catch (SchemaValidationException)` 的编译能力，
这属于已经批准进入资格审计的 2.0 breaking surface；当前公开搜索没有确认的
外部匹配，但私有/未索引消费者仍未知。

### 3.5 owner test 现状与缺口

[`SectionSchemaValidatorTests.cs`](../../tests/Bukit.Theme.Tests/SectionSchemaValidatorTests.cs)
当前只有四项测试，且没有创建真实 schema 文件：

- 覆盖 `Off` 返回空；
- 一个名为 required-field 的测试实际只验证“无 schema、无 props”返回空；
- strict 测试只断言异常 CLR 类型，不断言消息和首错时机；
- 一个名为 unknown-prop 的测试实际触发“有 props 但无 schema”，没有走 unknown-prop 分支。

Task 29 应先增加真实 schema fixture，至少冻结：

1. `Off` 不加载 schema；
2. warn 返回 record 的 `Section`、`Message`、`ToString()`；
3. required-before-props 的精确顺序；
4. unknown、string/maxLength、number、boolean、url、image 的当前错误文本；
5. warn 列表与 logger 的顺序一致；
6. strict 只抛首错，异常 `Message` 精确等于首个 record 的 `ToString()`；
7. exception internalize 后通过异常对象的运行时 full name/消息验证行为，不新增
   `InternalsVisibleTo`；
8. Architecture 断言 `SchemaValidationError` 仍 exported、exception 不再
   exported，且 `Validate(...)` 返回的泛型元素仍是 retained record。

这些测试只能冻结当前行为，不得顺带修复类型校验宽松度、字典排序、schema
加载吞异常或渲染错误分类。

## 4. Doctor result graph

### 4.1 真实 owner 图

```text
public ThemeDoctorCommand
  ├─ public Diagnose(...) -> public DoctorResult
  └─ public PrintReport(public DoctorResult)

Core CLI DoctorCommand
  └─ independent config/template/plugin diagnostic pipeline
     (does not call ThemeDoctorCommand)
```

`DoctorResult` 是 containing facade 两个公开方法的直接 companion type。只把 nested
record 改为 internal 会产生 inconsistent accessibility；把 `Diagnose`、`PrintReport`
或整个 `ThemeDoctorCommand` 一并 internalize 会扩大到父类型，而父类型不是本组三个
候选之一。

只读搜索还确认
`src/Bukit-Labs/Bukit.Labs.Cli/Commands/Theme/ThemeCommand.cs` 会调用
`Diagnose(...)` 并把结果传给 `PrintReport(...)`。Labs 不属于修复范围，不能通过改
Labs 调用点来解除传播。这个已知编译期消费者本身也满足总计划“发现消费者即停止
收窄”的条件。

### 4.2 doctor 输出与顺序

`Diagnose(...)` 按固定检查阶段追加同一个 `List<string>`：

1. `theme.yaml`；
2. page templates；
3. sections；
4. components；
5. variants；
6. assets；
7. extends；
8. tokens；
9. unused components；
10. schema required fields；
11. hardcoded text。

每个阶段内部仍可能依赖 manifest dictionary、registry 或文件枚举顺序。结束时：

- `✗` 或 `✘` 前缀设置 `HasErrors`；
- `⚠` 或 `◌` 前缀设置 `HasWarnings`；
- `DoctorResult.Issues` 暴露原始可变 `List<string>`；
- record equality 对该 `List<string>` 使用列表对象的引用相等语义，而不是逐项值相等。

`PrintReport(...)` 按 `Issues` 当前顺序输出，每项前置两个空格；summary 优先级是：

```text
HasErrors -> ERRORS FOUND
else HasWarnings -> WARNINGS FOUND
else -> ALL CLEAN
```

颜色根据字符串前缀临时设置，并在每项和 summary 后 reset。公共面治理不得改变：

- 检查阶段和 issue 次序；
- glyph、空行、缩进、summary 文本；
- error-over-warning 优先级；
- `◌` 被视作 warning 的现状；
- `Issues` 的具体 `List<string>` shape 或 record equality。

这些设计可能值得独立产品审计，但都不是 Task 30 的访问级别修复授权。

### 4.3 Core CLI、exit code 与 JSON 判定

[`Bukit.Cli.Commands.DoctorCommand`](../../src/Bukit-Core/Bukit.Cli/Commands/DoctorCommand.cs)
是另一套 Core doctor 实现。它直接检查 config、theme bootstrap、模板、assets、
plugins 和 Notion，并以 `Console` 文本及整数返回值表达结果；源码中没有
`ThemeDoctorCommand` 或 `DoctorResult` 引用。

所以 Task 30 所称“CLI 输出传播”在当前 Core 中的正确判定是：

- `DoctorResult` **不传播到 Core CLI**；
- Core CLI doctor exit code 不由 `HasErrors`/`HasWarnings` 决定；
- Core CLI 没有序列化 `DoctorResult` 的 JSON 路径；
- 不应为了收窄一个 nested record 而把两套 doctor 逻辑接起来；
- 不应在 G-04 中新增 `--json`、JSON schema、exit-code 规则或输出转换。

范围外 Labs `theme doctor` 的返回码选择不在本报告中评价或修复。

### 4.4 JSON 与 Native AOT

Core 中未发现三个候选的：

- `JsonSerializer.Serialize/Deserialize` 调用；
- `[JsonSerializable]` source-generation root；
- reflection activation；
- persisted artifact 或协议 DTO 绑定。

Theme 的现行 schema AOT context 只包含 `SectionSchema`、
`SchemaPropDefinition` 和字典容器；`DoctorResult`、`SchemaValidationError` 和
exception 均不在其中。

因此：

- internalize `SchemaValidationException` 不改变当前 Native AOT 可达性；
- retained 两个 record 也不应被误分类为 serialized contract；
- 如果未来要输出 doctor JSON，必须另立版本化诊断契约任务，先定义 JSON shape、
  顺序、schema、source-generated context 和 CLI exit-code，再评估 AOT；不能在
  Task 30 直接使用 reflection serializer。

### 4.5 owner test 现状与缺口

[`ThemeDoctorCommandTests.cs`](../../tests/Bukit.Theme.Tests/ThemeDoctorCommandTests.cs)
当前五项只覆盖部分 `Diagnose(...)` substring：

- 没有 `PrintReport(...)` golden；
- 没有同时出现 error/warning 时的 summary 优先级；
- 没有 `HasWarnings` 对 `⚠`/`◌` 的精确断言；
- 没有完整 issue 顺序；
- 没有 mutability/record shape 断言；
- 没有证明 Core CLI 与 Theme doctor 图相互独立；
- 没有 JSON/source-context 的负向架构断言。

Task 30 应增加：

1. error、warning、clean 三类精确 result flag；
2. 多阶段 issue 顺序；
3. `PrintReport` 精确文本 golden，并正确恢复 `Console`；
4. error-over-warning summary 优先级；
5. Architecture 断言 `DoctorResult` 仍为 public nested record，且
   `Diagnose`/`PrintReport` 的返回/参数仍引用同一类型；
6. Core CLI `DoctorCommand` 的现有文本和返回码回归，不新增 JSON；
7. 负向断言当前 Core serializer context 不包含 `DoctorResult`。

不得把 Labs tests 加入修复范围，也不得修改 Labs 调用点来制造收窄资格。

## 5. 兼容性与停止条件

### 5.1 Task 29：允许的最小变更

允许：

- `SchemaValidationException` 的 declaration 从 `public` 改为 `internal`；
- `SchemaValidationError` 保持 public，并更新当前 baseline 为
  `cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review`；
- 增加 Theme/Architecture 行为断言；
- 当前 baseline 只反映上述真实变化。

停止并改为 retained/另立迁移任务，如果发现：

- 任何 Core 精确 catch、public/protected member 或 reflection/serializer 对
  exception 的新依赖；
- exception 必须新增 `SchemaValidationError` 成员；
- 保持行为需要修改 `Validate(...)` 返回类型、参数或 `SectionSchemaValidator`
  可见性；
- schema shape、错误文本、顺序、fail-fast 或 Rendering 诊断必须改变；
- 新的治理级消费者证据。

### 5.2 Task 30：允许的最小变更

允许：

- `DoctorResult` 保持 public；
- 当前 baseline 将其改为
  `cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review`；
- 增加 Theme、Core CLI 和 Architecture 边界测试；
- 形成明确 retained ledger。

禁止：

- internalize `DoctorResult` 后顺带扩大修改父 `ThemeDoctorCommand`；
- 修改 Labs；
- 合并 Core doctor 与 Theme doctor；
- 新增 doctor JSON、schema、source context、exit-code 语义；
- 改 issue 顺序、glyph、summary、颜色或可变列表 shape。

如果未来希望收窄 `DoctorResult`，必须另立父 facade 迁移任务，同时解决所有已知
编译期消费者，提供替代命令/输出契约，并重新做 AOT 与 CLI 兼容审计；不能沿用本
Task 28 授权。

## 6. 后续实施顺序

1. **Task 29 / G-04D8A**
   - 先写真实 schema fixture 与行为/架构断言；
   - 保留并重分类 `SchemaValidationError`；
   - 仅 internalize `SchemaValidationException`；
   - 标记 `group-verification-pending`，不改变 schema 或 Rendering。
2. **Task 30 / G-04D8B**
   - 增加 doctor result、文本输出、Core CLI 隔离和 AOT 负向断言；
   - 保留并重分类 `DoctorResult`；
   - 不修改 Labs 或父 facade；
   - 由 Task 30 按总计划执行 Group 3 唯一完整测试、AOT、aggregate 和轻量复审。
3. **Task 31**
   - 汇总 Theme 三项终态；
   - 记录一个 internalized、两个 retained；
   - 历史 136-entry manifest 不重写。

## 7. 本任务验证声明

Task 28 只进行了源码、治理清单、消费者和测试盘点：

- 未运行 `dotnet test`；
- 未运行 focused/targeted/aggregate gate；
- 未运行 Native AOT、full、release 或 whole-solution；
- 未修改生产代码、测试、baseline、schema、协议或 Labs。

所有运行时通过声明必须等待 Task 30 的 Group 3 唯一完整验证；本报告只提供后续
原子实现的资格与停止条件。
