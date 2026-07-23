# Bukit Core G-04D8A Theme validation graph 受控收窄决议

> 日期：2026-07-23
>
> 范围：G3 Task 29；只处理 Bukit Core Theme validation graph
>
> 基线：`4fa2c476c146a0e11f1cc30a8e2660eb841fd6e5`
>
> 状态：implemented / group-verification-pending

## 1. 决议

G-04D8A 只把：

```text
Bukit.Theme.SchemaValidationException
```

从 public sealed class 收窄为 internal sealed class。唯一 production
accessibility 变化是：

```diff
- public sealed class SchemaValidationException : Exception
+ internal sealed class SchemaValidationException : Exception
```

exception 类型没有删除，仍继承 `Exception`，仍保留 public string constructor。
strict 模式仍在第一条 schema validation error 产生后执行：

```csharp
throw new SchemaValidationException(error.ToString());
```

`SchemaValidationError` 不收窄。它继续是 public sealed positional record，
继续暴露 `Section`、`Message`、deconstruction、record equality 和
`ToString() => $"{Section}: {Message}"`。当前 baseline 将其从误导性的
`implementation-public / 2.0-candidate` 纠正为：

```text
cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review
```

原因是 public `SectionSchemaValidator.Validate(...)` 直接返回
`List<SchemaValidationError>`。即使当前 Rendering 调用点丢弃返回值，该 record
仍是 public method metadata 的必要 companion 类型。

## 2. 原子传播图

```text
ThemeBootstrapper
  -> new SectionSchemaValidator(...)
  -> public ThemeBootstrapResult.SchemaValidator
  -> public ScribanTemplateRenderer constructor
  -> SectionRenderHelper
       -> public Validate(...)
          ├─ Off: empty List<SchemaValidationError>
          ├─ Warn: ordered List<SchemaValidationError> + logger warnings
          └─ Strict: first error -> internal SchemaValidationException(message)
```

G-04D8A 没有修改传播图中的任何 parent type 或 method：

- `ValidationMode` 保持 public enum；
- `SectionSchemaValidator` 保持 public sealed；
- constructor 参数/default 保持不变；
- `Validate(...)` 的参数和返回类型保持不变；
- `ThemeBootstrapResult` 与 public Rendering constructor 保持不变；
- Rendering 仍按 generic `Exception` 捕获并读取 `Message`，没有新增精确
  exception type 分支。

因此本任务没有产生 public method 返回 internal type 的破碎图。retained
`SchemaValidationError` 继续满足公开返回签名；internal exception 不出现在任何
public/protected signature 中。

## 3. 行为冻结

`SectionSchemaValidatorTests` 改为每项使用独立临时 theme root，并写入真实
`schema.json`。fixture 包含按声明顺序排列的：

- 两个 required string props；
- 有 `MaxLength` 的 string；
- number；
- boolean；
- url；
- image。

### 3.1 warn 模式

组合 fixture 固定当前遍历顺序：

1. 按 schema dictionary 顺序产生 required 缺失；
2. 按输入 props dictionary 顺序产生 unknown；
3. 继续产生 string maxLength、number、boolean、url、image 错误。

测试对每个 `SchemaValidationError` 的 `Section`、`Message` 和 record equality
做精确断言，并证明 logger 按相同顺序输出：

```text
[schema] hero: <message>
```

### 3.2 strict 模式

strict fixture 不再从外部测试程序集直接引用候选 exception CLR type。它从 public
validator 入口取得异常，并固定：

- runtime full name 仍为
  `Bukit.Theme.SchemaValidationException`；
- 第一条 required error 立即 fail-fast；
- `Message` 精确等于该 error 的 `ToString()`；
- `InnerException` 仍为 null；
- strict 分支在 throw 前不会进入 warn logger 分支。

这种测试方式不要求给 `Bukit.Theme.Tests` 增加
`InternalsVisibleTo`。

### 3.3 其他保留行为

owner tests 还固定：

- Off 模式返回空；
- null props 只按 schema 顺序返回 required errors；
- 无 schema 但 props 非空时保持单条
  `Section has props but no schema defined`；
- string/number/boolean/url/image 的有效值不产生错误或 warning。

没有改变或扩展 schema validation 规则。尤其没有修复：

- `SectionSchema.Load` 对缺失/解析异常返回 null 的现状；
- 字典排序；
- URL/image 非 string 的当前宽松行为；
- unknown schema type 的当前行为；
- strict 聚合多条错误；
- Rendering 的 error code 或 exception wrapping。

## 4. Architecture guard

新增：

```text
tests/Bukit.Architecture.Tests/G04D8AThemeValidationGraphTests.cs
```

专项 guard 固定：

1. `SchemaValidationError` 仍 public、sealed、exported；
2. record 的 string/string constructor、`Section`、`Message` 和 public
   `ToString()` shape 保持；
3. `SchemaValidationException` exact full name 仍存在，但为 internal、sealed，
   base type 仍为 `Exception`，且不再 exported；
4. exception 的 public string constructor 仍存在；
5. public `Validate(...)` 仍返回
   `List<SchemaValidationError>`，三个参数类型和 optional 状态不变；
6. `Bukit.Theme` friend assembly 集合仍为空；
7. Theme source-generated `JsonContext` 仍 root `SectionSchema` 和
   `SchemaPropDefinition`，不 root validation result 或 exception；
8. current baseline 为 `14 assemblies / 484 public types / 57 candidates`；
9. current baseline 保留并重分类 error，移除 exported exception entry；
10. closed manifest 仍为 136 项，保留两个 historical candidate，Git blob
    仍为 `7b07d6890562387010b52301e9f8716e9bf10ed1`。

Architecture 通过 public `SectionSchemaValidator` 定位 Theme assembly，再按 exact full
name 读取 internal exception；不需要新增 friend。

## 5. Public API baseline 与治理同步

修改前阶段值：

```text
14 assemblies / 485 public types / 59 candidates
```

修改后阶段值：

```text
14 assemblies / 484 public types / 57 candidates
```

两个 candidate 的去向不同：

| 类型 | public type delta | candidate delta | 终态 |
|---|---:|---:|---|
| `SchemaValidationException` | `-1` | `-1` | internal implementation exception |
| `SchemaValidationError` | `0` | `-1` | retained cross-assembly companion |

同步范围：

- current public API baseline；
- `guide/dev/public-api-governance.md`；
- `docs/governance/bukit-core-2.0-consumer-declaration.md`；
- 全部现行 G-04 Architecture current-count guards；
- 本 Task 29 resolution ledger。

`docs/governance/bukit-core-2.0-public-surface-candidates.v1.json` 保持不可变：

```text
declarationState = closed
candidateCount = 136
candidates.length = 136
SchemaValidationError historical entries = 1
SchemaValidationException historical entries = 1
Git blob = 7b07d6890562387010b52301e9f8716e9bf10ed1
```

Task 28 eligibility 报告和此前各阶段 ledger 中的历史数字没有重写。

## 6. 兼容性

这是明确的 2.0 source、binary 与 reflection breaking change：

- 外部源码不能再写
  `catch (SchemaValidationException)`；
- 以 exported type metadata 查找该 exception 的消费者不再找到它；
- 直接引用该 public CLR identity 的旧二进制需要迁移或重编译。

当前 authenticated public search 没有确认外部匹配，但 private、未索引或未声明
consumer 仍为 `unknown-until-voluntary-declaration`。本任务不把阴性公开搜索写成
“不存在消费者”。

运行时 strict behavior 不变：

- exception 类型仍存在；
- full name、base type 和 string constructor 保持；
- throw 位置、首错时机和 message 保持；
- Core Rendering 只依赖 generic exception 与 message。

`SchemaValidationError` 没有 breaking shape 变化，只改变治理分类。

## 7. JSON 与 Native AOT

三个现行 Theme schema serializer roots保持：

- `SectionSchema`；
- `SchemaPropDefinition`；
- `Dictionary<string, SchemaPropDefinition>`。

G-04D8A 没有：

- 给 error 或 exception 增加 `[JsonSerializable]`；
- 新增 reflection serializer；
- 修改 `SectionSchema` JSON shape；
- 修改 Theme YAML static context；
- 修改 trimmer/AOT 配置。

validation error/exception 不是 JSON、persisted artifact 或 plugin protocol
contract。本任务只改变 exception 的 CLR export visibility。

## 8. 明确未修改

- `SchemaValidationError` declaration、record shape、文本；
- `SectionSchemaValidator`、`ValidationMode`；
- `Validate(...)` signature；
- error 顺序、strict fail-fast、logger 格式；
- schema 文件 shape 或 `SectionSchema.Load`；
- Rendering、Engine、Core CLI；
- Theme doctor；
- Labs、外部插件；
- config、report、protocol、asset URL、path/security policy；
- JSON/source-generation roots；
- `InternalsVisibleTo`；
- 历史 136-entry manifest。

## 9. 验证状态

按 master plan，Task 29 不运行单任务 tests、focused gate、aggregate、Native AOT 或
review。本任务只完成实现、fixtures、Architecture guard 和静态治理投影：

| 检查 | 状态 |
|---|---|
| production diff | exception accessibility 单 token |
| Error public companion graph | 保留 |
| current baseline JSON | 静态解析为 `14/484/57` |
| historical manifest/blob | 静态保持 `136/136` 和原 blob |
| IVT / JSON roots | 未修改 |
| tests / aggregate / AOT / review | **未运行；Task 30 pending** |

所有运行时通过声明必须等待 Task 30 的 Group 3 唯一完整验证。

## 10. Task 30 待验证集合

G-04D8A 必须进入 Group 3 统一验证：

- `Bukit.Theme.Tests`；
- `Bukit.Rendering.Tests`；
- `Bukit.Engine.Tests`；
- `Bukit.Cli.Tests`；
- `Bukit.Architecture.Tests`；
- public API drift；
- G3 唯一 aggregate targeted gate；
- real Native AOT package/smoke；
- 一次独立轻量只读复审。

若统一验证发现以下任一情况，必须停止 G-04D8A 关闭，不得扩大修复：

- public validator signature 不再返回 retained error；
- strict 首错、异常文本、warn 顺序或 logger 文本漂移；
- Rendering error code/exception 路径改变；
- 需要新增 exception companion member、IVT 或 serializer root；
- theme schema、Labs、插件或其他非 Core scope 被修改；
- public API baseline 不等于真实程序集；
- historical manifest/blob 改变。
