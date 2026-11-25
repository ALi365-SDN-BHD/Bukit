# Bukit Core 性能优化策略：技术栈评估与不重写前提下的优化清单

- 状态：**草稿**（2026-11-24）。分析与提案，未获实施批准。本文件不授权任何 Core 改动、commit、push 或发布。
- 用途：作为后续在 Claude Code 中逐项执行的任务来源。执行规则见第 7 节。
- 分析基线：分支 `fix/i18n-release-blockers`，提交 `bb8bc44b`，版本 2.2.8。
- 分析方式：只读。阅读了项目结构、全部 csproj、插件层、渲染调度、发布脚本和 `docs/plans/2026-11-10-build-time-incremental-remediation-plan.zh-CN.md`；没有逐行读完全部源码。
- 相关变更草稿：`openspec/changes/tune-native-aot-size-and-gc/`、`openspec/changes/source-generate-build-path-regex/`。

## 0. 结论

1. **不换技术栈，不重写，不重新设计架构。** .NET 10 Native AOT 已经是合适的一档；现有瓶颈在算法和配置，不在语言。
2. **做定点优化**，共 12 项，按投入与风险分四层（第 4 节）。每项都先测量再修改，数据不支持就放弃。
3. **DeepSeek Harness 的插件机制只借鉴思想**，不能照搬（第 6 节）。
4. **pi 可以接入，但放在 Core 之外**（第 6 节）。

本文所有性能幅度都是估算，除非明确标注"实测"。

## 1. 现状事实

### 1.1 技术栈

- .NET 10（SDK 10.0.100），C#，`TreatWarningsAsErrors`，Core 项目全部 `IsAotCompatible`。
- `Bukit.Cli` 以 Native AOT 单文件发布，`InvariantGlobalization=true`。
- 依赖：Scriban 7.2.5（模板）、Markdig 0.38.0、YamlDotNet 16.3.0（含静态生成器）、AngleSharp 1.5.2、SkiaSharp 4.153.1、TiffLibrary 0.6.65。

### 1.2 代码体量（`.cs` 行数，不含 obj/bin）

| 范围 | 行数 | 说明 |
|---|---|---|
| Core（19 个项目） | 约 117,000 | Engine 39,440；Cli 21,100；Content 11,689；Config 7,854；Audit 5,168；Theme 5,062；Rendering 4,532；PluginHost 3,901；Notion 3,838 |
| 插件（9 个项目） | 约 21,400 | Importing 11,236；WechatSyncing 6,741 |
| Labs | 约 11,400 | |
| 测试 | 约 212,000 | 与产品代码约 1.4 : 1 |

### 1.3 性能基线

来自 11-10 计划文档的**实测**，对象是 4651 页的真实站点：

- 全量渲染 `renderDispatch` 约 11 秒。
- 报告阶段从 293 秒降到约 15 秒。
- 峰值内存从 2.72 GB 降到 651 MB。
- 内容无改动的构建渲染 0 页；改一篇文章渲染 812 页。

本机产物（`artifacts/publish/osx-arm64/`）：`bukit` 29.9 MB，`libSkiaSharp.dylib` 15.9 MB，合计约 46 MB。

未测量：冷启动时间、小站点内存、开发服务器空闲内存。

### 1.4 两套插件机制

- **进程内**：`IBukitPlugin` 及 `IDerivePagesPlugin`、`IAfterBuildPlugin` 等接口；11 个内置插件在 `BuiltInPluginSource` 中硬编码注册（`Bukit.Engine/Plugins/`，约 11,500 行）。
- **进程外**：命令插件，JSON over stdio，带清单、SHA-256 校验、权限模型、进程树限制、密钥遮蔽（`Bukit.PluginHost`、`Bukit.Plugin.Abstractions`）。每条协议消息启动一次插件进程；加载一个插件需要握手和取清单各一次。

## 2. 技术栈评估

### 2.1 若从零重写会选什么

Rust。理由：无 GC、流式处理是生态默认做法、编译期排除数据竞争、静态单文件。对应选型：pulldown-cmark、minijinja、lol_html、rayon、tokio + reqwest、image、blake3、axum + notify。

### 2.2 Rust 相对现状的预期（估算）

| 指标 | 现状 | Rust 预期 | 用户能否感知 |
|---|---|---|---|
| 冷启动 | 十几到几十毫秒 | 几毫秒 | 否 |
| 体积 | 约 46 MB（含 Skia） | 8–15 MB | 略有 |
| 渲染 CPU | 4651 页 11 秒 | 快 2–5 倍 | 大站点能 |
| 峰值内存 | 651 MB | 150–300 MB | CI 小机器上能 |
| 端到端构建 | 基线 | 快 1.3–2 倍 | 有限 |

端到端提升有限，因为冷构建的大头是 Notion 网络拉取和图片处理，与语言无关。

### 2.3 为什么不重写

- 需要重写约 11.7 万行，并且 21.2 万行测试绑定在 C# API 上，无法移植。
- Scriban 模板语义是现有主题的契约，Rust 没有 Scriban 实现，所有主题都要迁移。
- Markdig 与其他 Markdown 库的 HTML 输出有差异，所有页面哈希和 SEO diff 基线都会失效。
- 事务化构建、崩溃恢复、安全文件打开、SSRF 防护、进程树限制、多语言 worktree 构建等逻辑会被重新实现一遍，已修过的缺陷会重新出现。
- 第 4 节的优化做完后，与 Rust 的剩余差距估计在 1.5–2 倍，集中在内存和体积。

**结论：重写的收益不足以覆盖代价。** 只有当目标是把峰值内存和大站点渲染时间再压一个量级时才值得重新评估，而且应先做渲染热路径的原型取得真实数据。

## 3. 对先前分析的更正

以下两点在分析过程中曾被说错，以本节为准：

1. **Core 已经广泛使用 `[GeneratedRegex]`（82 处）。** 尚未转换的构建路径静态模式只有 14 个，见第 4 节 M3。
2. **正式发布包已经剥离符号。** `scripts/build/package-native-aot.sh` 传入 `-p:BukitStripSymbols=true` 和 `-p:NativeDebugSymbols=false`。csproj 中 `BukitStripSymbols` 的默认值 `false` 只影响本地发布。

因此 M1 和 M3 的收益比最初估计的小。

## 4. 优化清单

证据状态的含义：**已确认** = 在代码中核实了问题存在；**线索** = 看到了可疑迹象，执行前必须先验证；**未测** = 问题存在但收益未测量。

### 第一层：只改构建配置

| 编号 | 措施 | 证据 | 涉及文件 | 承载变更 |
|---|---|---|---|---|
| M1 | 补充 AOT 发布属性：`IlcFoldIdenticalMethodBodies`、`OptimizationPreference`（Size 与 Speed 各测）、特性开关；本地发布默认剥离符号 | 已确认 csproj 只有基本属性；收益未测，预计不大 | `Bukit.Cli.csproj` | `tune-native-aot-size-and-gc` |
| M2 | GC 设置：关闭并发 GC、`GCConserveMemory`、gen0 预算 | 已确认全仓库无任何 GC 配置；收益未测 | `Bukit.Cli.csproj` | 同上 |

M1 明确排除：`UseSystemResourceKeys`（会把框架异常消息换成资源键，而 Bukit 多处把 `ex.Message` 展示给用户）、`StackTraceSupport=false`、`InvariantTimezone`、`IlcInstructionSet`。

### 第二层：局部代码修改

| 编号 | 措施 | 证据 | 涉及文件 | 承载变更 |
|---|---|---|---|---|
| M3 | 14 个构建路径静态正则改为 `[GeneratedRegex]`。AOT 下 `RegexOptions.Compiled` 被忽略，这些模式由解释器执行 | 已确认；收益未测 | `SemanticHtmlAuditRules`（9）、`BasicMarkdownToHtml`（2）、`ContentImageRewritePipeline`（1）、`SeoDocumentTitleResolver`（1）、`AnalyticsManagedBlockFilter`（1） | `source-generate-build-path-regex` |
| M4 | 替换 AngleSharp。`HtmlTextProjection` 对每个页面做完整 DOM 解析，供搜索索引和 Markdown 正文投影使用 | 已确认 Core 只有 2 个文件使用；收益未测 | `Bukit.Engine/HtmlTextProjection.cs`、`MiniProgramHtmlSanitizer.cs` | 待起草 |
| M5 | 修正渲染并行度。`ComputeOptimalParallelism` 在超过 1000 页时用 1.5 倍核数，注释理由是"I/O 密集"，但模板渲染是 CPU 密集 | 线索。11-10 计划记录过"10 线程只用约 2 核，原因未定"，需先确认是否仍存在 | `Bukit.Engine/PageRenderDispatcher.cs` | 待起草，先采样 |
| M6 | 组件模板解析缓存。`ComponentFunctions.Invoke` 每次调用都执行 `Template.Parse`，只缓存了模板文本 | 已确认该路径每次都解析；该路径是否为主题实际使用的路径待确认 | `Bukit.Rendering/Scriban/ComponentFunctions.cs` | 待起草 |

M4 注意：`HtmlTextProjection` 中有针对 AngleSharp 1.5.2 的行为兼容处理（脚本提前结束、template 元素），替换时必须保持输出逐字节一致。小程序 HTML 清洗器是安全相关代码，需单独评估，可以不在同一变更内。

### 第三层：渲染管线

| 编号 | 措施 | 证据 | 承载变更 |
|---|---|---|---|
| M7 | 页面输出流式化：向池化缓冲区直接写 UTF-8，避免超过 85 KB 的字符串进入大对象堆 | 线索。Core 只有 8 处 `ArrayPool`；实际的大对象堆占用未测 | 待起草，先做内存采样 |
| M8 | 减少每页的 `ScriptObject` 构造，把不随页面变化的部分做成只读共享对象 | 线索。渲染层有 141 处 `ScriptObject` 引用；延续 v2.0.13 的站点模型共享 | 待起草，先采样 |
| M9 | 正文按需加载、渲染后释放 | 线索。`IContentBodyStore` 抽象已存在，需确认大站点构建时正文是否常驻 | 待起草，先验证 |

### 第四层：结构

| 编号 | 措施 | 证据 | 承载变更 |
|---|---|---|---|
| M10 | 进程外插件协议改为长连接会话，握手、取清单、调用在同一进程内完成 | 已确认当前每条消息启动一次进程 | 待起草；涉及插件协议版本 |
| M11 | `Bukit.PluginHost` 合并只有单一实现的接口（9 个） | 已确认；需核对公共 API 治理与外部消费者 | 待起草 |
| M12 | 增量构建指纹等非安全用途的哈希从 SHA256 换成 XxHash | 已确认 Core 有 41 处 SHA256 引用；优先级低 | 暂不起草 |

M12 的代价：需要新增依赖，并使现有增量缓存整体失效一次；安全相关的哈希（插件校验、隐私检查）不在范围内。

## 5. 执行顺序

| 阶段 | 内容 | 是否改 Core | 放行条件 |
|---|---|---|---|
| A | M2 的阶段 0：用现有 Native AOT 二进制先做两次 E0 基线对照，再用环境变量做 GC 实验 | 否 | 准备好的可信离线站点副本。E0 两次输出一致，并确认候选运行时设置确实生效后，再做候选测量 |
| B | M1、M2：按实验结果固化发布属性与 GC 设置 | 是（仅 csproj） | 阶段 A 有至少一组满足采纳条件 |
| C | M3 的阶段 0：测量发布审计中正则的占比；然后转换 | 是 | 用户确认 |
| D | M5 的采样：确认并行度问题是否存在 | 否 | 无 |
| E | M4、M6 | 是 | 用户逐项确认，先有 openspec 变更 |
| F | M7、M8、M9：先做一次内存与 CPU 采样，再决定做哪几项 | 视数据 | 采样结果 |
| G | M10、M11 | 是 | 用户确认；与 pi 接入一并考虑（第 6 节） |

阶段 A 使用仓库内的 `scripts/benchmarks/run-gc-experiments.py`，支持配备 Python 3.9+ 和 PyYAML 的 macOS 或 Linux。先跑两次 E0：

```bash
python3 scripts/benchmarks/run-gc-experiments.py --yes "<离线站点副本>" \
  --binary artifacts/publish/osx-arm64/bukit --runs 2
```

- 脚本把输入冻结在专用临时目录，每次从同一份输入恢复工作副本，显式指定副本内的输出和初始为空的构建缓存。站点应关闭媒体下载，移除外部命令插件，并将自定义缓存放在副本内。Notion 内容使用 `--notion-snapshot <副本内相对路径>` 离线读取。
- 脚本打印结果目录。`header.json` 记录二进制版本与 SHA-256，`results.tsv` 记录耗时、峰值 RSS 和等价性结果。原始构建日志、输出和按相对路径生成的清单也保存在该目录。
- 基线对照通过后，可以追加 `--experiments E0 E1 E2 E3 E4a E4b --runs 3`。每次从清洁环境启动。E4 使用无 `0x` 前缀的 `1000000` 和 `4000000`，分别表示 16 MiB 和 64 MiB 的最小 gen0 预算。
- 先确认 Native AOT 运行时采用的候选设置，再解释测量差异。`GCgen0size` 是实验环境变量，没有供 `RuntimeHostConfigurationOption` 使用的 `System.GC.*` 键。

### 统一的采纳条件

- 目标指标改善不少于 10%；
- 全量构建总耗时变差不超过 5%；
- 发布产物按相对路径逐文件比较，字节保持一致。比较 `.bukit` 的报告时仅归一化脚本明确列出的生成时间、耗时和由这些字段衍生的清单摘要。摘要在归一化前校验，报告的问题、计数及其余字段均须一致；
- 公共 API 无变化（PublicApiDrift 通过）；
- AOT 发布无 IL 警告。

阈值是提案值，由用户确认或修改。

## 6. DeepSeek Harness 插件机制与 pi

### 6.1 DeepSeek Harness（DSH）

DSH 的插件是导出 `apply(ctx)` 的模块，由 Cordis 管理依赖、注册和卸载；模型、工具、会话、沙箱乃至 Agent Loop 都是插件。它目前是开发者预览，API 不稳定，未经安全审计。

**不能照搬**：Cordis 依赖 JS 运行时的动态加载，Native AOT 做不到（仓库里 Wasmtime 也因 AOT 被移除）；"一切皆插件"与 Bukit "确定性的可信内容发布编译器"的定位相悖。

**可借鉴两点**：

1. 用一份能力清单统一两套插件的声明方式。
2. 插件副作用的登记与自动回收。

### 6.2 pi（pi.dev）

pi 是极简的终端编码 agent，扩展是 TypeScript 模块；非 Node 的集成走 stdin/stdout 上的 JSON RPC，Node 应用可用 SDK 内嵌。

README 明确把 AI intent 工作流排除在 Core 2.0 之外，LLM 进入 Core 会破坏构建确定性。因此：

- **方向一（先做）**：把 Bukit 做成 pi 的包。复用 `guide/skills`，加一个扩展把 `bukit build`、`doctor`、`seo audit` 等注册成工具。Core 零改动。
- **方向二**：Bukit 通过 RPC 子进程调用 pi，复用 PluginHost 的进程管控、权限和密钥遮蔽，落在 Labs。它与 M10 的长连接协议是同一形态，应在 M10 之后做。

## 7. 在 Claude Code 中执行的规则

执行者必须先读仓库根目录的 `AGENTS.md`，并遵守以下要点：

1. **一次只做一项**。从第 5 节的阶段表按顺序取，不跨项，不顺手修改范围外的代码。范围外的发现只记录。
2. **每项 Core 改动都需要用户在单独的任务里明确确认。** 本文件和 openspec 草稿都不构成批准。
3. **先有 openspec 变更再实施。** M1–M3 已有草稿；其余各项在实施前按 `openspec/changes/archive/` 中已归档变更的格式起草 `proposal.md`、`tasks.md` 和 spec，并运行 `openspec validate <名称> --strict`。
4. **先测量后修改。** 标为"线索"的项，第一步是验证问题存在；标为"未测"的项，第一步是取得基线。数据不支持就关闭该项并记录原因。
5. **验证范围**：实施前用 `python3 scripts/checks/codex-workflow.py closure` 生成验证闭包，只运行闭包内的专项测试。未经用户明确授权，不运行 `scripts/test-all.sh`、`scripts/smoke-all.sh`、整解测试或发布门禁。
6. **单写者**：同一时刻只有一个实施代理修改仓库文件。
7. **每项结束时**：一次专项审查；证据与指标按 `AGENTS.md` 记录；只有用户要求时才 commit、push 或开 PR。
8. **保护区域**：`guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/` 不得修改。

## 8. 局限

- 没有逐行阅读全部源码；第 4 节中标为"线索"的项都可能在验证后被否定。
- 没有任何一项优化的收益经过实测；第 2.2 节和各项的幅度都是估算。
- 冷启动、小站点内存、开发服务器空闲内存没有基线。
- 两份 OpenSpec 草稿已通过严格格式验证，仍待审批与对应专项验收。
- 第 6 节关于 DSH 和 pi 的描述来自 2026-11-24 的公开资料检索，两者都在快速变化。
