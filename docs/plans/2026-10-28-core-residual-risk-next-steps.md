# KD-002 后续风险收口 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this plan task-by-task, within each separately authorized scope.

**Goal:** 按 KD-004 → KD-003 → SCSS 定因的顺序消除剩余风险，不重开已完成阶段或重复验证未变化代码。

**Architecture:** 每项为独立、需明确授权的 Core 修复或诊断任务，沿用当前工作树、单 writer 和已有安全／增量构建控制层。优先封闭现有 writer 边界，再处理不完整模板指纹，最后收集 SCSS 因果证据；不新增业务入口、依赖、通用框架或配置开关。

**Tech Stack:** C# / .NET 10、xUnit、现有输出路径策略、增量 manifest、Sass 子进程测试替身、OpenSpec 与 `codex-workflow.py`。

---

## 状态与授权

- 计划制定时授权仅为**制定计划**；随后用户“按计划 执行”，按交接先单独执行任务 1（KD-004）。其收口后用户另行确认任务 2（KD-003）方案 A；任务 3 与阶段 7 不自动启动。
- KD-002 已完成独立修复，原任务为 69 通过 / 0 失败 / 0 Skip；持久证据在 `/Users/ali/.local/state/bukit-verification/kd002-clean-preserve-s2CkH1/completion.json`。这是该任务的执行事实，不自动成为未来改动闭包的缓存命中。
- KD-004 已按任务 1 独立修复并完成验证／复核，Skip 已移除；KD-003 原 KnownDefect Skip 也已按任务 2 激活并修复，SCSS 历史缺 `sass.log` 的原因仍未知。阶段 6 已完成结论保持原范围，不改成未完成，也不扩展成全 Core 验收。
- 任务 2（KD-003）方案 A 已独立收口：完整六类专项 168/168、0 Skip，专项和唯一最终增量复核各 0/0/0；持久证据见任务 2。任务 1 保持已完成，任务 3／阶段 7 不自动启动。
- 2026-10-29 用户“继续执行”后，任务 3 的 SCSS 有界诊断已独立收口：唯一覆盖率专项 TRX 20/20（含 1 个本机 NOT_APPLICABLE）、规定两次复核各 0/0/0；未复现、根因仍未知。不授权生产修复、重复稳定性运行或阶段 7。
- 用户随后在“继续阶段 6”的范围澄清中明确选择 SCSS 生产观测补证，并批准新 Debug 事件的 stderr 全文遮蔽；独立任务见下，不重开已完成阶段 6 审计或改变编译行为。

## 各任务共同执行规则

1. 开工时重新读取当前状态并保存持久 baseline；保留一切既有改动，不恢复旧快照、不自动提交／推送／合并。证据放 `/Users/ali/.local/state/bukit-verification/` 下的独立任务目录，不能只留易丢失临时 TRX。
2. 为本任务创建独立 OpenSpec proposal/tasks/spec，明确修改白名单、直接消费者、公开／序列化契约消费者和确切测试命令。使用 `python3 scripts/checks/codex-workflow.py closure` 生成闭包，处理全部 unmapped，再用 `classify` 分类；本计划的预计文件清单不能替代执行时闭包。
3. 单 writer 串行实施。若批准多项连续执行，用 `queue init/acquire/transition` 管理，任务经 writing → testing → review_wait → done/blocked 后才轮到下一项；无关源码调查可以只读并行。
4. 测试先行：观察**行为 RED**，排除编译／环境失败，才修改生产代码。RED/GREEN 各用独立目录、日志、TRX 和实际退出码，禁止覆盖失败证据。
5. 通过后 `cache record`；复用前 `cache check` 必须匹配 HEAD、闭包字节、确切命令、相关环境变量设置状态和 SDK。不得保存环境变量值，也不为刷新缓存重复跑 fixture。长调用后台执行并及时报告进展。
6. 每项通过规定专项后只做一次专项复核；仅 Critical/Important 回到实施并做范围内复核，Minor 记录后继续。每个已授权父任务收口时只生成一次 `review-scope`、做一次 delta-only 最终复核，复用未失效证据；若多项获批为同一父任务，最后统一做一次，不逐项重复最终审计。
7. 回填本任务 OpenSpec 与主测试计划的对应缺陷行，记录 metrics，核对范围外字节，再释放 writer。只跑本任务明确列出的专项及 `git diff --check`；扩大文件／契约／命令范围须先更新闭包和授权。

## 任务 1：KD-004——先封闭 taxonomy redirect writer 写入边界

**执行状态：SUCCESS（仅任务 1 授权范围）。** 独立清单 `openspec/changes/fix-kd004-taxonomy-redirect-boundary/tasks.md`；持久证据 `R=/Users/ali/.local/state/bukit-verification/kd004-redirect-boundary-bh6Eqt`。原 writer 19 RED / 33 passed → 首轮 GREEN 52/52；专项发现 I01（NUL 自别名跳过最终路径校验），再观察 1 RED / 52 passed 后修补顺序，最终 `R/review-green/tests.trx` 为 **53/53、0 失败、0 Skip**。I01 范围内复核已关闭，唯一最终 delta 复核无剩余 Critical/Important/Minor，`git diff --check` 通过；未变化运行输入和范围外既有改动保持字节不变。原 RED／候选 GREEN／专项 finding 保留，不冒充最终证明。下一步停在任务 2 的策略确认点，不自动启动 SCSS／阶段 7。

**开始条件：** 用户明确授权单独修 KD-004。当前只证实内部 writer 的函数级缺陷；普通生产构造链未发现非空 Aliases 入口，不宣称已证明整站远程利用。

**预计修改：**
- `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/TaxonomyRedirectWriter.cs`
- `tests/Bukit.Engine.Tests/TaxonomyRedirectWriterTests.cs`
- `docs/plans/bukit-core-automated-test-plan-2026-10-24.zh-CN.md` 中 KD-004 状态及独立证据记录
- 新建 `openspec/changes/fix-kd004-taxonomy-redirect-boundary/{proposal.md,tasks.md,specs/taxonomy-redirect-safety/spec.md}`

**只读消费者／复用点：** `TaxonomyPlugin.AfterBuild`、`TaxonomyPageCreator`、`FileWriter.GetSafeFullPath` 及其已有 `SafePathResolver`。不修改它们，不新增 aliases 配置／元数据接线，不改公开 API、输出编码或 HTML 转义策略。

### 1.1 测试先行

- 去掉现有 `WriteRedirects_AliasTraversal_ThrowsAndWritesNothing` 的 Skip/KnownDefect 排除，保留 Defect 编号和两个原始反例。
- 扩充相对穿越、根路径、Windows 盘符／UNC 和反斜杠形式；危险路径按跨平台输入判断，不能只依赖当前 OS 的 `Path.IsPathRooted`。
- 增加**合法 alias 在前、非法 alias 在后**，以及跨 term 的同类情况；断言 writer 本次调用没有新建目录／文件、没有覆盖原文件、没有追加 `generatedPaths`。此原子性限于 writer 输入预检，不声称整个 `AfterBuild` 尚未写过 `taxonomy.json`。
- 保留正常别名、空／自别名、定制 routePrefix、重定向 URL／HTML 内容和生成路径记录。安全的既有层级别名不因修复被无故禁止。
- 增加已有符号链接指向输出根外的拒绝用例，检查外部 sentinel 字节不变；能力不足单列验证缺口，不算通过。

核心失败断言沿用既有契约：

```csharp
var failure = Record.Exception(() =>
    TaxonomyRedirectWriter.WriteRedirects(outputDir, "tags", terms, generatedPaths: generatedPaths));
Assert.Empty(Directory.EnumerateFiles(fixtureRoot, "index.html", SearchOption.AllDirectories));
Assert.Equal(DiagnosticCode.BuildOutputUnsafe, Assert.IsType<ConfigException>(failure).Code);
Assert.Empty(generatedPaths);
```

### 1.2 最小生产修改

先遍历本次全部有效 aliases，将 Trim 后输入校验并解析成最终 `index.html` 路径；拒绝根路径／盘符／穿越段，使用 `FileWriter.GetSafeFullPath(outputDir, relativeIndexPath)` 做最终包含性及符号链接校验。仅将完整安全的写入项暂存在本地集合，**全部预检通过后**才创建目录、按原 UTF-8 策略写原重定向内容和追加生成路径。

路径策略抛出的 `OutputPathSecurityException` 在此边界转换为既有 `ConfigException` / `BuildOutputUnsafe`，保留内层错误；不改共享 resolver，不以字符串清洗静默改写危险 alias，也不把转义 HTML 当成路径保护。发现必须修改上述只读消费者时先停下重新定界。

### 1.3 RED／GREEN 专项与出口

执行时设 `R` 为该任务独立持久证据目录。RED 使用同一命令，仅将结果目录换成 `$R/red`，不得覆盖 GREEN：

```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~TaxonomyRedirectWriterTests|FullyQualifiedName~TaxonomyEnsureTermsTests|FullyQualifiedName~TaxonomyPageCreatorTests" --logger "trx;LogFileName=tests.trx" --results-directory "$R/green"
git diff --check
```

**验收：** 非法输入先报既定错误且本次 writer 零写入；正常输出和生成路径不回归；KD-004 无 Skip；上述完整三类专项通过，规定复核无 Critical/Important。只声明实际执行的平台，不借用旧平台测试冒充新修复的跨平台证据。

## 任务 2：KD-003——先确认预算策略，再修增量正确性

**执行状态：SUCCESS（仅任务 2 授权方案 A 范围）。** 独立清单 `openspec/changes/fix-kd003-hash-budget-fallback/tasks.md`；持久证据 `R=/Users/ali/.local/state/bukit-verification/kd003-budget-fallback-SkfsyV`，最终收口记录 `R/completion.json`。最终正文存储 fixture 在原生产实现上为 21 项预期失败／147 通过／0 Skip（`body-red/`），修复后完整六类专项为 168 通过／0 失败／0 Skip（`body-green/`）；运行证据已记录缓存并检查 HIT。专项复核 `reviews/specialty-v1.md` 和唯一 delta-only 最终复核 `reviews/final-delta-v1.md` 均为 Critical 0／Important 0／Minor 0，无需补修。仅两个生产文件修改，不修无关 inline-body 指纹路径；最终静态、范围保护、writer 及 metrics 结果以收口记录为准。不启动 SCSS／阶段 7，也不提交／推送／合并。

**开始条件：** KD-004 完成或被明确暂缓，用户单独授权 KD-003，并确认下列策略。默认 10,000 文件／100 MiB 是内容读取预算；漏失效不是 SHA-256 碰撞，而是被跳过的内容从未进入摘要。

### 2.1 必须先作出的决定

| 方案 | 行为及代价 | 建议 |
|---|---|---|
| A 保留预算，超限禁用该 variant 本次增量复用 | 构建继续但完整重渲染；复用已有 `IncrementalEnabled=false` 分支，稍多控制层修改 | **推荐**：不静默保留旧页面，也不取消预算 |
| B 保留预算，明确失败并提示 `--no-incremental` | 改动较小，但原来成功的大布局构建会变成失败 | 用户接受兼容性变化时可选 |
| C 超限仍完整流式读取内容 | 保持摘要接口、内存可控，但放弃原内容读取限制并增加 I/O | 仅明确接受性能代价时可选 |

不采用只增大阈值、只加 mtime、随机摘要或继续把路径＋长度当完整摘要。未确认 A/B/C 前不修改生产代码。

### 2.2 推荐方案 A 的预计闭包与实施

**预计生产修改：** `src/Bukit-Core/Bukit.Engine/Incremental/HashUtil.cs`、`src/Bukit-Core/Bukit.Engine/Incremental/DirectoryHashCache.cs`（仅确有必要时）、`src/Bukit-Core/Bukit.Engine/VariantManifestPlanner.cs`。

**预计测试修改：** `tests/Bukit.Engine.Tests/HashUtilTests.cs`、`tests/Bukit.Engine.Tests/DirectoryHashCacheTests.cs`、`tests/Bukit.Engine.Tests/LibraryDiagnosticOutputTests.cs`、`tests/Bukit.Engine.Tests/RenderPipelineTests.cs`。复用现有测试类，不新增公共测试钩子或配置项。

**直接消费者／运行链：** `SiteEngine`、`I18nWorktreeBuild` 创建目录缓存，经 `VariantBuildPipeline` / `VariantManifestPlanner` 将模板指纹交给页面和列表渲染。公开／序列化 manifest 形状不变；不得把不完整摘要写成可复用的正常 manifest。预计同步主测试计划、新独立 OpenSpec 与 `guide/user/14-troubleshooting.md` 的降级说明。

1. 保留原两类同长度内容修改的复现输入及历史 RED；按批准策略增加“超限不能返回可复用完整指纹”的 RED。若采用 A/B，明确调整原 `ChangesHash` 期望，而不是假装接口仍返回完整摘要。
2. `HashUtil` 用最小内部可辨识的超限信号终止不完整指纹；`DirectoryHashCache` 不缓存它。`VariantManifestPlanner` 只处理该信号，warning 后使用已有禁用增量分支：不复用旧 entries，不把 I/O／权限错误一并吞成降级。不要引入通用结果框架。
3. 覆盖恰好预算、超过文件数、超过总大小、首个文件已超限、空／缺失目录、未超限确定性和符号链接安全；有意更新旧超限“仍返回 64 字符十六进制摘要”的测试。
4. **同步 `LibraryDiagnosticOutputTests` 的 HashFileLimit/HashSizeLimit 场景。** 它直接调用 helper 并固定旧警告字节，不能遗漏，也不能顺手放宽其余日志断言。
5. 在既有 `RenderPipelineTests` 构造真实 planner→渲染对照：用小目录／缩小预算，先构建，再同长度修改模板，验证降级后的实际输出字节与相同最终输入的非增量渲染一致；再验证普通未超限输入仍能复用。不创建 10,001 文件的大 fixture，不只比较两个字符串哈希或使用占位 renderer 代替实际产物。
6. 小预算通过内部现成 `DirectoryHashCache` 参数传入；若现有内部构造链无法完成证明，先补充闭包／方案，不为测试擅改公开 `SiteEngine` API。

### 2.3 拟议专项与出口（仅方案 A）

```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~HashUtilTests|FullyQualifiedName~DirectoryHashCacheTests|FullyQualifiedName~LibraryDiagnosticOutputTests|FullyQualifiedName~RenderPipelineTests|FullyQualifiedName~IncrementalBuildEngineTests|FullyQualifiedName~IncrementalBuildEngineAsyncTests" --logger "trx;LogFileName=tests.trx" --results-directory "$R/body-green"
git diff --check
```

**验收：** 两种预算超限都不再复用旧模板结果；真实最终产物与非增量对照一致；普通增量行为保留；KD-003 不再 Skip，完整上述专项通过且规定复核无阻塞。若选 B/C，先重写此段契约、测试期望和闭包再执行，不机械套用 A 的计划。

## 任务 3：SCSS——独立、有界地补因果证据

**执行状态：有界诊断已收口；定因仍为 PARTIAL（根因未知）。** 独立清单 `openspec/changes/diagnose-scss-missing-log/tasks.md`；证据根 `R=/Users/ali/.local/state/bukit-verification/scss-causal-diagnosis-z6Jmp9`，最终机械收口记录 `R/completion.json`。只改原集成用例及记录，增加探测／编译分支 trace 和失败保存自检；不改 `ExternalToolProcessRunnerTests` 或生产代码。唯一带覆盖率专项 `R/diagnostic/tests.trx` 为 20 通过／0 失败／0 Skip、实际退出码 0；其中 1 个 Windows 专用用例在本机记录 NOT_APPLICABLE，不是 Windows 执行证明。两路 trace 各一次、compile-only 日志一次、源内容及三语言 CSS 原断言和失败留存自检均通过，构建及断言总耗时约 704 ms；`R/captures/` 与覆盖率附件已持久留存，运行缓存记录并检查 HIT。专项复核 `R/reviews/specialty-v1.md` 与唯一最终增量复核 `R/reviews/final-delta-v1.md` 各为 Critical 0／Important 0／Minor 0，无需补修或重跑。本次未复现历史缺日志，不能标已修复。当前接口不暴露成功探测／编译的独立进程退出码、stderr、耗时或调度延迟，这些观测仍为缺证，不能凭历史约 3 秒耗时认定超时；进一步生产观测补证或修复须另行授权。

**开始条件：** 用户单独授权诊断。目标是定位缺日志发生在探测、编译、工具选择、日志路径还是进程调度／执行阶段，不承诺本轮必能复现或修好。

**预计只改测试：** `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs` 的 `BuildAsync_ScssEnabled_PreservesSourceAndCompilesOnceAcrossLanguages`；如需验证诊断采集，再修改 `tests/Bukit.Engine.Tests/ExternalToolProcessRunnerTests.cs`。计划记录另行回填，不重开阶段 6。只读 `src/Bukit-Core/Bukit.Engine.Assets/ScssCompiler.cs`、`src/Bukit-Core/Bukit.Engine.Assets/ExternalToolProcessRunner.cs` 和资产工作区调用链。

1. 给假 Sass 的 `--version` 和编译分支各留独立 trace，保留原 `sass.log` 仅记 compile 的断言；失败时在清理 fixture **之前**把 trace、debug/warning、预期／实际 CSS 状态及可取到的退出码／耗时复制到持久证据目录。
2. 只记录工具身份的脱敏标记和环境变量设置状态，不落原始 PATH、环境值、密钥或无关内容。测试自身确认 trace 留存机制有效，不能只在成功时才保存证据。
3. 在原覆盖率设置下执行一次下列有界专项；不自动运行完整 Engine，也不连续二十次刷 GREEN。探测与编译的结果、stderr 或耗时若现有测试无法观测，准确标为缺证；需要生产观测改动时另行定界并取得授权，不能猜根因。
4. 保持源 SCSS 字节不变、compile 仅一次、三个语言 CSS 字节正确的原断言。不得先增大 3 秒／5 秒超时、加自动重试、放宽断言或把缺 CSS 的 warning 偷改成另一套产品契约。

```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --settings coverage.runsettings --collect "XPlat Code Coverage" --filter "FullyQualifiedName~ExternalToolProcessRunnerTests|FullyQualifiedName~SiteEngineIntegrationTests.BuildAsync_ScssEnabled_PreservesSourceAndCompilesOnceAcrossLanguages" --logger "trx;LogFileName=tests.trx" --results-directory "$R/diagnostic"
git diff --check
```

**出口：** 能定位则提出单独的最小修复与对应 RED；未复现或缺关键观测则明确“根因仍未知／待授权补证”，不标已修复。该 Unix 假工具用例不能冒充 Windows 证明。旧临时原始附件不可读的事实保留，不为恢复旧证据重复跑全量。

## 独立追加任务：SCSS 生产进程观测补证（2026-10-29）

**执行状态：SUCCESS（仅生产观测补证），已收口；历史根因仍未知。** 用户已明确选定补证及全文遮蔽策略。独立计划 `docs/plans/2026-10-29-scss-process-observability.md`；清单 `openspec/changes/observe-scss-process-attempts/tasks.md`；证据 `R=/Users/ali/.local/state/bukit-verification/scss-process-observation-Ycjxhu`，最终机械收口 `R/completion.json`。仅 `ScssCompiler` 新增一个私有观测入口，复用原 runner，记录关联 attempt、固定阶段／候选、实际返回退出码、调用边界耗时、stderr 状态及字符数；不输出正文、参数、环境值、路径或异常消息。抛错无返回结果时明确 unavailable，新诊断接收器异常不替换工具结果或取消。

RED 为 10 项缺事件预期失败／93 通过／0 Skip，原生产字节及旧行为控制已确认；GREEN 为 **104 通过／0 失败／0 Skip**，含 1 个 Windows-only NOT_APPLICABLE，不是 Windows 执行证明。新增十个受控场景包含日志接收器故障控制；真实多语言构建记录 probe／compile 退出码均为 0，调用耗时 163／26 ms，总构建及断言 521 ms，源及三语言 CSS 原断言通过。运行 GREEN 已缓存并检查 HIT；专项复核 `R/reviews/specialty-v1.md` 和唯一最终增量复核 `R/reviews/final-delta-v1.md` 各为 Critical 0／Important 0／Minor 0，无需补修或重跑。

3 秒／5 秒超时、工具顺序、编译／CSS 检查、原 warning／Info、取消和 runner／清理逻辑不改。新遮蔽仅保证新 Debug 事件，原编译失败 warning 的 stderr detail 不在本批改动内。受控超时不是历史偶发复现；原始 stderr 有意不可见，抛错时进程结果、物理 PATH 解析及 OS 调度仍不可观测。只跑计划内三类／命名用例专项，不启动全 Engine、稳定性循环、阶段 7 或发布门禁。

## 后续里程碑与不做事项

- 三项按各自出口处置后，再规划阶段 7 集成／稳定性；二十次稳定性执行须取得单独授权、固定输入及命令，不能从本计划自动启动。
- KD-002 及新修复的 Linux／Windows 补证、CI 接线另行授权并安排 owner 自检，不混入当前业务修复，不声称本机通过等于跨平台通过。
- 不运行 `scripts/test-all.sh`、`scripts/smoke-all.sh`、全解决方案、full/release 或未命名 gate；不修改历史参考目录，不新增依赖，不自动创建新主工作树或提交。
- **当前交接：** 任务 1、任务 2、任务 3 的有界诊断及独立 SCSS 生产观测补证均按各自授权收口；当前停在历史根因仍未知的边界。进一步调查／修复、重复稳定性运行及阶段 7 仍须单独定界授权，不自动启动。另开执行会话时须先核对同一工作树与 writer 状态，不并发写。

计划制定阶段仅做文档静态核对；任务 1、任务 2、任务 3 后续执行证明以各自独立证据为准。任务 3 只执行一次计划内带覆盖率专项，未复现并不消除历史问题。剩余风险：KD-003 超限完整重渲染会增加耗时；SCSS 尚未定因，新增诊断仅改善下一次失败的证据留存；当前本机相应专项不代表跨平台／整站安全验收。计划批准与具体 Core 实施授权必须区分。
