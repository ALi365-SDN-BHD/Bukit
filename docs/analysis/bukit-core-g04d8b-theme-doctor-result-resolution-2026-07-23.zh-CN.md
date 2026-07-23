# Bukit Core G-04D8B Theme doctor result 保留与重分类决议

> 日期：2026-07-23
>
> 范围：G3 Task 30；只处理 Bukit Core Theme doctor result 公共面终态与
> Group 3 验证准备
>
> 基线：`2c2049b9b7fe156e924982ccb8bedd068ff65069`
>
> 状态：implementation-complete / group-verification-pending

## 1. 决议

G-04D8B 不收窄：

```text
Bukit.Theme.ThemeDoctorCommand.DoctorResult
```

该类型继续是 public nested sealed positional record。当前 public API baseline
只把它从：

```text
implementation-public / 2.0-candidate / 2.0-review
```

纠正为：

```text
cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review
```

`owner = Theme runtime`、CLR full name、signature、public members 和 protected
members 全部保持不变。

保留原因不是“尚未找到外部消费者”，而是当前 public facade 本身已经形成必要的
companion graph：

```text
public ThemeDoctorCommand
  ├─ public Diagnose(...) -> public DoctorResult
  └─ public PrintReport(public DoctorResult)
```

单独把 `DoctorResult` 改成 internal 会造成 public method 暴露低可见性类型，
无法编译。若同时收窄父 facade，则会扩大为另一个未经批准的公共面迁移任务。

## 2. 生产源码边界

本任务没有修改任何 production source：

- `ThemeDoctorCommand` 与 `DoctorResult` declaration 不变；
- Core CLI `DoctorCommand` 不变；
- Theme JSON source-generation contexts 不变；
- Native AOT/trimmer 配置不变；
- Labs 与外部插件不变。

范围外 Labs 中存在一个只读确认的直接 CLR 调用图：

```text
Bukit.Labs.Cli ThemeCommand
  -> ThemeDoctorCommand.Diagnose(...)
  -> ThemeDoctorCommand.PrintReport(result)
```

该证据支持 retained 决议，但不授权修改 Labs。公开 GitHub 搜索没有确认外部
full-name consumer；private、未索引和未声明消费者继续是
`unknown-until-voluntary-declaration`，不能从阴性搜索推导为“不存在”。

## 3. DoctorResult 行为与 shape 冻结

owner tests 固定现行结果契约：

1. clean theme 返回 `HasErrors = false`、`HasWarnings = false`；
2. `✗` issue 设置 error flag；
3. `⚠` issue 设置 warning flag；
4. `◌` issue同样设置 warning flag；
5. 多阶段检查继续按以下阶段顺序追加 issue：
   theme.yaml、page templates、sections、components、assets、extends、tokens、
   unused components；
6. `Issues` 继续直接暴露构造时传入的可变 `List<string>`；
7. record clone 继续共享同一 list reference；
8. 两个内容相同但 list instance 不同的 result 继续不相等。

本任务没有把这些当前行为改造成新的不可变 DTO，也没有改变：

- constructor 的 `bool / bool / List<string>` shape；
- issue glyph、文本或排序；
- dictionary/file iteration 规则；
- error/warning 判定；
- record equality；
- `List<string>` mutability。

这些设计是否适合未来 2.0 API，可以在父 facade 迁移任务中重新评估；不能在本
retained 重分类任务内顺带整改。

## 4. PrintReport 文本边界

Theme owner golden 固定：

- report 前置空行；
- `═══ Theme Doctor Report ═══` 标题；
- 标题后的空行；
- 每条 issue 前两个空格；
- issue 保持输入顺序；
- summary 前空行；
- `HasErrors` 和 `HasWarnings` 同时为 true 时，优先输出
  `Summary: ERRORS FOUND`；
- 最终换行保持。

测试用 `try/finally` 恢复 `Console.Out`，并放入禁用并行的专用 xUnit console
collection，避免全局 Console 被并发测试污染。

颜色 setter/reset 行为没有改动。Console color 本身不进入重定向文本 golden，
本任务也没有新增 terminal abstraction。

## 5. Core CLI 隔离

Core CLI 的 `Bukit.Cli.Commands.DoctorCommand` 是另一套独立流程：

```text
public RunAsync(CliBoundCommand) -> Task<int>
  -> config/theme bootstrap/template/assets/plugin/notion/route checks
  -> Console text
  -> exit code 0 or 1
```

它不调用 `ThemeDoctorCommand`，不接收或返回 `DoctorResult`。CLI 项目没有直接
引用 `Bukit.Theme.csproj`，现有 Theme 能力通过 Engine 边界使用。

CLI owner tests在既有 success 和 invalid-theme 两条路径上增加负向断言：

- success 仍为 exit code `0` 且包含 `Doctor passed`；
- invalid theme 仍为 exit code `1` 且包含 `Theme manifest invalid`；
- 两条路径均不出现 Theme doctor report header；
- 两条路径均不输出 `HasErrors`/`HasWarnings` JSON 字段。

本任务没有新增 `--json`、诊断 JSON schema、输出 adapter 或 exit-code 映射，
也没有连接两套 doctor。

## 6. JSON 与 Native AOT

Architecture guard 枚举 Bukit.Theme 中两个现行
`JsonSerializerContext`，固定六个 source-generation roots：

1. `Dictionary<string, SchemaPropDefinition>`；
2. `SchemaPropDefinition`；
3. `SectionSchema`；
4. `ThemeCatalog`；
5. `ThemeCatalogComponentEntry`；
6. `ThemeCatalogSectionEntry`。

`DoctorResult` 不在 attribute roots 或生成的 `JsonTypeInfo<T>` properties 中。
本任务没有：

- 给 `DoctorResult` 增加 `[JsonSerializable]`；
- 使用 reflection serializer；
- 把 Theme doctor 暴露为 persisted JSON contract；
- 增加 `DynamicDependency` 或 artificial AOT root；
- 修改 CLI publish/AOT 设置。

标准 Core package smoke 通过 config、build 和 publish audit 证明现有 CLI/Theme
构建链；它不会直接执行 `ThemeDoctorCommand`。因此 Group 3 AOT 结果只能证明
现有可达 CLI/build graph 未回归，不能虚称 Native AOT 已直接覆盖 retained
`DoctorResult`。其 CLR shape 由 Theme 与 Architecture tests 验证。

## 7. Architecture guard

新增：

```text
tests/Bukit.Architecture.Tests/G04D8BThemeDoctorResultTests.cs
```

专项 guard 固定：

1. `DoctorResult` 仍 nested public、sealed、exported，并实现精确
   `IEquatable<DoctorResult>` record graph；
2. 唯一 public constructor 参数仍为
   `bool / bool / List<string>`；
3. `HasErrors`、`HasWarnings`、`Issues` 名称、类型和 public init setter shape
   保持；
4. `Diagnose` 返回 exact `DoctorResult`，三个参数类型/default 状态保持；
5. `PrintReport` 的唯一参数仍是 exact `DoctorResult`；
6. Core CLI source 和 project 不建立 Theme doctor direct dependency；
7. Core CLI `RunAsync(CliBoundCommand) -> Task<int>` 保持；
8. Theme JSON contexts 不 root `DoctorResult`；
9. current baseline 为 `14 assemblies / 484 public types / 56 candidates`；
10. current baseline 保留并重分类 DoctorResult；
11. 两份活动治理文档记录最终 Group 3 baseline；
12. closed historical manifest 仍为 136 项，保留 DoctorResult historical
    candidate，Git blob 保持
    `7b07d6890562387010b52301e9f8716e9bf10ed1`。

全部现行 G-04 Architecture current-count guards同步到 `484/56`。历史
136-entry manifest、eligibility 报告中的条件投影和已完成任务的决策事实不重写。

## 8. Public API baseline 与治理同步

Task 30 入场阶段值：

```text
14 assemblies / 484 public types / 57 candidates
```

Task 30 终值：

```text
14 assemblies / 484 public types / 56 candidates
```

净变化公式：

```text
public types: 0
2.0-candidate: -1
```

这不是 public type removal。`DoctorResult` 仍在 baseline 中，只把已证明必须保留
的 companion type 从候选集合移出。

同步范围：

- current public API baseline；
- `guide/dev/public-api-governance.md`；
- `docs/governance/bukit-core-2.0-consumer-declaration.md`；
- 全部现行 G-04 Architecture current-count guards；
- 本 Task 30 resolution ledger。

closed historical manifest 保持：

```text
declarationState = closed
candidateCount = 136
candidates.length = 136
DoctorResult historical entries = 1
Git blob = 7b07d6890562387010b52301e9f8716e9bf10ed1
```

## 9. 明确未修改

- `src/Bukit-Core/Bukit.Theme/ThemeDoctorCommand.cs`；
- `src/Bukit-Core/Bukit.Cli/` 生产源码和 project；
- Theme JSON contexts、schema、catalog format；
- Native AOT/trimmer/publish scripts；
- Theme manifest、`theme.yaml` 要求；
- config schema、plugin protocol、asset URL、path/security policy；
- persisted report 或 diagnostic schema；
- `InternalsVisibleTo`；
- Labs、外部插件；
- 历史 136-entry candidate manifest。

## 10. Group 3 验证状态

按 master plan，Task 30 的实现提交不单独运行 tests、focused gate、aggregate、
Native AOT 或 review。本提交只完成 behavior guards、governance terminal state
和 Group 3 验证输入：

| 检查 | 当前状态 |
|---|---|
| production diff | 无 production source 变化 |
| DoctorResult public facade graph | 保留并建立 guard |
| Theme/CLI behavior tests | 已补，未运行 |
| current baseline JSON | 待提交前静态确认为 `14/484/56` |
| historical manifest/blob | 待提交前静态确认为 `136/136` 与原 blob |
| Rendering/Routing/Theme/CLI/Engine/Architecture tests | **未运行** |
| public API drift | **未运行** |
| Group 3 aggregate targeted gate | **未运行** |
| real Native AOT package/smoke | **未运行** |
| independent light review | **未运行** |

运行结果必须由主任务完成 Group 3 唯一完整验证后回填关闭证据；在此之前不得把
`group-verification-pending` 写成通过。

## 11. Group 3 关闭清单

主任务应从 Group 3 `GROUP_BASE` 对完整 diff 执行一次：

1. Rendering、Routing、Theme、CLI、Engine、Architecture 六个项目测试；
2. `bash scripts/checks/public-api-drift.sh check Release`；
3. 只执行一次
   `post-change-targeted.sh --base "$GROUP_BASE" -- <all group changed paths>`；
4. `git diff --check "$GROUP_BASE"..HEAD`；
5. 当前 host RID 的真实 Native AOT package 和 release/core smoke；
6. published CLI 对临时有效/无效站点的 doctor exit/text 检查；
7. 一次独立只读轻量复审。

关闭时必须确认：

- current baseline 精确为 `14/484/56`；
- historical manifest blob 未改变；
- DoctorResult 仍 public，未增加 JSON/AOT root；
- Core CLI doctor 未接入 Theme doctor；
- 0 schema、protocol、config、asset URL、path/security 或 persisted-format
  漂移；
- 环境阻塞与真实回归分开记录。
