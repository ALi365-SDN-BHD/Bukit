# Bukit 全量深度代码审计报告

> 审计日期：2026-07-31
>
> 源码基线：`main@dd355ae3f0afe2c64f601b22e1233d75113ac08c`
>
> 审计范围：`src/Bukit-Core/`（14 个项目）、`src/Bukit-Plugins/`（7 个插件）、`src/Bukit-Labs/`、`tests/`（20+ 测试项目），共 759 个 C# 源文件、约 101,626 行。
>
> 审计方式：只读静态分析 + 五路并行模块深审（Engine / Content·Notion / CLI·PluginHost / Rendering·Config / Plugins·Labs），关键发现由主审计逐一复核源码确认。
>
> 交付性质：只读审计结论，本任务不修改任何代码、不运行测试与构建。
>
> 前置基线：2026-07-09 旧 Core 审计 8 项、2026-07-15 全面审计 8 项（3 P1 + 5 P2）、Wechat 插件审计、SSRF 统一化、sync-over-async 消除、异常吞没修复等均已关闭，本次审计确认无回归，**不重复报告**。

## 1. 执行摘要

本轮未发现 Critical 级（可利用的远程执行、默认路径数据破坏、不可恢复损坏）缺陷。共确认 **16 个 Important 级问题**与 **87 个 Minor 级问题**，其中：

| 严重度 | 数量 | 重点 |
|---|---:|---|
| Critical | 0 | — |
| Important | 16 | 并发字典无锁竞争、外部进程超时后孤儿化、Notion 链路无超时/取消、SVG 存储型 XSS 向量、缓存中毒、默认 HTTP 路径无 SSRF、tar 解压符号链接、Scriban 输出未转义、模板缓存命中仍全量 IO |
| Minor | 87 | 静默降级、路径/正则加固、死代码、O(n²) 残留、Console 直出日志、敏感信息加固等 |

项目整体工程成熟度较高：分层清晰、契约治理强（架构测试、public API 基线、AOT 兼容）、安全防线（SsrfGuard、SecretMasker、路径守护、插件信任链、原子 manifest 保存）已系统化。当前问题集中在**进程生命周期管理、并发缓存一致性、网络链路超时与内容类型校验**四类，多为"配置默认值/边界条件"类缺陷而非设计方向错误。

## 2. 审计方法与证据等级

- 五路并行模块审计代理 + 主审计复核：所有 Important 发现均回到源码逐行复核（本报告中标注的 `文件:行号` 已核对）。
- 未运行全量/专项测试（审计任务约定），未修改任何文件。
- 证据等级：已确认（源码路径闭环）/ 高可信风险（触发条件明确）/ 加固建议（纵深防御）。

## 3. 分模块发现

### 3.1 Bukit.Engine（5 Important / 18 Minor）

#### IMP-E1 BuildManifest.Entries 普通 Dictionary 无锁并发读写
- `src/Bukit-Core/Bukit.Engine/Incremental/BuildManifest.cs:9` 定义普通 `Dictionary<string, BuildManifestEntry>`；
- `SpecialListRenderer.cs:103` 无锁读 `manifest.Entries.TryGetValue(...)`；`:147-159` 写方持 `lock (manifest)`；`IncrementalBuildEngine.cs:174` 另一无锁读点；
- 触发路径：`PageRenderDispatcher.cs:331` `Parallel.ForEachAsync(specialLists, ...)` 并行执行；字典并发读写（扩容）可抛 `InvalidOperationException` 或产生损坏状态。
- 建议：读方统一 `lock (manifest)`，或改 `ConcurrentDictionary`，或将 manifest 更新集中主线程。

#### IMP-E2 ScssCompiler 进程未释放 + 超时孤儿进程 + stdout 未读
- `src/Bukit-Core/Bukit.Engine/ScssCompiler.cs:48-58`：`Process.Start` 结果无 `using`；5 秒超时后 `WaitForExitAsync` 抛 `TaskCanceledException` 被 `:74` catch 吞掉，**子进程不被 kill** → 孤儿 sass 进程稍后可能写出 .css 覆盖预期产物；`RedirectStandardOutput = true` 但 stdout 从不读取 → 管道缓冲满时进程阻塞（管道死锁）。
- 建议：`using var process` + catch 中 `process.Kill(entireProcessTree: true)` + 双流异步读取。

#### IMP-E3 ImageOptimizer 超时孤儿进程
- `src/Bukit-Core/Bukit.Engine/ImageOptimizer.cs:115-117,142-144,186-197`：超时后仅 `using` 释放 Process 对象，**不 kill 子进程** → 孤儿 cwebp/magick 持续运行；stdout 重定向但从不读取（同 IMP-E2 管道风险）。
- 建议：catch `OperationCanceledException` 时 `Kill(entireProcessTree: true)`；双流异步读取。

#### IMP-E4 PagesIndexPlugin Notion 链路无超时/无取消
- `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/PagesIndexPlugin.cs:304-307`：`sem.WaitAsync(CancellationToken.None)` + `FetchAsync(client, pageId, CancellationToken.None)`；`:388` 媒体本地化同样传 `CancellationToken.None`。
- 影响：Notion API 网络挂起时整个构建（含 CI）永久阻塞；`Task.WhenAll(:259)` 一并挂起。NotionApiClient 默认无硬超时。
- 建议：传递构建级 token 并为 Notion 获取/下载挂整体超时（LinkedTokenSource）。

#### IMP-E5 DataFilesPlugin 解析失败静默跳过
- `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/DataFilesPlugin.cs:99-102`：`catch { continue; }` 使 data 目录内任一 yaml/json/toml 解析失败即静默丢弃，站点以缺数据状态构建且用户无感知。
- 建议：至少 `logger.Warn(...)`；建议失败即构建失败（配置化）。

#### Minor（Engine，18 项，择要）
- 安全：`TaxonomyFeedWriter.cs:46-50` 绕过 FileWriter 统一路径校验（纵深防御）；`ImageMetadataReader.cs:303-304` front-matter 图片路径未校验 `..`；`RobotsTxtWriter.cs:60-75` bot 名未剔除换行可注入 robots.txt 行；`LlmsTxtPlugin.cs:433-436` Markdown 链接未转义 `]`/`)`；`PagesIndexCacheHelper.cs:222-231` cache_path 支持任意绝对路径。
- 错误处理：`TaxonomyMetadataLoader.cs:76-78` 空 catch；`ImageOptimizer.cs:178-180` 探测空 catch；`BuildManifest.cs:79-83` 损坏降级仅 Console.Error；多处库代码用 Console.Error 绕过 ILogger（DirectoryCopy.cs 13 处等）。
- 性能：`IncrementalBuildEngine.cs:144-191` 每个列表遍历全部文档 O(L×D)；`BuildManifestTracker.cs:318`/`ImageMetadataReader.cs:166` 全量读文件仅需哈希/头部；`MachineReadabilityTrustAuditBuilder.Core.cs:70` 每路由全量读 HTML；`TaxonomyTermsInjector.cs:52,200` O(n²)；`MachineReadabilityTrustAuditBuilder.Helpers.cs:107-116` 成对比较 O(k²)。
- 质量：`SiteEngine.cs:322-340`、`I18nOutputMerger.cs:44-71`、`BuildPathUtils.cs:140-270` 死代码；`LlmsTxtPlugin.cs:238-431` 同步/异步双份实现重复 95 行。

### 3.2 Bukit.Content / Bukit.Content.Notion / Bukit.Notion（5 Important / 16 Minor）

#### IMP-C1 NotionClient 默认构造路径无 SSRF 防护且跟随重定向
- `src/Bukit-Core/Bukit.Notion/Transport/NotionClient.cs:198-214`：`HttpHandlerFactory` 为 null 时使用裸 `SocketsHttpHandler`（无 ConnectCallback 守卫）。
- 当前生产路径 `NotionApiClient.cs:122`、`NotionContentClient.cs:94` 均正确注入 `SsrfGuard.CreateSafeHandler()`（已复核），实际利用面低；但公开默认构造是未来插件/调用方的隐患。
- 建议：默认路径也安装 SsrfGuard，或对未注入 handler 的构造告警。

#### IMP-C2 ImageAssetLocalizer 注入路径 DNS 预检查 TOCTOU + 重定向不检查
- `src/Bukit-Core/Bukit.Content/Media/ImageAssetLocalizer.cs:193-198`：`_ownsHttpClient == false`（internal 注入构造）时退化为 `SsrfGuard.IsPrivateHostAsync` DNS 预检查，检查与实际连接两次独立解析 → DNS rebinding 窗口；重定向目标不经任何检查。
- 生产默认构造（`:57-61`）用 ConnectCallback 逐连接检查（含重定向），无此问题。
- 建议：注入路径同样要求 handler 自带守卫；预检查仅作纵深防御。

#### IMP-C3 下载内容类型校验过宽：SVG/空类型/octet-stream 全放行（存储型 XSS 向量）
- `src/Bukit-Core/Bukit.Content/Media/ImageAssetLocalizer.cs:232-238,406-416`：`image/svg+xml` 被原样落盘为 `.svg`（`:22-25` 映射表）并在站点同源提供 → 脚本携带型 SVG 构成**存储型 XSS** 向量；空 Content-Type 与 `application/octet-stream` 放行任意字节落盘（内容嗅探绕过）。
- 建议：Content-Type 白名单精确匹配（jpeg/png/gif/webp/avif/bmp）；SVG 单独策略（禁止或净化）；空类型拒绝；octet-stream 结合魔数校验。

#### IMP-C4 BodyCacheDecorator 缓存捕获首个调用者 CancellationToken 且失败条目永不逐出（缓存中毒）
- `src/Bukit-Core/Bukit.Content/BodyCacheDecorator.cs:67-69`：Lazy 闭包捕获**第一个调用者**的 document 与 cancellationToken 并永久缓存；首调用者 token 取消/超时 → 共享 task 变 Cancelled/Faulted，后续所有调用持续失败；无失败逐出逻辑（仅 LRU 超上限才清理）→ 一次瞬时错误可永久毒化页面渲染直至构建结束；缓存还持有大对象 document 造成内存滞留。
- 建议：inner 加载使用内部生命周期 token；task Faulted/Cancelled 时立即 `TryRemove(key, lazy)`。

#### IMP-C5 NotionBodyStore 同根因（仅可自愈）
- `src/Bukit-Core/Bukit.Content.Notion/NotionBodyStore.cs:26-41`：同 IMP-C4 捕获首调用者 token；区别是该类 catch 中按 KeyValuePair 精确移除失败条目（可自愈），但一次取消仍会使并发等待者集体失败。
- 建议：统一改为不捕获调用者 token。

#### Minor（Content/Notion，16 项，择要）
- 安全：`NotionContentSource.cs:258-263` 异常消息携带页面属性原始 JSON；`NotionContentSourceOptions.cs`/`NotionProviderOptions.cs` record 默认 ToString 泄露 Token（对照组 `NotionClientOptions.cs:19-20` 已正确重写）；`CalloutBlockRenderer.cs:25-45` 图标 URL 未走 RenderingSafeUrl 白名单；SsrfGuard 未覆盖 198.18.0.0/15。
- 并发：`NotionBlockRendererRegistry.cs:20-52` 普通 Dictionary 公开可变无锁；BodyCacheDecorator(Ordinal) 与 NotionBodyStore(OrdinalIgnoreCase) 键比较策略不一致；`ImageAssetLocalizer._failures` 无界增长。
- 错误处理：`MarkdownFrontMatterParser.cs:87-91` 解析失败静默降级 + 全库唯一 Console 直出；`NotionClient.cs:269-283` Retry-After 为过去时间 → 立即重试空转、退避无抖动；传输错误仅保留 rootErrorType；`ImageAssetLocalizer.cs:281` 失败 reason 含未脱敏 ex.Message。
- 性能：`NotionContentSource.cs:46,214,287` 每次渲染新建 HttpClient 无连接复用；`MediaIndexManager.cs:91-110` 每新 URL 全目录枚举 O(n²)；无失败负缓存 → 失败源反复网络请求；`NotionClient.cs:134,168` 响应双重缓冲；`NotionBlocksRenderer.cs:45-126` 页面渲染串行。
- 质量：`NotionProviderOptions`/`NotionContentSourceOptions` 近乎重复；NormalizeFieldKey 三份重复实现；`NotionCacheManager.cs:106` 缓存写非原子（可自愈）。

### 3.3 Bukit.Cli / Bukit.Cli.Shared / Bukit.Shared / Bukit.PluginHost / Bukit.Plugin.Abstractions（1 Important / 26 Minor）

#### IMP-P1 插件进程 stdin 写入不受超时约束；取消时子进程成孤儿
- `src/Bukit-Core/Bukit.PluginHost/SystemProcessRunner.cs:54`：`WriteStandardInputAsync(process, request.StandardInput, cancellationToken)` 只用调用方 token（未链接 `timeoutCts`）且位于 `:57` try 块之外。
- 影响：插件不读 stdin 且 payload 超过 OS 管道缓冲（~64KB，invoke JSON 大参数时）→ `WriteAsync` 永久挂起，**超时机制完全不生效**；若调用方取消，OCE 从 try 外抛出，进程不被 `KillProcess` 清理 → 孤儿进程。当前 CLI 链路传 `CancellationToken.None` 掩盖了该缺陷。
- 建议：stdin 写入改 `linkedCts.Token` 并整体纳入受保护段，取消时必然 KillProcess。

#### Minor（CLI/PluginHost，26 项，择要）
- 安全：`PluginProtocolClient.cs:184-199` 资源监控 MaxCpuTime/MaxMemoryBytes 在全部调用链**从未传值**（死功能，安全边界虚假承诺）；`CleanCommand.cs:50-51` 无条件删 `.cache`/`.bukit`（cwd 为 `/` 时危险）；`Program.cs:77-85` catch-all 打印未脱敏 InnerException；`GitHubPagesDeployProvider.Validation.cs:8-23` SanitizeError 精确匹配漏 token 编码变体；`PluginCommandInvoker.cs:39-62` 插件输出直打控制台未脱敏（与报告不一致）；`SsrfGuard.cs:14-23` DNS rebinding 残余 TOCTOU；`DeploymentPrivacyValidator.cs:153-154` O(文件×token) 扫描。
- 并发：`SystemProcessRunner.cs:61-66` 超时瞬间正常退出被误判超时（exitCode 强制 -1）；`PluginExecutionReporter.cs:16-17` 毫秒时间戳并发覆盖写同一报告；`DeployCommand.cs:135`/`PluginCommandInvoker.cs:38` 不可取消；`PreviewCommand.cs:105-107` fire-and-forget 无并发上限。
- 错误处理：`GitHubPagesDeployProvider.Git.cs:218-276` 先 WaitForExit 后读管道（死锁反模式）；`:98-99` WaitForExit(3000) 返回值被忽略；`Validation.cs:54-57` 非 fast-forward 判定依赖英文输出（本地化失效）；`PluginCommandInvoker.cs:157` int.Parse 抛未包装 FormatException；`CliBoundCommand.cs:18-22` GetInt 解析失败静默回退默认值。
- 质量：`PluginConfigLoader.cs:126` PermissionsExplicit 恒 true 死检查；`DoctorManifestChecker.cs:176-186` 无效 O(n²) 死循环；`PluginManifestMigrator.cs:26-27` 残留注释；`UrlRedactor.cs:32` fragment URL 被改写为 `?[REDACTED]`；`SeoExternalAuditor.cs:31` 报告 outputPath 无相对路径校验；`CliParser.cs:82` 不接受 `-` 开头选项值；`CompletionCommand.cs:36-42` fish 补全转义不完整。

### 3.4 Bukit.Rendering / Bukit.Routing / Bukit.Theme / Bukit.Config / Bukit.Engine.Abstractions（2 Important / 17 Minor）

#### IMP-R1 Scriban 输出默认不转义 + 内容字段/Props 直出 → 存储型 XSS 面
- `src/Bukit-Core/Bukit.Rendering/Scriban/SectionRenderHelper.cs:228-235,275`：section props 原样注入 ScriptObject 并 `sectionTemplate.Render(...)`（Scriban `{{ }}` 默认不 HTML 转义）；
- `ScribanPageModelMapper.cs:11-19`：`title/url/content/fields` 全部原样 SetValue（含渲染后 HTML）；
- `TemplateContextBuilder.cs:51-57`：启用 `EnableRelaxedMemberAccess/EnableRelaxedTargetAccess/EnableNullIndexer` 宽松访问。
- 影响：内容作者或 Notion 导入字段可在自定义字段/section props 写入 `<script>`/事件属性/`javascript:` URL，主题模板 `{{ fields.x }}`/`{{ props.x }}` 直出即注入生成站点的静态 HTML；拼入 HTML 属性时存在属性逃逸。ImageFunctions（HtmlEncode + scheme 白名单）已示范正确做法但仅覆盖 image 辅助函数。
- 建议：内容字段默认转义、仅显式 `| safe` 放行；SectionSchema 增加 `type: html` 显式声明；拦截 `javascript:` URL 与事件属性。

#### IMP-R2 ScribanTemplateRenderer 缓存命中仍全量读文件 + SHA256
- `src/Bukit-Core/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs:166-169`：先 `File.ReadAllText` + `ComputeContentHash` 再查 `_cache`——缓存命中时仍发生全量 IO+哈希；`TryGetCachedSectionTemplate(:235-238)` 同款。布局模板每页每层渲染触发一次，section 模板每 section 一次。对比 `FileTemplateLoader.LoadCached(L141-158)` 先查缓存的设计倒退。
- 建议：先用 `LastWriteTimeUtc + Length` 签名查缓存，命中即返回；仅未命中/签名变化时才读文件。

#### Minor（Rendering/Config，17 项，择要）
- 安全：`SectionRenderHelper.cs:305-314` 渲染错误 ex.Message 写入产出 HTML 注释（源码泄露）；`FileTemplateLoader.cs:73-77,109-134` GetFullPath+StartsWith 校验不解析符号链接（信任模型内低风险）；`ThemeTokensProcessor.cs:29` tokens 值未转义可注入额外 CSS 属性；`ProviderValidators.cs:285` 域名正则嵌套量词回溯面（253 长度上限有界，未缓存）；`I18nValidator.cs:278-335` 固定正则每次 new。
- 并发：`RoutePathBuilder.cs:164,173` static SlugCache 无界增长。
- 错误处理：`ThemeTokensLoader.cs:24-27`、`PageComposer.cs:32-35`、`SectionSchema.cs:28-31` 三处 catch 吞异常静默降级；`ThemeDoctorCommand.cs:291-293` 空 catch + `:233` "not yet implemented" 占位。
- 性能：`SectionDataResolver.cs:19-25` 每 section 全量扫描 allPages（页面数×section×文档数放大）；`ComponentFunctions.cs:79` 组件模板每次 Template.Parse 无缓存；`ContentFieldReader.cs:206` 大小写回退 O(n) 线性扫描。
- 质量：`ConfigJsonSchemaGenerator.cs` 752 行大文件；`ContentBodyResolver.cs:18-27` [Obsolete] 阻塞桥接仍保留。
- 正面确认：ImageFunctions、ShortcodeProcessor（GeneratedRegex）、RouteSecurityValidator、ConfigYamlHelpers 布尔陷阱处理均正确；模板缓存 ConcurrentDictionary 线程安全；TemplateContext 每渲染新建无跨线程复用。

### 3.5 Bukit-Plugins / Bukit-Labs（3 Important / 10 Minor）

#### IMP-L1 ThemeInstallCommand tar 解压未拒绝符号链接/硬链接条目（潜在 tar-slip + DoS）
- `src/Bukit-Labs/Bukit.Labs.Cli/Commands/Theme/ThemeInstallCommand.cs:192-216`：循环只检查路径前缀（防 `../`，做得好），**未显式拒绝 `TarEntryType.SymbolicLink`/`HardLink`**；.NET 8 下 `ExtractToFileAsync` 对非 RegularFile 抛 `NotSupportedException`，而 catch 只捕获 `InvalidDataException` → 恶意归档使 CLI 崩溃（DoS）且 `finally` 为空、tmpDir 清理（:236）不执行 → 临时目录残留；若未来版本支持提取符号链接则演变为 tar-slip 写任意文件。
- 建议：循环开头显式跳过/拒绝非 RegularFile 条目并记录警告；catch 扩展捕获 NotSupportedException/IOException；finally 中删除 tmpDir；解压总量设上限。

#### IMP-L2 ThemeInstallCommand `--url` 安装无 SHA256 完整性校验、下载无大小上限
- `src/Bukit-Labs/Bukit.Labs.Cli/Commands/Theme/ThemeInstallCommand.cs:133-167`：registry 安装路径（:99-111）有 Sha256 校验 + 失败拒绝，但 `--url` 路径**完全没有完整性校验**，`CopyToAsync` 无限流下载（磁盘填充 DoS）。
- 建议：`--url` 增加可选 `--sha256` 校验；流式写入 + 大小上限（如 100MB）。

#### IMP-L3 WechatDraftGateway.DefaultDownloadImageAsync 每次下载新建 HttpClient
- `src/Bukit-Plugins/Bukit.WechatSyncing/WechatDraftGateway.cs:592-627`：`internal static` 方法每次调用 `new HttpClient(new SocketsHttpHandler {...})`（SSRF 防护存在，好），每张图一次全新 TCP 连接 + TLS 握手，批量同步大量图片时 socket/端口耗尽风险。
- 建议：改为网关级长生命周期 HttpClient（与 :69 网关客户端统一或注入共享下载器）。

#### Minor（Plugins/Labs，10 项，择要）
- `CloneAssetDownloader.cs:20-41` catch{} 静默吞下载失败 + GetByteArrayAsync 无大小上限；`VisualCommand.cs:98-99` 生成 Playwright 脚本单引号未转义（JS 注入到生成文件）；`IndexNowHttpClient.cs:25` 响应体无大小限制；`WebhookCommand.cs:33,68-72,84` 硬编码 http:// 无 TLS 选项、每请求 Task.Run 无并发上限（令牌用 FixedTimeTokenEquals 好）；`ContentImageProcessor.cs:60-92` 图片逐个顺序下载上传；`ContentProcessor.cs` 约 28 处内联正则多次全量扫描长 HTML；`ImportSafetyScanner.cs:9-62` 目录全量遍历 3-4+ 次；`WechatDraftGateway.cs:69` 构造函数裸 HttpClient（实际仅访问固定端点，一致性建议）；`CloneContentAssetHelpers.cs:80-86` 整串 Replace O(n×m)；`SyncCache/WechatSyncWorkflow` 每次状态转换全量写盘（正确性优先的取舍，可选合并写入）。
- 正面确认：Import 插件无 git/进程调用、路径全部经 ImportPluginPathGuard；所有 HTTP 出口均有 SSRF 防护（含重定向逐跳）；无 token/secret 写入日志；SyncCache 运行锁+身份守卫+原子替换完善；WebhookCommand 恒定时间比较正确。

## 4. 测试质量快速核验

- 测试代码中的 `.GetAwaiter().GetResult()`/`Thread.Sleep` 仅存在于测试辅助 handler（`CaptureHandler`、模拟网络延迟），不属于反模式，可接受。
- 测试覆盖面广（20+ 测试项目、508 个测试文件），架构测试、安全回归、AOT 兼容测试齐备；本次审计发现的问题大多缺少针对性测试（如进程超时孤儿、缓存失败逐出、tar 特殊条目、模板缓存 IO 顺序），建议随修复补充。

## 5. 修复优先级建议

| 优先级 | 项 | 说明 |
|---|---|---|
| P0 | IMP-E2/E3 进程超时孤儿 | 超时后 kill 进程树 + 读双流，改动局部、收益高 |
| P0 | IMP-P1 stdin 不受超时约束 | 插件协议层挂起可致 CLI 永久无响应 |
| P0 | IMP-E4 Notion 无超时 | 网络挂起可阻塞整个构建/CI |
| P0 | IMP-L1 tar 符号链接 | 恶意归档 DoS 现成、未来 tar-slip |
| P1 | IMP-C3 SVG/内容类型校验 | 存储型 XSS 向量，需白名单+魔数 |
| P1 | IMP-R1 Scriban 转义策略 | 需模板约定+文档双管齐下 |
| P1 | IMP-E1 manifest 并发 | 并行列表渲染数据竞争 |
| P1 | IMP-C4/C5 缓存中毒 | 一次瞬时错误毒化页面缓存 |
| P2 | IMP-C1/C2 SSRF 默认路径 | 纵深防御统一 |
| P2 | IMP-R2 模板缓存 IO 顺序 | 性能收益大、改动局部 |
| P2 | IMP-L2/IMP-L3 下载校验与复用 | Labs/插件健壮性 |
| P3 | Minor 项 | 死代码、正则缓存、日志接入、负缓存等，随迭代清理 |

## 6. 结论

1. **未发现 Critical 缺陷**：命令注入（ArgumentList）、路径穿越（多重守护）、SSRF（生产路径统一 SsrfGuard）、token 泄露（SecretMasker/红act）等主防线复核通过。
2. **16 个 Important 中 11 个集中在"进程生命周期、网络超时、并发缓存一致性、内容类型校验"四类**，均为边界条件缺陷，修复范围局部、可验证。
3. **架构与治理仍健康**：模块化单体、契约基线、AOT 静态注册、原子写入、信任链校验均为工程亮点。
4. **建议按第 5 节优先级推进修复**，每个修复配针对性测试（进程孤儿、缓存逐出、tar 条目、模板缓存 IO 顺序目前均无测试覆盖）。

**统计：Critical 0 / Important 16 / Minor 87**
