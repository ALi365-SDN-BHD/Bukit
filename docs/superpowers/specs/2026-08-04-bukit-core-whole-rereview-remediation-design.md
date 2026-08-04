# Bukit Core 全量复审问题闭合设计

日期：2026-08-04

状态：已批准设计方案；待人工复核书面规格后制定实施计划

审计基线：`main@1571b6735c73a8bf27d54b6350018d45f35fa51c`，并包含该提交上的现有未提交 Core 修复

审计证据：`/tmp/codex-reports/bukit-core-whole-rereview-final.md`

## 1. 目标

在不扩大产品语义、不修改非 Core 生产代码的前提下，闭合全量复审确认的 13 项 Important、8 项 Conditional Important 和 9 项 Minor，并以完整 owner specialty tests、Native AOT publish、原生二进制 smoke 和一次 delta-only 统一复审建立最终可信证据。

整体完成条件不是“测试绿色”，而是每个 finding 都有唯一 owning batch、有效 RED、最小生产修复、对应 GREEN、Native AOT 证据和无 Critical/Important 的专项或最终复审。

## 2. 强制边界

- 生产代码修改只允许位于 `src/Bukit-Core/`。
- 测试可修改 `tests/Bukit.*.Tests/` 中与 Core 直接对应的 owner/consumer 项目；不得修改 `src/Bukit-Labs/` 或 `src/Bukit-Plugins/` 生产代码。
- 不修改 CI、release、gate、workflow policy、public release 文档或历史参考目录。
- 不运行 whole-solution tests、`scripts/test-all.sh`、`scripts/smoke-all.sh`、full/release gate、`post-change-*` 或未列名矩阵。
- 不 push、不部署、不发布包；本地 merge 仍需单独授权。
- 所有实现保持 Native AOT：禁止动态程序集加载、runtime code generation、未声明反射序列化和 AOT 不可分析的动态调用。
- 使用单写者队列；任一时刻只有一个 batch 可以处于 `writing`、`testing` 或 `review_wait`。
- 每批在写代码前生成机器可读 closure，列出 changed files、direct consumers、public/serialized-contract consumers 和精确专项命令；`unmapped` 必须为零。
- 每批固定执行 `scoped RED -> 最小修复 -> 完整 owner specialty tests -> Native AOT -> 一次专项复审`。只有 Critical/Important 可重新进入实现；Minor 不触发重复复审。

## 3. Finding 账本

### 3.1 Confirmed Important

| ID | 根因 | Owning batch |
|---|---|---|
| I-01 | render dependency hash 遗漏 `site.data` 值和 module 可渲染字段 | Batch 1 |
| I-02 | render value encoder 无类型/framing/sequence 且 culture-sensitive | Batch 1 |
| I-03 | 损坏 recovery state fail open 且 live file 非原子写 | Batch 1 |
| I-04 | Atom 时间格式依赖当前 culture | Batch 1 |
| I-05 | route query/fragment 进入物理输出路径 | Batch 1 |
| I-06 | 图片缓存、变体和转换产物缺少有界完整解码验证 | Batch 2 |
| I-07 | plugin invoke 可把 resource-limit kill 报告为 success | Batch 3 |
| I-08 | strict config 静默忽略错误 YAML node kind | Batch 4 |
| I-09 | 重复 SourceKey 覆盖 body store 并错路由正文 | Batch 4 |
| I-10 | Composite body store 不转发生命周期且部分失败泄漏 | Batch 4 |
| I-11 | accepted GetAsync 与 Dispose 之间存在未启动 Lazy 竞态 | Batch 4 |
| I-12 | Notion auto-summary 在 canonical graph 建立后修改共享字段 | Batch 5 |
| I-13 | raw-to-canonical merge 违反 case-insensitive precedence 合同 | Batch 4 |

### 3.2 Conditional Important

| ID | 条件风险 | Owning batch |
|---|---|---|
| CI-01 | plugin 子孙进程绕过父进程 CPU/内存限制 | Batch 3 |
| CI-02 | external-tool 父进程退出后，后代持有 pipe 并遗留任务/进程 | Batch 3 |
| CI-03 | 不读消息的 WebSocket 客户端无限阻塞 reload/rebuild | Batch 3 |
| CI-04 | template symlink 越过 layouts 根目录 | Batch 6 |
| CI-05 | Markdown 枚举后 pathname replacement/symlink 越界读取 | Batch 6 |
| CI-06 | Windows trailing-dot/space/device alias 逃过 route inventory | Batch 1 |
| CI-07 | Notion cache 截断 live JSON，崩溃/并发造成半写文件 | Batch 5 |
| CI-08 | Notion pagination 重复 cursor 导致重复积累或无限请求 | Batch 5 |

### 3.3 Minor

| ID | 问题 | Owning batch |
|---|---|---|
| M-01 | BodyCache 超限 1 项却批量驱逐 10% | Batch 4 |
| M-02 | MediaIndex static path gate 永不回收 | Batch 2 |
| M-03 | shared feed collision winner 依赖输入顺序 | Batch 1 |
| M-04 | taxonomy feed equal-time 20-item window 不稳定 | Batch 1 |
| M-05 | 合法 `*-<digits>w` 用户源图被当作 generated | Batch 2 |
| M-06 | Section/Collection/llms/archive 等 selector 缺最终稳定 tie-break | Batch 1 |
| M-07 | DevFileWatcher fire-and-forget task 与 Dispose semaphore 竞态 | Batch 3 |
| M-08 | Markdown long/double parsing 使用 CurrentCulture | Batch 6 |
| M-09 | media URL 日志仍可能包含 userinfo/path credential | Batch 2 |

## 4. Conditional finding 的闭合规则

Conditional Important 不允许凭推测修改行为，也不允许因触发环境复杂而直接跳过：

1. 先在 owning batch 内建立可控、可重复且不依赖公网的触发夹具。
2. RED 必须表现为报告中的故障语义，例如未终止后代、重复 cursor 继续请求、symlink 越界读取或半写 cache 被当作有效。
3. 若触发成立，按本规格的 fail-closed 设计做最小修复。
4. 若当前平台无法构造该条件，必须保留条件 finding 为 `unverified`，记录缺失平台/权限，不得宣称 closed，也不得用无关重构代替。
5. 跨平台安全合同无法可靠实施时，配置或操作必须在不支持的平台明确 fail closed，不能退回“只约束父进程”或 lexical check 的假安全。

## 5. 七批串行闭包架构

### Batch 0：可信基线与执行账本

此批不改生产代码。它锁定当前 HEAD 与 dirty worktree 中已获批准的 Core delta，为 30 个 findings 建立唯一状态记录和六个实现 batch 的 closure。

要求：

- 保存 baseline、全部现有 dirty paths、finding-to-batch 映射、精确 test commands 和 Native AOT command。
- 对现有未提交修改做路径归属；任何无法映射到 I/CI/M finding 的修改必须在实施前隔离，不得顺手带入。
- 初始化单写者队列、metrics state、每批独立 evidence/report 路径。
- 不重复运行未失效的绿色证据；cache reuse 必须满足 HEAD、closure、命令、SDK 和相关环境状态完全一致。

### Batch 1：增量确定性、恢复状态、feed 与 route identity

闭合：I-01、I-02、I-03、I-04、I-05、CI-06、M-03、M-04、M-06。

设计：

- 将 `RenderDependencyHashWriter` 提升为唯一 canonical value encoder：每个值包含类型标签与字节长度 framing；null、string、bool、整数、浮点/decimal、日期、dictionary、sequence 和已批准 record 均使用 invariant、ordinal、递归结构编码。
- dictionary 按 Ordinal key 排序；语义无序集合先编码元素再按编码字节排序；语义有序 sequence 保持输入顺序。
- encoder 使用 active-reference cycle detection、64 层最大深度和 100,000 节点上限；cycle、超限和不支持类型以稳定的构建诊断 fail closed，不回退到 `object.ToString()`。
- `SiteModelDataContributor` 对 `site.data` 调用 canonical encoder，并显式编码 ModuleInfo 的 Id、Title、Slug、Content、Fields；不得只记录 key/count/ID。
- recovery state 采用同目录临时文件、flush-to-disk 和 atomic replace。状态文件存在但不可读、schema/version 未知或状态未知时，一律视为 incomplete 并触发 clean recovery。
- Atom 使用显式 `CultureInfo.InvariantCulture` 和 UTC RFC3339 literal 格式。
- route identity 禁止 query、fragment、NUL、trailing dot/space 和 Windows device aliases；该合同在所有平台统一执行，使同一配置跨平台产生相同接受/拒绝结果。
- 所有外部可见 selector 在主排序键后增加 Ordinal canonical URL/ID tie-break，并在 `Take`、dedupe 或分组 winner 之前完成排序。

兼容性：此前含 query/fragment、Windows alias 或 unsupported render value 的配置将稳定失败；这是为防止不可达输出、平台差异和 stale HTML 接受的必要收紧。

### Batch 2：图片、媒体产物和安全日志

闭合：I-06、M-02、M-05、M-09。

设计：

- `ImageContentValidator` 保留 MIME allowlist 和 signature match，再执行两阶段验证：Identify 获取格式/尺寸并执行 checked pixel budget；重新从文件起点使用 approved decoder 完整 Load，确认实际 decoder format、尺寸和 frame budget后立即释放 image。
- 固定安全预算：Identify 阶段以 `width × height × max(frameMetadataCount, 1)` 计算总 decoded-pixel estimate，最多 100,000,000 pixels 且最多 256 frames；所有乘法使用 checked arithmetic。Load 使用 `MaxFrames=257`，解码后再次检查实际 frame count 与总 pixels，超过预算拒绝。该上限只用于安全验证，不改变 resize 输出尺寸。
- ImageSharp 3.1.12 没有 approved decoder 的 AVIF/ICO 继续 fail closed。不得因扩展名或 magic 恢复接受。
- 下载临时文件、缓存命中、move collision winner、resize 临时文件和 external image optimizer 产物必须在 atomic publish/ownership tracking 前共用同一 validator。
- external converter 的 expected output MIME/format 由请求类型决定；exit 0、文件存在或扩展名匹配均不是成功证据。
- generated variant 身份由 ownership/freshness manifest 证明，不能只凭 `*-<digits>w` 文件名；未被 manifest 拥有的同名文件按用户源文件处理。
- MediaIndex path gate 使用带引用计数的 lease；最后一个持有者释放时以 key/value identity 条件移除 static dictionary，不能移除后来者的新 gate。
- 媒体 URL 日志只保留规范化 scheme、host、port 和固定 `<redacted-path>`；移除 userinfo、query、fragment 和原始 path，避免猜测路径中哪一段是密钥。

### Batch 3：插件资源语义、进程树与 dev 生命周期

闭合：I-07、CI-01、CI-02、CI-03、M-07。

设计：

- `PluginProtocolClient` 在反序列化 invoke success 前统一调用 process terminal-state gate；timeout、output limit、resource limit、取消和不可接受 exit 均优先失败。资源超限不得降级为 `processExitMismatch` warning。
- 引入 internal process-tree limiter abstraction。Windows 使用 Job Object；Unix 使用独立 process group，并对可证明的整组终止与资源采样负责。配置 resource limit 而当前平台无法证明整棵进程树受控时，在启动前返回稳定 unsupported/resource-limit diagnostic。
- external-tool 在父进程退出后仍无法在限定 drain window 内完成 stdout/stderr 时，终止对应 process group/tree、等待 reader 收口，再返回稳定失败；不得仅 seal collector 后遗留后台任务。
- WebSocket broadcast 为每个 client 使用 linked shutdown token 和 2 秒 send timeout，并发发送后汇总结果；单个 client timeout/失败只移除该 client，不阻塞其他 client 或 rebuild。
- `DevFileWatcher` 跟踪所有 scheduled rebuild tasks。新增异步停止/释放路径：先停止 watcher、取消 lifetime token、等待 tracked tasks、最后释放 semaphore；`DevCommand` 必须 await 该路径。

兼容性：启用资源限制但平台不能提供真实进程树约束时将 fail closed；未配置资源限制的插件启动语义不变。

### Batch 4：strict config、正文路由与 canonical 生命周期

闭合：I-08、I-09、I-10、I-11、I-13、M-01。

设计：

- strict validator 的 mapping、sequence、scalar 访问器必须区分“字段不存在”和“字段存在但 node kind 错误”。后者统一抛 `ConfigException(ConfigInvalidValue)`，错误路径包含完整 YAML field path 与 expected/actual kind。
- loader helper 同样不得把错误 node kind 转换为 null/default；strict validation 与 loader 对相同输入必须给出一致失败。
- 重复 public SourceKey 的兼容语义保持“合并文档”，不能直接禁止。Composite 为每个 provider 分配稳定 internal store route token；最终 document ID、sourceKey 和 collection 不变，只有 opaque BodyKey 携带 token。委派前去除 token并恢复原始 BodyKey。
- `CompositeContentBodyStore` 实现 `IAsyncDisposable`，对 distinct child stores 恰好释放一次。LoadRawAsync 任一 provider、relation projection 或组装失败时，释放所有已成功获得的 stores，再重抛原异常。
- `BodyCacheDecorator` 与 `NotionBodyStore` 使用 admission/active-operation gate：GetAsync 在检查 disposed 与发布/启动 Lazy 前取得 operation lease；Dispose 关闭新 admission、等待所有已接受调用完成，再取消 lifetime、清 cache 和释放 inner/CTS。
- `ContentDocumentFactory.MergeFields` 不再原地修改调用方 mutable dictionary；始终创建 OrdinalIgnoreCase dictionary，先导入 properties、再覆盖 customFields，显式实现文档注释的 precedence。
- BodyCache 只驱逐 `_cache.Count - maxEntries` 个最旧项；并发下每次循环重新检查 excess，不能按容量百分比过度 trim。

兼容性：重复 SourceKey 仍可用，public document identity 不变；错误 YAML node kind 从静默默认变为稳定配置失败。

### Batch 5：Notion 摘要、cache durability 与 pagination

闭合：I-12、CI-07、CI-08。

设计：

- `RenderContent=true`、AutoSummary 开启且原文无 summary 时，在 RawContentDocument/canonical graph 构建前，以现有 `RenderConcurrency`（未配置时为 4）取得页面 HTML并派生摘要；结果写入新的 OrdinalIgnoreCase fields dictionary，不修改已发布 dictionary。`RenderContent=false` 时保持现有“不获取正文、不生成摘要”语义。
- 为避免重复请求，预取 HTML 直接作为已完成 body result 注入 NotionBodyStore；后续 GetAsync 读取同一 immutable result。AutoSummary 关闭时继续保持 lazy body fetch。
- page HTML cache 和 relation target cache 使用同目录 temp file、flush-to-disk、atomic replace 与跨进程 lock file；取消/异常清理 temp，永不截断 live JSON。
- 三个 pagination loop 共用 cursor guard：记录已见的非空 cursor；重复 cursor 立即抛稳定 Notion pagination exception。每个逻辑 pagination 最多 10,000 次请求；超过预算同样 fail closed。
- content source 的 MaxItems 仍是结果数量合同，不得用它替代 cursor/request budget；block/table renderer 也必须受 guard 保护。

兼容性：异常或循环 cursor 从潜在挂起变为明确失败；有效 Notion pagination 与 cache schema 不变。

### Batch 6：文件系统 trust boundary 与 Markdown determinism

闭合：CI-04、CI-05、M-08。

设计：

- 从现有 Engine no-follow opener 提取最小 handle-based read primitive 到 `Bukit.Shared.IO`。类型保持 internal，通过明确的 `InternalsVisibleTo` 仅开放给 `Bukit.Engine`、`Bukit.Rendering` 和 `Bukit.Content`，不增加 public API。
- Windows 使用已打开 handle 拒绝 reparse point并核对 final path；Linux/macOS 使用 `O_NOFOLLOW`/平台 final-path API。验证和读取必须基于同一 handle，不得 validate pathname 后二次 `File.OpenRead`。
- `FileTemplateLoader` 的 sync/async load 都从 verified handle 读取；layouts root 外 final path 稳定拒绝。
- Markdown enumeration 只保存候选 identity；实际 body load 时从 source root 重新做 handle-based open/final-path containment。文件在两阶段之间被替换为 symlink 时 fail closed。
- Markdown long/double parsing 显式使用 `InvariantCulture` 与确定的 NumberStyles；日期的现有 invariant 合同保持不变。

兼容性：layouts/content 目录中的 symlink 输入不再被跟随；普通文件行为不变。

### Batch 7：统一验收

此批不新增产品范围。它只汇总 Batch 0-6 evidence、处理跨批交叉风险并执行最终硬门。

统一复审只检查：

- render canonical encoder 与 body/canonical graph 的交叉；
- process lifecycle、resource terminal state 与 dev shutdown；
- image validation、ownership 与 media index；
- Notion prefetch/cache/body-store lifecycle；
- shared no-follow opener 的三个直接消费者；
- public/config/serialized contract 与 AOT rooting。

最终 specialty 项目固定为：

1. `tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj`
2. `tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj`
3. `tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj`
4. `tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj`
5. `tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj`
6. `tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj`
7. `tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj`
8. `tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj`
9. `tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj`
10. `tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj`
11. `tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj`
12. `tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj`
13. `tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj`
14. `tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj`

每个实现 batch 均执行 fresh Native AOT：

```bash
aot_dir="$(mktemp -d /tmp/bukit-core-whole-remediation-aot.XXXXXX)"
dotnet publish src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj \
  -c Release -r osx-arm64 --self-contained true \
  -p:PublishAot=true -o "$aot_dir/publish"
"$aot_dir/publish/bukit" version
"$aot_dir/publish/bukit" --help
```

最终整体只能在以下条件同时满足时标记 CLOSED：

- 13/13 Confirmed Important 有 RED/GREEN/复审证据；
- 8/8 Conditional Important 均有可重复 RED、GREEN 和复审证据并标记 `closed`；任何因缺失平台/权限而保留的 `unverified` 都使整体状态保持 PARTIAL，且不得计为 closed；
- 9/9 Minor 已闭合，或经用户明确决定延期；
- 14 个最终 specialty 项目全部 GREEN；
- final Native AOT publish、`version`、`--help` 全部 exit 0；
- delta-only unified review 为 Critical 0 / Important 0；
- 没有未映射 changed file、writer conflict、未观察后台任务或未披露环境阻塞。

## 6. 测试设计原则

- 每个 RED 只证明一个 finding；不得用编译失败、mock 配置错误或不相关异常充当 RED。
- 并发问题使用 deterministic seam、barrier、TCS 或 fake clock，不使用概率 sleep。
- 文件系统问题使用独立临时根目录、真实 symlink/reparse 条件和同一 handle 验证；平台不支持时明确 skip reason 并保持 finding unverified。
- 进程问题使用受控 probe executable，所有子孙进程必须在测试 finally 中可回收。
- 图片夹具必须包含真实可解码图、有效头部后截断图、错误 MIME、超 pixel budget、超过 frame budget和伪成功 converter 输出。
- culture 测试保存并恢复 CurrentCulture/CurrentUICulture，不并行污染其他测试。
- AOT smoke 不替代 specialty test；specialty GREEN 也不替代源码语义复审。

## 7. 提交和复审策略

- 每个 batch 形成一个或少量按根因组织的本地提交；不得把多个 ownership batch 混入同一提交。
- 提交前只 stage 当前 batch closure 明确列出的文件。
- 每批只有一次 specialty review；Critical/Important 修复后只做该 finding 的 scoped re-review。
- Batch 7 只做一次 delta-only unified review，不重复历史全量审计。
- 任何相邻改进、API 美化、性能重构或新配置能力均记录为后续建议，不进入本计划。

## 8. 非目标

- 不修复 Bukit Labs、Bukit Plugins、网站业务代码或第三方插件。
- 不重写整个 incremental engine、content model、plugin protocol 或 Notion transport。
- 不新增远程服务、daemon、数据库、动态插件 loader 或新的用户可见配置体系。
- 不把 Conditional finding 自动升级为不受边界约束的安全重构。
- 不以 release gate、全仓测试或 benchmark 代替上述精确验收。

## 9. 预期终态

完成后，同一输入在不同 culture、调度顺序和受支持平台上产生一致输出；incremental build 不复用由 data/module/typed value 变化失效的页面；损坏 recovery/cache 状态安全失败；正文 store 与异步任务都有明确所有权；图片和进程结果在越过发布边界前完成真实验证；Notion 与 filesystem 的循环、并发和 symlink 边界均可控；CLI 继续通过 Native AOT 发布与原生 smoke。
