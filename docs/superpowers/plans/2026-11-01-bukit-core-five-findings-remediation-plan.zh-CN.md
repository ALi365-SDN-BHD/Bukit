# Bukit Core 五项问题修复、测试与验证方案

> **历史方案快照：**下文的状态和证据描述以编制时的基线为准，并非当前验收结论。后续实施记录见[归档任务清单](../../../openspec/changes/archive/2026-11-01-fix-core-five-findings/tasks.md)；归档本身不代表所有验收证据均已闭合。

> 状态：三轮严格审计后修订，待实施。本文中的测试、验收和任务编排均为计划，不代表已经完成修复或取得 GREEN 证据；“五项修复均已闭合”须满足第 10 节证据条件。
>
> 日期：2026-11-01<br>
> 源码核对基线：`de9a5ebf`<br>
> 范围：Bukit Core 及其直接测试、用户说明和验证策略映射。<br>
> 证据来源：源码、调用链、现有测试、公共 API 工具及项目引用图核对，隔离 Git 语义/条件配置传输、JSON 合法性及 .NET 解码、Notion store 取消时序、API 加载器探针，以及 `closure` / `classify` 预览。

## 1. 目标与实施顺序

针对已发现的五项 Core 问题，明确根因、修复边界、兼容性、回归测试、专项命令和可观察的验收结果。

推荐顺序：

1. **准备阶段。** 先建立队列，完成策略映射/资源分类修正，再建立 OpenSpec change 和唯一 `tasks.md`。
2. **安全修复包：问题 1、2、4。** 优先处理原稿保护、部署目标身份和隐私漏检。
3. **Notion 稳定性包：问题 3。** 修复限速、正文并发和资源生命周期。
4. **增量一致性包：问题 5。** 先补齐 Notion 正文模式指纹和 Markdown 正文快照一致性，再统一内容哈希保存与比较口径。
5. **一次最终统一审查。** 检查跨包交点、失效证据、未覆盖变更和未关闭的重要发现。

| 编号 | 已核实的根因 | 主要后果 | 修复重点 |
|---|---|---|---|
| 1 | 写入资源检查遗漏本地内容源；worktree 另有提交入口，注入 provider 的合成配置不代表真实读写位置 | 原稿可能被清理或被缓存污染；仅按合成配置补检查还会误拒绝合法调用 | 按真实可知读写路径统一预检，覆盖原项目 worktree 正式目标及内置 Notion 可写缓存 |
| 2 | Git origin 查询继承 CWD；定位环境、全局同名 remote 及条件 include 下的 URL 重写均可影响目标 | 配置项目、身份查询与实际 Git 传输目标可能错配 | 仓库作用域定址，先建临时仓库/私有 remote，再在相同有效配置中前检和传输 |
| 3 | 正文按页面创建客户端，普通正文读取缺少并发门；提前退出和失败任务清理不完整 | 限速重置、并发超限、请求未结束即释放资源，取消后下一次读取承受旧失败 | 加载级共享客户端、正文并发门、全退出路径收束及失败任务按身份清除 |
| 4 | JSON 解码后只检查 provider 标记，解析失败被放行，字符串解码还可能抛出非 JsonException | 转义 ID/超深 JSON 漏检，warn 可能被解码异常中断 | 共用解码扫描，将解析及输入解码失败纳入扫描未完成 |
| 5 | manifest 保存/比较口径不同；现有稳定指纹还遗漏正文模式及两次读取的一致性前提 | 多渲染一轮；直接统一哈希又可能错误复用旧正文 | 先保证指纹对应有效模式和实际正文，再统一保存与比较 |

### 1.1 证据状态

- 已完成：静态源码核对、消费者核对、测试缺口分析、候选路径闭包与资源分类预览。
- 已执行的审计探针：不同工作目录下继承 `GIT_DIR` 仍返回原仓库 origin；65 层数组中的转义 ID 经 Python JSON 解码还原，原文不含完整 ID。本机 .NET 10 SDK 文档确认默认 JSON 最大深度为 64。
- 前两轮 Git 探针：全局同名 origin 可覆盖 B 的有效 origin，普通 clone 也可读取 A；已核对作用域查询、worktree 优先/缺失回退、显式 include、空首值保留及私有 remote。此前私有 remote 的成功对照仅覆盖当时配置，不能排除第三轮发现的条件 include 缺口。
- .NET 10.0.0 探针已确认：`{"x":"\uD800"}` 和 `{"\uD800":"x"}` 可以通过 `JsonDocument.Parse`，但读取字符串值或属性名会抛出 `InvalidOperationException`；合法代理项配对正常解码。
- 第三轮 Git 2.39.5 探针：独立前检未发现规则，但 `hasconfig:remote.*.url` 在注入 remote 后、`gitdir` 在 clone 创建 `.git` 后激活 include，真实本地传输读取了另一仓库。没有执行提交、推送或互联网 Git 传输；第 3.2.2 节据此替换原传输顺序。
- 第三轮 .NET 10.0.0 探针：执行原 `NotionBodyStore` 类并使用最小文档类型替身，确认唯一等待者取消后，延迟失败留给第二次读取，第三次才重新调用 factory；执行原 API 加载器并使用 SDK 依赖，确认相对根加载失败、绝对根对照成功。
- 第三轮 3、5、8 有上述隔离运行时证据，其余八项为经复核的静态源码/测试推导。详细报告及探针索引曾保存在本机临时目录，不随本方案提交；其中方案行号对应修订前的 754 行版本。
- 上述探针没有执行 .NET 隐私扫描器、完整 Core 来源/构建集成或实际部署；第 7.5 节的 18 个受管程序集覆盖结论来自项目引用图静态核对，尚未执行 Core 编译或完整 API 对比。绝对根加载成功不能替代 API-01/API-02。
- 待完成：失败回归复现、代码修复、完整专项测试、专项审查及最终统一审查。
- 下文的 RED 均指实施时需要实际证明的失败结果；GREEN 均指修复后需要取得的通过证据。
- 实施前重新核对 HEAD 和工作树；基线发生变化时，先更新受影响的分析和验证闭包。

### 1.2 严格审计修订对照

首轮审计 1–6、复审 1–2、第三轮 1–11 与原五项问题编号分开记录。第三轮为 Important 8、Minor 3，全部纳入本次方案；下表只表示设计及验收要求已有落点，运行时发现仍须实施证据关闭。

| 审计项 | 修订落点 | 实施时的直接证明 |
|---|---|---|
| 1：合成配置与真实来源混淆 | 第 2.2–2.5 节 | SRC-10–SRC-13：公开 provider 入口的合法调用、真实冲突、Composite 和来源可见性边界 |
| 2：Git 环境覆盖工作目录 | 第 3.2–3.5 节 | DEP-09–DEP-12：真实 Git、污染环境及原仓库/索引保护 |
| 3：JSON 解析失败放行 | 第 5.2–5.5 节 | PRI-07、PRI-12–PRI-14：深度边界、未完成检查及 strict/warn/deploy 行为 |
| 4：API 验收没有实时对比 | 第 7.5 节 | API-01 新鲜 Core 程序集构建；API-02 当前编译表面对比 |
| 5：缺少 OpenSpec 唯一任务清单 | 第 7.6、8 节 | change 验证、唯一 `tasks.md` 及逐项证据引用 |
| 6：策略写入早于队列初始化 | 第 7.3、8.1 节 | 队列初始化、策略任务闭包/classify、acquire 均先于策略写入 |
| 复审 1：全局 origin 仍覆盖项目身份 | 第 3.2.1–3.2.2、3.5 节 | DEP-13–DEP-16：local/worktree 定址、私有 remote 实际传输、URL 重写阻断及认证保留 |
| 复审 2：字符串解码异常未归类 | 第 5.2、5.5 节 | PRI-15–PRI-16：未配对代理项、合法配对，以及异常不越过扫描器进入 warn 安全门前的行为 |
| 第三轮 1：显式多语言 CLI 绕过事务入口 | 第 2.2、2.4–2.6、7.6 节 | SRC-14–SRC-15：原项目来源与正式目标预检、原稿不变、未启动 worker，安全多语言对照成功 |
| 第三轮 2：注入内置 Notion 的真实缓存未入写集 | 第 2.2–2.6 节 | SRC-16–SRC-18：实际 readwrite 路径先于加载被检查；readonly/off、独立目录及嵌套来源对照 |
| 第三轮 3：条件 include 在前检后激活重写 | 第 3.2.2、3.5–3.6 节 | DEP-17–DEP-18：仓库/私有 remote 建立后的相同有效配置前检，错误目标传输未启动 |
| 第三轮 4：分类关系调度提前退出漏收束 | 第 4.4–4.7 节 | NOT-13：冷/热关系缓存混合取消与异常，已启动请求结束后才释放资源，保留原异常/token |
| 第三轮 5：无人等待的正文失败任务未清除 | 第 4.4–4.7 节 | NOT-14：唯一等待者取消后延迟失败，下一次同键读取立即新尝试，旧任务不删除新条目 |
| 第三轮 6：Notion 指纹遗漏正文模式 | 第 6.1–6.5 节 | INC-09：有效 RenderContent 双向切换失效，稳定模式复用，公开 HTML 与全量一致 |
| 第三轮 7：Markdown 指纹与实际正文不一致 | 第 6.1–6.5 节 | INC-10–INC-11：正文读取中核对同一指纹，A→B 提交前失败，A→A 保持首热复用 |
| 第三轮 8：API-02 相对根导致加载失败 | 第 7.1–7.5 节 | API-02 使用绝对仓库根，精确命令登记/分类/缓存一致，并取得新鲜 Core 表面对比 |
| 第三轮 9：Composite 最终包装构造漏清理 | 第 4.4–4.7 节 | NOT-15：最终构造失败时，已成功加载的子 store 恰好清理一次 |
| 第三轮 10：空正文预取未保留 | 第 4.2、4.6–4.7 节 | NOT-16：AutoSummary 与 cache off 的空页，正文请求不重复且摘要行为保持 |
| 第三轮 11：部署兼容性缺少用户指南落点 | 第 3.3–3.4、5.3–5.4、7.3、8.2 节 | 部署指南路径映射齐全；origin/重写迁移及 JSON 模式说明与 DEP、PRI 证据一致 |

## 2. 问题 1：内容源与输出重叠，原稿可能被清理

### 2.1 根因与触发链

关键位置，行号以核对基线为准：

- `src/Bukit-Core/Bukit.Engine/BuildTransaction.cs:98`：来源保护仅包含主题目录。
- `src/Bukit-Core/Bukit.Engine/BuildPlanner.cs:90`：首次 `clean:false`、没有旧 manifest/marker 时，非空目录可能通过。
- `src/Bukit-Core/Bukit.Engine/SiteEngine.cs:215`：成功构建写入输出 marker 并提交。
- `src/Bukit-Core/Bukit.Cli/Commands/CleanCommand.cs:52`：清理输出后，还会删除项目 `.cache`。
- `src/Bukit-Core/Bukit.Engine/SiteEngine.cs:114–124`、`BuildOptionsMapper.cs:26`：公开 provider 重载合成默认 `content` 来源配置，实际内容由 `FixedContentProviderFactory` 返回注入的 provider；该合成来源不能作为磁盘事实。
- `src/Bukit-Core/Bukit.Cli/Commands/BuildCommand.cs:88–100`：显式多语言 CLI 走独立 worktree 流程；`WorktreeBuildRun.cs:116–141,348–351` 的准备及正式目标提交不经过 `BuildTransaction.Begin`。只检查 detached worktree 的来源，保护不到原项目中被交换/删除的目录。
- `src/Bukit-Core/Bukit.Engine/BuildTransaction.cs:174–183` 只枚举配置中的 Notion 缓存；注入内置 Notion 保留自己的 options。`NotionCacheManager.cs:9–25` 可在真实 `CacheDir` 创建 `pages`，合成配置无法代表这条写入路径。

典型触发过程：

1. Markdown 来源配置为 `content/`，输出也配置为 `content/`。
2. 首次非清理构建通过，向该目录写入产物和输出标记。
3. 后续配置型 clean 将其识别为可清理输出。
4. 原始 Markdown 随目录递归删除。

marker 只能证明目录曾被构建使用，不能证明其中没有原始输入。检查只放在递归删除前，也无法阻止首次构建污染来源目录。

### 2.2 推荐修复

在 Engine 内增加接收真实来源根与实际写目标的内部检查方法，由事务构建、CLI worktree 入口和配置型 clean 共同使用：

1. 配置构建和配置型 clean：按实际有效配置收集所有本地 Markdown 来源根目录。
2. `BuildAsync(IContentProvider, BuildOptions)`：从注入的内置 `MarkdownFolderProvider` 读取真实 `ContentDir`；递归检查内置 `CompositeContentProvider` 的子来源，同时收集内置 `NotionContentSource` 的实际可写缓存位置。通过 Content 已有 friend assembly 边界暴露最少的内部只读成员，不增加公共来源接口或修改 `IContentProvider`。
3. 把注入入口的来源根集合及补充写目标经内部 `BuildCoreAsync` 参数传到 `BuildTransaction.Begin`。区分“未传来源集合，按配置解析”与“显式空集合，没有可识别本地根”；后者绝不能回退到合成的 `content`。来源、缓存相对路径均按各 provider 的实际进程/配置语义解析，不能统一改按合成项目根解释。
4. 共享检查复用现有路径规范化、符号链接解析及实际文件系统大小写判定，检查路径相等、父子目录双向包含关系。
5. 覆盖实际受管写集：输出、默认及自定义缓存、媒体下载、Notion 可写缓存、外部 metrics 文件及其伴随 HTML；注入内置 provider 的额外缓存写入也必须与全部已知 Markdown 根比较。按有效缓存模式枚举：只有 readwrite 且非空 `CacheDir` 才贡献写目标，覆盖 pages、relations 及其临时/锁文件；readonly/off 或空目录不虚构写入。
6. 在 `WorktreeBuildRun.StartAsync` 中，以应用覆盖后的配置、原项目真实来源根和正式输出/缓存/媒体/metrics 目标执行同一预检。预检先于写锁、运行目录、暂存和 worker 创建；保留后续 worktree 输入校验，但不能用映射后的输入副本代替原项目来源。最终目录交换及备份清除必须处于此已验证范围。
7. 事务构建在创建暂存目录及加载 provider 前执行检查；配置型 clean 在删除任何内容前，一次性检查输出和 `.cache` 两个删除目标。发现缓存写目标的过程只读 options，不调用会创建缓存目录的工厂。
8. 使用现有 `BuildOutputUnsafe` 诊断，使 CLI 返回明确的配置错误。

共享路径检查可放在 `OutputDirectoryCleaner.cs` 中，通过已有的 `InternalsVisibleTo("bukit")` 供 CLI 调用。合成配置仍可服务于原有内容处理，但不承担注入 provider 的来源发现职责。

### 2.3 兼容性决策

**本方案按整个来源根目录保护。** `includePaths/includeGlobs` 控制内容选择，不缩小来源根目录的保护范围。

因此，`markdown.dir: "."` 即使配合精确选择，也会与项目内输出、缓存冲突。实施时同步提供迁移说明：将来源根目录收窄到独立内容目录。

公共 `CleanIfExists(rootDir, outputDir)` 签名继续有效。配置来源保护通过内部入口提供；无配置目录清理继续遵循现有危险路径及 marker 契约。

来源保护保证覆盖配置中声明的根，以及注入的内置 Markdown/Composite 可识别的根。任意自定义 provider 或不透明包装器的私有磁盘输入无法从现有公共接口推断；此类调用保持原有输出安全规则，不虚构来源、不一律拒绝，也不宣称其隐藏根已经受保护。该边界须写入契约说明和最终验收结论，公共 API 继续保持兼容。

注入的内置 Notion 可知写入同样受冲突预检约束；独立安全缓存、readonly/off 模式保持既有行为。这项修复保证缓存不侵入已知来源根，不新增“所有注入 provider 写入均可事务回滚”的承诺。显式多语言 CLI 与进程内构建适用相同的原项目来源保护边界。

### 2.4 修改范围与消费者

生产代码：

- `src/Bukit-Core/Bukit.Engine/BuildTransaction.cs`
- `src/Bukit-Core/Bukit.Engine/OutputDirectoryCleaner.cs`
- `src/Bukit-Core/Bukit.Engine/SiteEngine.cs`
- `src/Bukit-Core/Bukit.Content/Markdown/MarkdownFolderProvider.cs`
- `src/Bukit-Core/Bukit.Content/CompositeContentProvider.cs`
- `src/Bukit-Core/Bukit.Content/Notion/NotionContentSource.cs`
- `src/Bukit-Core/Bukit.Cli/Commands/CleanCommand.cs`
- `src/Bukit-Core/Bukit.Cli/Commands/WorktreeBuildRun.cs`

测试：

- `tests/Bukit.Engine.Tests/BuildTransactionTests.cs`
- `tests/Bukit.Engine.Tests/SiteEngineSourceProtectionTests.cs`（新增，验证公开 provider 入口）
- `tests/Bukit.Cli.Tests/CleanCommandTests.cs`
- `tests/Bukit.Cli.Tests/WorktreeBuildCoordinatorTests.cs`
- `tests/Bukit.Content.Tests/CompositeContentProviderTests.cs`（内置来源发现与嵌套组合）

消费者重点核对：两种公开 `SiteEngine.BuildAsync` 入口、`BuildOptionsMapper`、`FixedContentProviderFactory`、`BuildPlanner`、CLI build、deploy 内部构建及 dev 重建；另追踪 `WorktreeBuildRepository` / `WorktreeBuildPaths` / `WorktreeBuildTransaction`、`I18nWorktreeBuild` 的原项目→暂存→正式目标映射，以及 `NotionCacheManager` / `NotionRelationTargetCache` 的真实写路径。内部来源成员只读取 provider 配置，不触发内容加载、缓存创建或提前取得客户端所有权。

配套说明：`guide/user/05-markdown-content.md`、`guide/user/06-notion-content.md`，以及第 7.6 节事务和 i18n-worktree 规范 delta 中的来源保护场景。

### 2.5 回归测试

| ID | 场景 | 修复后必须证明 |
|---|---|---|
| SRC-01 | 首次构建，来源等于输出，`clean:false` | 在写 marker、输出、缓存前拒绝 |
| SRC-02 | 输出是来源父目录或子目录 | 两个方向都拒绝 |
| SRC-03 | 来源位于 `.cache`，输出本身安全 | clean 在删除输出前拒绝，避免部分清理 |
| SRC-04 | 来源与自定义缓存、metrics 冲突 | 同一共享检查生效 |
| SRC-05 | 来源目录已有旧 marker | marker 不能绕过来源保护 |
| SRC-06 | 符号链接、大小写别名、尚未创建的末级目录 | 按真实路径身份判定 |
| SRC-07 | `content` 与 `content-old` | 不因字符串前缀误判 |
| SRC-08 | 多来源、独立安全目录 | 正常构建和清理成功 |
| SRC-09 | `dir: "."` 配合精确选择 | 按明确的根目录保护契约拒绝，并给出迁移提示 |
| SRC-10 | 公开入口传入内存 provider，输出命名为 `content` | 正常构建，不把合成的默认 Markdown 来源当作真实输入 |
| SRC-11 | 注入 Markdown provider，真实根和输出均为 `articles`，`clean:false` | 在 marker、暂存及缓存写入前拒绝，原稿哈希不变 |
| SRC-12 | 注入多层 Composite，某个 Markdown 子来源与输出/缓存重叠 | 识别真实子来源并拒绝；全部独立的对照组合成功 |
| SRC-13 | 不透明自定义 provider，含明确无本地输入的对照 | 不猜测默认 `content`；测试与说明仅承诺可识别根的保护范围 |
| SRC-14 | 显式多语言 CLI，原项目来源=正式输出；首次 no-clean 及旧 marker clean | 原项目冲突在锁/暂存/worker 创建前拒绝；不交换目录或删除备份，原稿和旧目标不变 |
| SRC-15 | 显式多语言 CLI，原来源与正式缓存、媒体或 metrics/伴随 HTML 冲突；独立目录对照 | 以原项目路径判定，不被 detached worktree 副本掩盖；冲突时未启动 worker，安全构建成功 |
| SRC-16 | 注入 Markdown/Notion Composite，readwrite 缓存与 Markdown 根相等或双向包含 | 加载前拒绝；pages/relations、缓存 JSON、临时/锁文件未创建，原稿及既存缓存哨兵不变 |
| SRC-17 | 注入内置 Notion 的 readonly/off、空 CacheDir、独立 readwrite 目录 | 不将无写入模式误判为写冲突，安全组合成功；保持实际缓存语义 |
| SRC-18 | 嵌套 Composite，Notion 相对缓存路径或路径别名与 Markdown 根重叠 | 按 provider 实际进程语义及统一真实路径规则识别冲突，不改用合成配置路径 |

核心 RED：由真实首次非清理构建暴露来源目录被接受的问题，并由配置型 clean 回归暴露旧 marker 或 `.cache` 删除路径的缺口。

SRC-11/12 同时承担公开入口漏保护的 RED；SRC-10/13 是防止新检查引入兼容性回归的正向约束，不能要求这类原本合法调用在旧实现中失败。

SRC-14/15 使用干净且输入已跟踪的真实临时 Git 项目，通过显式 CLI worktree 分支验证，不能用进程内入口替代；SRC-16/18 使用真实内置混合 provider，证明检查早于缓存创建。SRC-17 与各安全对照分别记录为兼容性证明。

### 2.6 验收标准

- 冲突配置在首次写入或删除前失败。
- 原稿文件清单及内容哈希前后相同。
- 旧输出及缓存保持原值。
- 冲突时没有新增输出 marker。
- 安全配置和现有主题来源保护回归通过。
- 配置入口、显式多语言 CLI worktree 入口与公开 provider 入口均有真实行为证据；worktree 冲突检查针对原项目正式目标，且拒绝发生在 worker 启动前。
- 内存 provider 无误拒绝；内置 Markdown/Composite 无漏保护，注入内置 Notion 的实际可写缓存不能侵入已知来源根；readonly/off 与安全缓存对照通过。
- 公共入口、provider 和共享辅助成员通过第 7.5 节实时 API 对比；隐藏来源边界按第 2.3 节明确记录。
- 符号链接、大小写相关测试记录实际平台能力；能力不足不得记为已通过相应场景。

## 3. 问题 2：跨项目 `--config` 导致部署目标错配

### 3.1 根因与影响

- `src/Bukit-Core/Bukit.Cli/Commands/DeployCommand.cs:94` 使用 `resolved.RootDir` 定位输出。
- `src/Bukit-Core/Bukit.Cli/Deploy/GitHubPagesDeployProvider.Git.cs:146` 查询 origin 时传入空工作目录，继承当前进程 CWD。
- 同文件 `:480–501` 的 `CreateGitProcess` 继承进程环境；即使设置 `WorkingDirectory`，`GIT_DIR` 仍可把查询重定向到其他仓库。
- 全局 `remote.origin.url`（包括由 `GIT_CONFIG_GLOBAL` 指定的配置）还会参与 `remote get-url origin`，并可影响默认 remote 名为 `origin` 的 clone。只清理仓库局部环境变量或给 clone 传入 URL，均不足以证明实际传输目标正确。
- 第三轮探针进一步证明：先独立检查配置，再注入 remote 或创建 `.git`，可能使 `includeIf hasconfig:remote.*.url` / `includeIf gitdir` 在检查后才激活目标重写。相同 CWD 和环境不足以证明有效配置相同。

在 A 仓库执行指向 B 项目的 `--config` 时，构建产物身份属于 B，部署仓库身份可能属于 A。这是部署上下文缺少项目根目录导致的身份错配。

### 3.2 推荐修复

增加 provider 的内部带项目根目录重载，依次固定仓库发现、身份读取和实际传输目标：

1. `DeployCommand` 将 `resolved.RootDir` 显式传入。
2. 身份读取明确使用该目录，按第 3.2.1 节从 local/worktree 作用域选择原始 origin，不用合并所有作用域的 `remote get-url origin` 决定部署身份。
3. `CreateGitProcess` 从子进程环境移除继承的仓库局部定位、索引和对象覆盖变量：`GIT_DIR`、`GIT_WORK_TREE`、`GIT_COMMON_DIR`、`GIT_INDEX_FILE`、`GIT_OBJECT_DIRECTORY`、`GIT_ALTERNATE_OBJECT_DIRECTORIES`、`GIT_IMPLICIT_WORK_TREE`、`GIT_GRAFT_FILE`、`GIT_NO_REPLACE_OBJECTS`、`GIT_REPLACE_REF_BASE`、`GIT_PREFIX`、`GIT_INTERNAL_SUPER_PREFIX`、`GIT_SHALLOW_FILE`。
4. 同时移除可注入其他 origin/仓库配置的 `GIT_CONFIG`、`GIT_CONFIG_PARAMETERS`、`GIT_CONFIG_COUNT` 及其 `GIT_CONFIG_KEY_*` / `GIT_CONFIG_VALUE_*` 项。以本机 `git rev-parse --local-env-vars` 的名称清单核对覆盖范围；生产实现集中维护这组清理逻辑，不为每条 Git 命令增加发现子进程。
5. 环境处理覆盖身份读取和全部临时 Git 操作；后续操作使用部署临时 checkout，包括当前继承 CWD 的 `RemoteBranchExistsAsync`。保留正常 global/system 凭据、代理和 SSH 配置及其配置文件选择；`GIT_CONFIG_GLOBAL` / `GIT_CONFIG_SYSTEM` 不能提供项目身份，其 URL 重写规则仍受第 3.2.2 节约束。认证入口继续显式设置 askpass，不修改父进程环境。
6. 配置项目无法在允许作用域中解析有效 origin 时返回失败，不回退到 global/system origin、其他 CWD 或环境指定的仓库。
7. 公共 `DeployAsync(DeployContext, CancellationToken)` 保留签名，以调用时 CWD 作为项目根目录委托内部实现，并使用同一环境隔离规则。

通过显式参数传递上下文，保持进程全局工作目录稳定。

#### 3.2.1 身份读取：worktree 优先，local 回退

在配置项目目录、使用清理后的子进程环境执行：

1. 先调用 `git config --worktree --includes --null --get-all remote.origin.url`；只有退出码为 1、表示键不存在时，才调用 `git config --local --includes --null --get-all remote.origin.url`。未启用 worktree 独立配置时，Git 的 `--worktree` 本身按 local 读取。
2. 在选中的作用域内保留 Git fetch URL 的首值语义。按 NUL 读取原始值，保留空首值；首值为空、无效或非直接 GitHub 地址时失败，不跳到后续 URL、低优先级作用域或全局配置。其余退出码、超时、无法读取配置均按错误处理，不能伪装成“缺少 worktree 键”后回退。
3. 项目显式声明的 include/includeIf 随所属作用域读取；global/system 的同名 origin 和 pushurl 不参与定址。公共旧入口也使用同一规则，只把调用时 CWD 作为项目目录。
4. 复用现有 `TryParseGitHubRemoteUrl` 验证选中的原始 URL，随后生成一次规范化 `https://github.com/{owner}/{repo}.git`，供该次部署全程使用。由全局 URL 重写才能变成 GitHub 地址的别名不作为合法 origin。

配置读取需要内部保留退出码的可选查询路径，复用已有进程创建、超时和有界输出处理；不能捕获所有 `GitException` 并一律当作键缺失。

#### 3.2.2 传输绑定：先建立最终配置上下文，再前检与获取

确定 owner/repo 以后，还必须确保 Git 实际连接同一目标：

1. 每次部署生成一个内部私有 remote 名，例如 `bukit-deploy-<guid>`。在临时 checkout 先执行仓库初始化，再把私有 remote 及 canonical URL 写入该临时仓库的 local 配置；这两步不发起传输，也不修改配置项目的 remote。
2. 此时 `.git`、实际私有 remote URL 及适用的条件 include 均已进入传输配置上下文。后续分支查询、获取和推送始终使用该仓库和该 remote，不再用先前方案的临时 `-c remote.<name>.url=...` 注入或 `clone --origin` 建仓路径。
3. 在每次 `ls-remote`、fetch、push 前，以相同实际工作目录、清理后的环境和受控命令行配置集合执行 `git config --includes --null --get-regexp '^url\..*\.(insteadof|pushinsteadof)$'`。检查必须在本次传输相关配置写入之后；不能在检查后再增加会激活 include 的 remote 或配置覆盖。按 Git 的 NUL 记录及 key/value 边界读取规则，保留原值；仅退出码 1 代表无规则，其他查询/解码错误阻止传输。
4. 任一规则值是 canonical URL 的前缀时，按 Git 大小写敏感语义前置拒绝，包括空前缀；覆盖 `hasconfig`、`gitdir` 等实际生效 include 中的 `insteadOf` / `pushInsteadOf`。无关 URL 的规则继续允许，诊断说明迁移方式并沿用脱敏处理。
5. 同一次前检还须核对私有 remote 的全部有效 fetch/push URL 均为 canonical URL；有额外地址或差异时拒绝。URL 核对是传输前的补充证据，不能等传输完成后才验证，也不能以检查源项目一次代替检查临时仓库。
6. 前检通过后执行 `git ls-remote --heads <name> refs/heads/<branch>`。若目标分支存在，在已建立的仓库中 fetch 该分支并 checkout 对应本地分支；`keepHistory=false` 继续浅获取深度 1，`keepHistory=true` 取得目标分支所需完整历史。若分支不存在，使用既有空仓库建分支语义，不重新建立另一配置上下文。
7. 复用既有产物复制、隐私校验、add/commit 流程；push 显式使用该私有 remote 和目标分支，保持 force、认证、失败处理及清理语义。分支查询与 fetch 之间、checkout 与 push 之间均按当前上下文重新前检，避免后续配置变化使先前检查失效。

上述规则保留正常认证配置，同时有意拒绝改写部署目标 URL。私有 remote 隔离常见同名 origin 配置，前检覆盖条件 include 重写与有效地址异常；必须在最终配置上下文中取得真实 Git 证据。它们不增加公共 `DeployContext` 字段或新的用户配置选项。

### 3.3 兼容性决策

- `DeployContext` 和 `IDeployProvider` 已纳入公共 API 基线；采用内部重载承载新增上下文。
- dry-run 保留现有能力：免部署令牌、可以在非 Git 临时项目中运行、不会触发构建或实际部署。
- 仓库身份解析位于实际部署路径。
- `--output` 只选择产物位置，部署仓库身份由配置项目确定。
- 不再支持通过上述继承变量把部署重定位到另一个仓库或索引；这是有意收紧的安全语义。正常仓库子目录与 linked worktree 通过各自 `.git` 元数据发现仓库，补充正向回归。
- origin 取 worktree 优先、local 回退的首个原始 URL；明确保留空值/错误，不使用 global/system 的同名值兜底。需要把依赖全局 URL 别名的 origin 改为直接 GitHub 地址。
- 匹配部署 canonical URL 的 `insteadOf` / `pushInsteadOf` 规则一律拒绝，即使只改变协议或仍指向同一仓库；不匹配该目标的重写及正常 global/system 认证配置保留。通过被清理的继承运行时 config 变量注入的配置不在保留保证内。
- 更新 `guide/user/13-deploy-github-pages.md`：说明配置项目身份、worktree/local 优先级、直接 GitHub origin、移除匹配 URL 重写的迁移步骤，以及保留的正常认证设置；条件 include 同样受约束。同步说明第 5.3 节 JSON strict/warn/deploy 行为。

### 3.4 修改范围

- `src/Bukit-Core/Bukit.Cli/Commands/DeployCommand.cs`
- `src/Bukit-Core/Bukit.Cli/Deploy/GitHubPagesDeployProvider.cs`
- `src/Bukit-Core/Bukit.Cli/Deploy/GitHubPagesDeployProvider.Git.cs`
- `tests/Bukit.Cli.Tests/DeployCommandTests.cs`
- `tests/Bukit.Cli.Tests/GitHubPagesDeployProviderTests.cs`
- `guide/user/13-deploy-github-pages.md`

### 3.5 回归测试

建立 A、B 两个隔离项目，配置不同 origin 和不同输出哨兵文件。

| ID | 场景 | 修复后必须证明 |
|---|---|---|
| DEP-01 | CWD=A，`--config` 指向 B | origin 查询工作目录为 B，目标仓库和内容均属于 B |
| DEP-02 | CWD 不在任何仓库，配置指向 B | 能正确使用 B 的仓库身份 |
| DEP-03 | A 有有效 origin，独立项目 B 没有 | 部署失败，没有向 A 推送 |
| DEP-04 | 配置位于仓库子目录 | 正确识别其所属仓库 |
| DEP-05 | `--output` 覆盖输出位置 | 仓库身份仍由配置项目确定 |
| DEP-06 | dry-run、skip-build 组合 | 既有免令牌和输出校验行为保持 |
| DEP-07 | 公共旧入口 | 现有调用兼容性测试通过 |
| DEP-08 | 调用前后检查 CWD | 全局工作目录保持原值 |
| DEP-09 | B 有自己的 origin，子进程继承指向 A 的 `GIT_DIR` | 真实 Git 仍返回 B 的 origin；不能仅断言工作目录 |
| DEP-10 | B 为无仓库目录或无 origin 的独立仓库，继承 A 的定位变量 | 返回失败，不能从 A 补出 origin，不进入推送路径 |
| DEP-11 | 暂存操作继承 A 的 `GIT_WORK_TREE`、`GIT_COMMON_DIR`、`GIT_INDEX_FILE`、对象目录或运行时 config 覆盖 | 覆盖变量按策略清理；本地真实 Git 只操作暂存仓库，A 的 refs、工作树和索引哨兵不变 |
| DEP-12 | linked worktree、仓库子目录和正常认证配置 | origin 仍归属于配置项目；必要认证设置保留，父进程环境保持原值 |
| DEP-13 | B 有 local origin；global/system 或其配置文件覆盖中存在 A 的同名 origin/pushurl | 选择 B；真实 Git 的分支查询、fetch/checkout 和私有 remote push URL 均指向 B，不能只验证身份查询 |
| DEP-14 | worktree 覆盖、缺键回退、local include、多 URL 的空/无效首值及读取错误 | 遵循第 3.2.1 节优先级和首值规则；仅键缺失回退，缺少有效项目 origin 或读取失败时拒绝 |
| DEP-15 | global/system/local/include 中匹配与不匹配的 insteadOf/pushInsteadOf，包括协议重写和全局 URL 别名 | 匹配规则在任何相关 Git 传输前拒绝；原始别名不用于定址；无关规则和正常认证对照继续工作 |
| DEP-16 | 现有远端分支与新建分支、keep-history/浅获取、force，以及两次独立部署 | 初始化后 fetch/checkout 保持原分支/历史/推送语义；浅获取深度为 1、keep-history 保留历史；全链路同一 canonical URL，临时 remote 不写回源仓库 |
| DEP-17 | `includeIf hasconfig:remote.*.url:<canonical-url>` 引入匹配重写 | 私有 remote 写入后前检能看到规则，并在首次分支查询/获取前拒绝；错误仓库的 transport 未启动 |
| DEP-18 | `includeIf gitdir:<checkout>/.git` 引入匹配重写；无关条件对照 | 仓库初始化后前检能看到规则，所有相关传输前拒绝；无关条件不误拒绝，不能以事后 URL 正确代替阻断证据 |

核心 RED 覆盖三条具体失败链：fake git 证明项目目录传递错误；生产共享进程路径调用真实 Git 证明继承 `GIT_DIR` 的重定向；DEP-13/15 证明全局同名 remote 或 URL 重写能改变身份/传输。修复前失败必须落在相应断言，修复后同时取得查询和实际传输目标的证据。

DEP-17/18 还须锁定第三轮发现的“前检上下文与传输上下文不同”反例。既有隔离探针证明旧设计不成立；实施时通过生产调用链记录 init/remote 配置/前检/transport 次序及实际访问计数，证明修复阻断早于任何错误目标访问。

真实 Git 回归使用隔离临时仓库及本地 remote，污染变量只注入受测子进程；保留必要的非并行测试约束。fake git 固定 URL、仅检查工作目录或仅检查清理后的字典，均不能替代真实仓库身份及原仓库未被修改的证据。

相应 fake git 解析及现有命令断言改为支持 scoped config 查询、init、私有 remote 配置、分支查询、fetch/checkout 和 push，不能硬编码 remote 必为 `origin` 或继续要求 clone 命令。真实 Git 测试用本地 transport 或受控替身记录实际访问仓库，覆盖条件 include 的启用时序和“日志目标 B、实际访问 A”反例；测试不访问互联网或真实发布目标。

### 3.6 验收标准

- 项目 local/worktree origin、canonical URL、私有 remote 的实际 fetch/push 目标、分支和产物哨兵一致。
- 缺少或无效的项目作用域 origin 不使用全局同名值兜底；Git 配置读取错误不伪装为缺键。
- 匹配目标的 URL 重写在相关传输前拒绝，包括仓库初始化/remote 配置后才激活的 hasconfig 与 gitdir 条件 include；错误仓库 transport 访问计数为零。
- 全局同名 remote 不影响实际分支查询/fetch/推送；keep-history、深度 1、force 及正常认证配置均有对照证据。
- 继承仓库定位/索引/config 覆盖变量时仍满足身份和隔离断言；原仓库 refs、工作树及索引前后保持一致。
- dry-run、公共 API 兼容性和已有部署失败处理通过回归。
- 测试使用本地替身或本地仓库，远端发布状态不作为此项修复的验收依据。
- 部署指南覆盖本节迁移要求，并与第 5 节隐私失败模式保持一致。

## 4. 问题 3：Notion 跨页面限速重置及正文并发失控

### 4.1 根因拆解

关键位置：

- `src/Bukit-Core/Bukit.Content/Notion/NotionContentSource.cs:49`：元数据客户端在 `LoadRawAsync` 返回时释放。
- 同文件 `:197`、`:265`：摘要预取和延迟正文为每页创建客户端。
- `src/Bukit-Core/Bukit.Notion/Transport/NotionClient.cs:429`：限速许可调度状态属于客户端实例。
- `src/Bukit-Core/Bukit.Content/Notion/NotionBodyStore.cs`：普通延迟正文读取缺少来源级正文并发门。

两个独立缺口：

1. 正文每页新建客户端，每页重新获得初始限速许可。
2. 摘要预取已有并发限制，普通延迟正文读取没有执行 `renderConcurrency`。

此外，`CompositeContentProvider` 的跨源关系补查会建立独立客户端。要保证同一次来源加载中的请求连续限速，应将构建中的这条调用链一起接入。

第三轮确认了同一修复闭包内的四个边界：`NotionRelationResolver.cs:31–53` 调度提前退出可跳过已启动请求的收束；`NotionBodyStore.cs:76–95` 依赖等待者清除失败任务；`CompositeContentProvider.cs:156` 的最终包装构造在清理区域之外；`NotionContentSource.cs:195–205` 在记录成功预取种子前提前返回空正文。它们分别需要任务收束、独立失败清理、最终构造清理和空正文复用回归。

### 4.2 推荐修复：一次来源加载对应一套资源

同一次 `LoadRawAsync` 使用同一个客户端，服务于：

```text
数据库查询
  → 分类关系解析
  → 摘要预取
  → Composite 跨源关系补查
  → 延迟正文读取
```

具体设计：

- 每次加载创建一个客户端，复用其现有限速和重试实现。
- 每次加载创建一个正文并发门，容量沿用现有有效配置及默认值。
- 摘要预取、延迟正文调用同一受限渲染入口。
- 成功预取先记录正文种子，包括空或纯空白 HTML，再决定是否提取摘要；“成功得到空正文”与“未预取”分开表示，cache off 时后续读取也不重复请求。
- 每页创建独立 renderer，避免共享其可变渲染状态。
- `NotionBodyStore` 持有本次加载的客户端及并发门所有权。
- 通过现有内部 resolver 接口，将本次加载绑定的关系 resolver 暴露给 `CompositeContentProvider`。
- Composite 优先使用结果附带的 resolver，保留其他 provider 的既有 fallback 行为。
- 独立调用的来源 fallback 入口保留其独立调用生命周期。

### 4.3 限速范围与兼容性

限速范围为同一来源的一次加载及其返回的正文存储生命周期。

多个来源即使使用同一 token，也各自拥有预算。用户应按总额度分配来源配置，配置说明需要明确这一边界。

`renderConcurrency` 的验收指标是活动正文渲染数；API 请求速率由共享客户端的 `maxRps` 机制约束。

### 4.4 生命周期与取消契约

1. 加载成功前发生异常或取消：加载方先收束本次实际启动的任务，再释放客户端和并发门。分类关系调度在读缓存、排队或发起请求时提前退出，也必须等待已启动任务完成；join 不因已取消的调用 token 提前返回，清理异常不得覆盖原异常及原取消 token。
2. 加载成功：所有权转交 body store。
3. `DisposeAsync`：先关闭新请求接纳，等待已接纳操作结束，再按既有规则取消、收束后台任务，最后释放资源。
4. 单个 `GetAsync` 等待者取消，只取消该等待者，保留其他等待者共享的正文任务。
5. 共享正文任务自身失败或取消时，即使已无等待者，也必须清除对应缓存条目并观察失败；按条目/任务身份移除，不能由旧任务回调删掉新尝试。覆盖同步完成和发布时序，下一次同键读取直接新建尝试；仅等待者取消不清除仍在运行的共享任务。
6. 并发门只在成功取得许可后释放，异常与取消路径均执行释放；释放信号量和客户端晚于所有使用它们的已启动任务结束。
7. Composite 的结果组装、投影和最终 `CompositeContentBodyStore` / 返回结果构造均纳入现有清理区域，成功返回才移交所有权。最终构造因 null source key 等输入失败时，也释放已完成的子 stores，维持原异常并避免重复清理。
8. 加载阶段 token 与返回后的 store 生命周期保持明确边界，避免无意扩大取消影响。

### 4.5 修改范围

生产代码：

- `src/Bukit-Core/Bukit.Content/Notion/NotionContentSource.cs`
- `src/Bukit-Core/Bukit.Content/Notion/NotionBodyStore.cs`
- `src/Bukit-Core/Bukit.Content/Notion/NotionRelationResolver.cs`
- `src/Bukit-Core/Bukit.Content/CompositeContentProvider.cs`

测试：

- `tests/Bukit.Content.Tests/NotionBodyStoreTests.cs`
- `tests/Bukit.Content.Tests/NotionContentSourceEndToEndTests.cs`
- `tests/Bukit.Content.Tests/NotionAutoSummaryTests.cs`
- `tests/Bukit.Content.Tests/CompositeContentProviderTests.cs`
- `tests/Bukit.Content.Notion.Tests/NotionContentSourceTests.cs`
- `tests/Bukit.Content.Notion.Tests/NotionCancellationTests.cs`
- `tests/Bukit.Content.Notion.Tests/CrossSourceRelationProjectionTests.cs`

配套说明：`guide/user/06-notion-content.md`。

直接消费者核对 `NotionRelationTargetCache`、`CompositeContentBodyStore` 构造及清理、Engine 的 body store 释放路径。`NotionContentSource` 的正文模式指纹由第 8.4 节增量包实施，作为同一文件后续变更进入最终证据失效检查。

### 4.6 回归测试

使用现有可注入时钟、延迟函数和 HTTP handler，以可控任务屏障验证并发，避免依赖真实等待时间。

| ID | 测试 | 核心断言 |
|---|---|---|
| NOT-01 | 多页正文连续请求 | 许可时间序列跨页面连续，客户端计数不随页面数增长 |
| NOT-02 | 查询→关系→摘要→正文 | 同一加载的请求共享限速状态 |
| NOT-03 | 高并发读取，`renderConcurrency=2` | 活动正文渲染峰值不超过 2 |
| NOT-04 | 同一正文多个等待者 | 只执行一次实际读取 |
| NOT-05 | 预取命中、内存命中、磁盘缓存命中 | 没有额外正文 HTTP 请求 |
| NOT-06 | 一个等待者取消 | 其他等待者仍成功 |
| NOT-07 | 排队期间取消或渲染失败 | 许可没有泄漏，后续请求能够继续 |
| NOT-08 | 加载失败、投影失败、正常释放 | 客户端均恰好释放一次 |
| NOT-09 | 释放时仍有已接纳操作 | 操作期间客户端保持可用 |
| NOT-10 | 释放后读取 | 按既有契约拒绝新操作 |
| NOT-11 | 429、重试、关系权限失败 | 既有重试和诊断语义保持 |
| NOT-12 | 同一个来源对象的不同加载结果 | 生命周期独立，释放一个结果不影响另一个 |
| NOT-13 | 分类关系 A 为冷请求且已启动，B 的热缓存读取取消/抛错，A 的收尾由屏障控制 | 调度退出仍等待 A；A 结束前不释放信号量/客户端，保留原异常及 token，所有已启动任务均被观察 |
| NOT-14 | 唯一等待者取消，共享任务随后失败/取消，下一位同键读取；另含旧任务与新条目竞争 | 下一次立即新尝试并可成功，不先重抛旧缓存失败；factory 次数、按身份移除及许可可复用均正确，等待者取消不破坏仍运行任务 |
| NOT-15 | 子来源成功后，最终 Composite 包装因 null source key 构造失败 | 每个已拥有的子 store 恰好释放一次，无资源遗留，返回原构造异常 |
| NOT-16 | AutoSummary 开启、cache off 的空/纯空白正文预取，随后 GetAsync | 成功空正文被种子复用，正文 HTTP 请求仅一次，不产生新摘要或重复渲染 |

核心 RED：通过真实 `NotionContentSource` 多页读取证明限速许可重置，并通过受控阻塞正文任务证明普通读取超过配置并发值。

NOT-13/14 使用确定性屏障固定“首任务已启动→后续取消/失败→清理/重试”的顺序，不能仅分别测试单请求取消和缓存取消。NOT-14 的隔离探针只证明原 store 缺口，必须补实际专项中的回归；NOT-15/16 同样取得针对最终构造与空正文路径的失败断言。

### 4.7 验收标准

- 同一次来源加载的请求许可调度连续。
- 活动正文渲染峰值不超过有效配置。
- 缓存和预取命中没有新增正文请求，包括 cache off 时成功预取的空正文。
- 单个等待者取消不破坏共享读取。
- 唯一等待者取消后的延迟失败不污染下一次读取；旧失败任务不删除新条目，许可可复用。
- 正常、失败、取消及 Composite 最终包装失败均正确释放资源；分类关系调度提前退出时，资源释放晚于已启动请求收尾。
- 证据包含请求许可记录、活动渲染峰值、实际请求次数和资源释放计数。
- 单客户端限速单元测试作为基础证据，来源级回归作为修复成立的直接证据。

## 5. 问题 4：JSON 转义内部 ID 漏过隐私检查

### 5.1 根因

两个扫描器存在相同缺口：

- `src/Bukit-Core/Bukit.Engine.Audit/PublicOutputPrivacyCheck.cs`
- `src/Bukit-Core/Bukit.Cli/Deploy/DeploymentPrivacyValidator.cs`

原始文本扫描能匹配明文 ID；JSON 解码递归目前只识别 `source/sourceKey == notion`。

普通字段里的已知敏感 ID 经 Unicode 转义后可能漏检，例如：

```json
{"reference": "39bf\u006139a-5013-81ae-9516-fbd448f3bd47"}
```

该字符串解码后包含已知 ID，但原始文本中没有完整明文 ID，也不包含 provider 标记。

还有独立的解析完成性边界：将该转义字符串置于 65 层数组内仍是合法 JSON，但默认 `JsonDocument.Parse` 最大深度为 64。当前两侧捕获 `JsonException` 后回退原文检查，仍会漏掉该 ID。该结论目前来自 Python 合法性/解码探针、SDK 文档和源码；.NET 扫描器失败回归须在实施时取得。

复审又确认解析成功不等于字符串可解码：`{"x":"\uD800"}` 的字符串值和 `{"\uD800":"x"}` 的属性名，在 .NET 10.0.0 的 DOM 读取阶段会抛出 `InvalidOperationException`。如果共享扫描器只处理 `JsonException`，该异常会越过结果映射，在 `SiteEngine` 创建安全报告数据时中断构建，尚未进入 strict/warn 安全门。解码异常已由运行时探针验证，Core 调用链后果仍须集成回归证明。

### 5.2 推荐修复

扩展 Audit 内现有 JSON 检查，把解析、解码遍历和完成性判定一起作为内部共享方法供构建侧和部署侧调用：

- 检查解码后的字符串值。
- 检查解码后的属性名。
- 遍历对象、数组及顶层字符串。
- 保留 `source/sourceKey == notion` 检测。
- 使用两侧各自收集的敏感 token 集合。
- 内部结果区分“扫描完成且无泄露”“发现泄露”“扫描未完成”。解析位置的语法错误、深度超限等 `JsonException`，以及字符串输入解码失败，均返回未完成；原文未命中不能把该结果覆盖为通过。
- 字符串解码失败在最窄读取边界归类：确认 `ValueKind` 为 String 且文档仍有效后，对 `JsonElement.GetString()` 的 `InvalidOperationException` 返回未完成；对遍历中 `JsonProperty.Name` 的同类异常作相同处理。覆盖属性名、普通字段、数组元素及顶层字符串，并把未完成结果传回根扫描结果。
- 不依赖异常消息文本识别 Unicode 问题，不把整个扫描器或构建流程的所有 `InvalidOperationException` 都吞掉；I/O、取消及解码边界之外的程序错误继续遵循原有失败路径。诊断只记录固定的未完成原因和脱敏路径，不输出原始 JSON、属性内容或异常消息中的输入片段。
- 维持默认深度边界；提高阈值不是本问题的修复。原文已命中时可以直接判定泄露，否则须完成 JSON 扫描才能判定无泄露。
- 构建侧把泄露和未完成都映射为现有 `publicOutputPrivacy=failed` 并写入脱敏错误：strict 阻断事务，warn 报告失败并按原策略继续；既有 off 及不适用判定保持各自契约。
- 部署侧把两类失败均转换为验证错误，在 Git add/commit/push 前停止。沿用 `BKT-BUILD-SECURITY-PRIVACY-0001` / `BKT-DEPLOY-PRIVACY-0001`，消息明确区分“发现泄露”与“无法完成检查”，不能把未完成误报为已证实泄露。

现有程序集允许 CLI 访问 Audit 的内部成员，可直接复用。

### 5.3 兼容性与边界

- 保留原始文本检查、非 JSON 行为、现有错误码和路径遮蔽；报告 schema、字段和状态集合不扩展，完成性只作为内部判定和错误原因。
- 无法完成解析或字符串解码的公开 JSON 均为检查失败，包括不含敏感 ID 的超深/畸形 JSON、未配对 Unicode 代理项。这是明确的兼容性收紧；strict 构建和部署须拒绝，warn 必须得到失败报告并按既有策略继续，不能因这类输入解码异常在安全门之前中断。
- JSON 按语义解码一次，避免将普通字面反斜杠误判为再次编码的 ID。
- 未知业务 UUID 继续允许。
- 构建侧的 Notion 适用性判断、部署侧内部身份报告校验保持现有契约。
- 构建严格模式与 warn 模式分别验收，避免将报告失败等同于所有模式都会阻止构建。
- 在 `guide/user/13-deploy-github-pages.md` 说明：修复畸形/超深 JSON 或无效 Unicode 后重建；warn 构建生成产物不代表允许部署，部署仍独立拒绝未完成扫描。说明保持现有 Notion 适用性及内部身份报告前提。

### 5.4 修改范围

- `src/Bukit-Core/Bukit.Engine.Audit/PublicOutputPrivacyCheck.cs`
- `src/Bukit-Core/Bukit.Cli/Deploy/DeploymentPrivacyValidator.cs`
- `tests/Bukit.Engine.Tests/BuildReporterTests.cs`
- `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs`
- `tests/Bukit.Cli.Tests/GitHubPagesDeployProviderTests.cs`
- `guide/user/13-deploy-github-pages.md`（与第 3 节共同交付迁移和失败模式说明）

消费者重点核对：`BuildReporterSecurity`、`SiteEngine` 安全门和事务提交、GitHub Pages provider 的暂存产物隐私检查。

### 5.5 回归测试

| ID | 测试 | 核心断言 |
|---|---|---|
| PRI-01 | 已知 ID 部分转义、全部转义 | 均能识别 |
| PRI-02 | 带连字符和紧凑形式 | 延续现有 token 归一化语义 |
| PRI-03 | 嵌套对象、数组、属性名、顶层字符串 | 所有 JSON 字符串位置受检查 |
| PRI-04 | `.json` 与 `.jsonld` | 两类产物均覆盖 |
| PRI-05 | escaped provider marker | 既有覆盖继续通过 |
| PRI-06 | 未知业务 UUID、普通文本、字面反斜杠 | 正常内容不被误判 |
| PRI-07 | 无效 JSON，原文分别有/无已知 ID | 有 ID 时判泄露；无 ID 时判未完成，两者均不能报告通过 |
| PRI-08 | 内部报告与诊断输出 | 排除规则有效，诊断不泄露敏感 ID |
| PRI-09 | 严格构建包含转义 ID | 隐私检查失败，事务不提交，旧输出和缓存保持 |
| PRI-10 | warn 模式包含转义 ID | 报告准确失败，执行行为符合既有 warn 策略 |
| PRI-11 | 实际部署路径包含转义 ID | 返回 `BKT-DEPLOY-PRIVACY-0001`，在 Git add/commit/push 前停止 |
| PRI-12 | 64 层与 65 层数组包裹部分/全部转义 ID；`.json/.jsonld` 两类扩展名 | 实际 .NET 扫描：边界内解码命中，边界外返回未完成；两侧均失败 |
| PRI-13 | 深度边界内外的不含 ID 内容，以及畸形 JSON | 合法边界内内容通过；边界外和畸形内容为未完成，诊断不伪称发现 ID |
| PRI-14 | 扫描未完成分别进入 strict、warn 和实际部署路径 | strict 不提交且旧输出/缓存不变；warn 报告失败并按策略执行；部署在 add/commit/push 前拒绝 |
| PRI-15 | 未配对高/低代理项出现在属性名、普通字段、数组元素及顶层字符串；合法配对作对照 | 两侧解析/遍历均把输入解码失败归为未完成；合法配对正常扫描，不把所有 Unicode 转义一律拒绝 |
| PRI-16 | 已满足 Notion 适用性条件，包含未配对代理项的公开 JSON 进入 strict、warn 和部署路径 | 异常不越过扫描器；strict 事务不提交，warn 生成 failed 报告且按策略完成，部署返回隐私验证错误而非未归类异常；诊断脱敏 |

核心 RED：敏感 token 集合已包含目标 ID，但只在公开 JSON 的普通字段中放入转义形式，证明两侧扫描器当前漏检。

另以实际 .NET 扫描器执行 PRI-12/14，取得深度超限被错误放行的独立 RED；Python 解码成功或单独证明解析器抛异常，均不能代替扫描器与最终阻断路径的行为证据。

PRI-15/16 必须调用两侧真实扫描器和最终消费路径；原实现的误通过或未归类异常均不能满足“返回未完成并进入模式处理”的断言。`GetString()` / 属性名的独立探针只证明异常类型，不能代替 warn 构建成功且报告 failed 的端到端断言。

### 5.6 验收标准

- 两侧扫描器对同类 JSON 输入作出一致的泄露及完成性判定；解析异常及输入字符串解码异常均不能被当作通过。
- 构建错误到达安全门，部署错误到达发布阻断点。
- 未配对代理项不会以未归类异常越过扫描器；warn 明确生成失败报告并完成，strict 与部署按既有失败契约阻断，合法 Unicode 配对不误拒绝。
- 严格构建失败保持正式输出和缓存原值。
- 无关 UUID、内部报告排除和诊断脱敏回归通过。
- 第 7.5 节实时公共 API 对比通过，Architecture 依赖边界及 report inventory 专项通过；三类证据分别记录。

## 6. 问题 5：增量内容哈希保存与比较口径不同

### 6.1 根因

`src/Bukit-Core/Bukit.Engine/PageRenderDispatcher.cs` 中：

- `:122–129`：只有可以尝试跳过渲染时，才计算稳定内容哈希。
- `:204`：冷构建或模板、依赖变化导致必须渲染时，可能保存基于正文 HTML 的哈希。
- 下一次构建又使用正文指纹计算哈希，两者不一致。

现有口径相当于：

```text
部分构建保存：H(metadata, HTML)
下一次比较： H(metadata, bodyFingerprint)
```

原始哈希口径问题影响 Markdown、Notion 和可生成稳定指纹的 inline HTML，直接证据指向额外渲染。第三轮还确认了“立即保存稳定指纹”必须先满足的两个正确性前提：

- `NotionContentSource.cs:106–113` 的指纹只有 `last_edited_time` 或 page ID，正文却受有效 `RenderContent` 控制；现有渲染依赖未包含该模式。媒体本地化和自动摘要关闭时，仅切换 true/false 可保留同一复用输入，从而错误保留正文或空正文。
- `MarkdownFolderProvider.cs:114–178,202` 读取 A 并计算 body-only 指纹；`MarkdownBodyStore.cs:33–49` 延迟重新读取到 B 后，拟定规则会保存 A 指纹与 B 页面。随后恢复 A，热构建可能持续错误复用 B。

这两条为已复核的静态配置/时序推导，尚未执行完整构建序列；路径安全验证不等于正文快照一致性，不能把本包仅作为性能优化验收。

### 6.2 推荐修复

先补齐内置来源的稳定指纹语义，再让 manifest 写入统一采用现有选择规则：

1. **Notion 有效正文模式。** 在现有 `bodyFingerprint` 基础值中加入有明确分隔的有效 `RenderContent` 布尔标记，使用已经合并来源覆盖/默认值的 options，与实际正文分支一致。无需把 token、连接设置或整个配置散列进指纹；不增加公共配置或 manifest 字段。
2. **Markdown 正文核对。** 将现有 `ComputeBodyFingerprint` 的内部实现最小复用到 producer/body store，在安全 opener 已读取的同一字符串上，按与 provider 相同的 front matter 剥离、body-only SHA-256 语义计算并核对捕获指纹；HTML 渲染使用这次已核对的正文。检测到变化时返回明确的来源变化失败，在返回该正文、更新 manifest 或事务提交之前停止，保留旧输出和缓存；提示重试，诊断不输出正文内容。
3. **兼容既有正文入口。** 只对携带捕获指纹的 Markdown 文档核对；无指纹调用继续走既有正文读取/HTML 哈希回退。保持编码、换行与 front matter 处理语义，路径安全检查仍由现有 opener 承担。核对复用已有读取，不为保存哈希增加磁盘重读。
4. **统一保存与比较。** 上述前提完成后，复用现有稳定指纹选择器：

```text
可以使用语义完整且对应实际正文的稳定指纹：
    保存 H(metadata, bodyFingerprint)

否则：
    保存 H(metadata, 已加载的正文 HTML)
```

具体要求：

- 复用 `IncrementalBuildEngine.TryComputeStableContentHash`。
- 写 manifest 时使用已经加载的 HTML，避免为保存哈希额外读取正文。
- 保留 `LocalizedContentBodyStore`、`ContentMediaOutput` 的稳定快跳限制。
- 模板、路由、元数据、渲染依赖变化继续分别触发失效。
- manifest 结构保持现状，旧 HTML 哈希条目允许一次必要重渲染后自然收敛。
- 旧 Notion 模式缺失指纹也通过一次必要失效自然收敛；切换模式后产生正确输出，下一次稳定构建恢复复用。
- 发现构建期间 Markdown 正文变化而失败是有意的安全语义；在 Markdown 用户指南说明保留旧目标并重试。稳定输入的冷/热构建和现有无指纹调用继续兼容。

### 6.3 修改范围

- `src/Bukit-Core/Bukit.Engine/PageRenderDispatcher.cs`
- `src/Bukit-Core/Bukit.Content/Notion/NotionContentSource.cs`
- `src/Bukit-Core/Bukit.Content/Markdown/MarkdownFolderProvider.cs`
- `src/Bukit-Core/Bukit.Content/Markdown/MarkdownBodyStore.cs`
- `src/Bukit-Core/Bukit.Content/Markdown/MarkdownTextHelper.cs`（复用正文提取/指纹逻辑）
- `tests/Bukit.Engine.Tests/PageRenderDispatcherLazyBodyTests.cs`
- `tests/Bukit.Engine.Tests/RenderPipelineTests.cs`
- `tests/Bukit.Engine.Tests/IncrementalBuildEngineTests.cs`
- `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs`（真实来源模式与事务失败序列）
- `tests/Bukit.Content.Tests/MarkdownFolderProviderTests.cs`
- `tests/Bukit.Content.Tests/MarkdownBodyStoreTests.cs`
- `tests/Bukit.Content.Tests/MarkdownTextHelperTests.cs`
- `tests/Bukit.Content.Notion.Tests/NotionContentSourceTests.cs`
- `guide/user/05-markdown-content.md`
- `guide/user/06-notion-content.md`（正文模式切换与缓存/增量语义）

直接消费者重点核对：`ContentProviderFactory` 的有效 RenderContent、Notion 正文分支、Markdown 元数据/延迟正文及 front matter 提取、`RenderPipeline`、manifest 持久化、事务失败路径、稳定指纹选择及媒体本地化正文包装层。与来源包和 Notion 包共享的 Content 文件进入闭包及最终交点审查；完整专项归属扩大为 Engine、Content、Content.Notion，并包含第 7.5 节 API 专项。

### 6.4 回归测试

核心测试由真实渲染流程生成并保存 manifest；冷/热构建使用允许增量复用的配置。

| ID | 构建序列或场景 | 修复后预期 |
|---|---|---|
| INC-01 | 冷构建 → 首次无变化热构建 | 首次热构建立即复用符合条件的页面 |
| INC-02 | 修改模板 → 再次无变化构建 | 修改时重渲染，下一次立即复用 |
| INC-03 | 修改元数据、正文、路由或依赖 | 正确失效，输出与全量渲染一致 |
| INC-04 | inline HTML 页面 | 同样遵守稳定保存/比较口径 |
| INC-05 | 无稳定指纹的正文 | 正确走 HTML 哈希回退 |
| INC-06 | 媒体本地化开启 | 媒体改写参与结果，避免错误快跳 |
| INC-07 | 旧 manifest 条目 | 至多经过一次转换后稳定复用 |
| INC-08 | 构建失败 | 旧 manifest 和正式输出保持原值 |
| INC-09 | Notion 冷 true → false → 稳定 false → true → 稳定 true；媒体/AutoSummary 关闭，固定元数据 | 有效正文模式双向切换均失效，稳定模式立即复用；每步公开 HTML 与对应全量一致，true/false 分别有/无正文 |
| INC-10 | Markdown 元数据读 A 后、延迟正文读前用屏障替换为 B；构建后恢复 A | A→B 被指纹核对拒绝，正式输出/缓存/manifest 不提交；恢复 A 后生成或复用正确 A，不能残留 B 页面 |
| INC-11 | Markdown A→A；含/不含 front matter、既有换行/Unicode、无捕获指纹调用对照 | producer 与正文核对使用同一 body-only 语义，无误拒绝或额外读取；稳定输入保持首热复用，无指纹回退兼容 |

核心 RED：让冷构建真实产生 manifest，随后执行输入完全相同的第一次热构建，证明页面被不必要地再次渲染。

手工预填稳定 manifest 的测试继续作为局部检查，但不能代替上述冷/热构建回归。

INC-09 要使用真实内置 Notion 来源及受控 HTTP 返回，既检查有效模式指纹变化，也检查 Engine 的连续构建输出；不能仅比较两个手写指纹。INC-10 在现有安全文件读取处设置可控屏障，既验证不一致被检测，也经真实事务证明旧目标保留。实施顺序先补模式指纹与正文核对，再统一保存规则；完整序列中“稳定后立即复用”的 GREEN 在后一步完成后取得，避免以错误复用换取跳过数提升。

### 6.5 验收标准

- 第一次符合条件的热构建即可复用页面。
- 模板或依赖变化后的下一次稳定构建即可恢复复用。
- 页面渲染数、跳过数及渲染原因符合预期。
- 公开 HTML 内容与对应全量渲染一致。
- 连续稳定构建的 manifest 内容哈希稳定。
- Notion 有效正文模式双向切换正确失效，稳定后恢复复用；旧指纹/旧 HTML 哈希均自然收敛。
- Markdown 捕获指纹与实际渲染正文一致；中途修改导致提交前失败，旧输出/缓存/manifest 保持，恢复 A 后不存在持续复用 B。
- 正文读取次数限定在被验证的页面渲染链路；其他内容投影有独立合法读取需求时分别记录。
- Engine、Content、Content.Notion 完整专项及最新 API 对比均有有效记录，用户说明覆盖新增的正文变化失败/重试语义。

## 7. 验证闭包与完整专项命令

### 7.1 专项命令及构建前提

以下命令在仓库根目录运行。本轮只核对语法、工具入口、项目引用图及现有策略；均尚未执行。原五个测试项目和两个 self-test 已有映射，新增 API/OpenSpec 命令须先按第 7.3 节登记，不能把命令列入文档等同于闭包已更新。

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj

dotnet build src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj --configuration Debug --nologo
dotnet run --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj --configuration Debug -- check docs/governance/bukit-core-public-api-baseline.v1.json "$PWD" Debug

python3 -B scripts/checks/core-report-inventory.py --self-test
bash scripts/checks/codex-workflow-self-test.sh

openspec validate fix-core-five-findings --type change --strict --no-interactive
```

| 变更 | 必需专项 |
|---|---|
| 问题 1 | Engine、CLI、Content、Content.Notion（新增内置 Notion 写路径发现）；第 7.5 节 API 专项 |
| 问题 2 | CLI；第 7.5 节 API 专项 |
| 问题 3 | Content、Content.Notion；第 7.5 节 API 专项 |
| 问题 4 | Engine、CLI、Architecture、report inventory self-test；第 7.5 节 API 专项 |
| 问题 5 | Engine、Content、Content.Notion；第 7.5 节 API 专项 |
| 验证策略映射修改 | codex-workflow self-test |
| OpenSpec change 草案 | 指定 change 的 strict validate；运行时专项由业务包的实际源码、测试及正式规范变更触发 |

这些是受影响文件对应的专项验证；完整解决方案、full/release gate 需要单独的明确授权。

本版回归编号为 SRC-01–SRC-18、DEP-01–DEP-18、NOT-01–NOT-16、PRI-01–PRI-16、INC-01–INC-11，共 79 个场景编号，另有 API-01/API-02 两步。编号表示验收场景，参数化用例数和实际测试数以实施记录为准；各包共享命令只按有效闭包执行或复用一次，不按问题数重复执行。

### 7.2 资源分类

只读分类预览及必须修正的调度结果：

- 五个 `dotnet test` 均为 `dotnet-serial`，串行执行。
- 新增的精确 `dotnet build` / `dotnet run` 命令在当前策略中落入默认 `static-parallel`，该默认结果不适合其实际构建资源。准备阶段须登记本文使用绝对根的 API-02 命令及 API-01，并重新 `classify` 确认 `dotnet-serial` 后才能执行；两步还必须按前置依赖顺序运行。
- 两个脚本为 `static-parallel`，仅在实际资源互不冲突时并行。
- 指定 change 的 OpenSpec 文档验证按 `static-parallel` 登记，读取的文档必须处于稳定版本；不与这些文档的写入并行。
- 本方案没有另行安排 fixture 构建命令；若实施范围新增 fixture、锁、manifest 或共享缓存操作，先更新闭包和分类，并按独占要求调度。

源码检索得到的候选消费者需要按真实调用链核实。类型名匹配产生的保守候选不自动扩大实施范围或专项命令集合。

### 7.3 闭包预览的前置项

原代码和测试候选路径已有策略映射；公开 provider 入口的早期预览映射到 Engine/Content 专项。第三轮补充预览覆盖 `WorktreeBuildRun`、Notion 关系/store/source、Markdown producer/store 及相关测试，新增路径对应 CLI、Content、Content.Notion owner。原预览不涵盖本版所有实际变更、新 API 命令和全部 OpenSpec 路径，实施前必须重新生成每个包的实际闭包，特别确认问题 1/5 的 Content.Notion 归属。

当前待补齐的文档包括：

- `guide/user/06-notion-content.md`
- `guide/user/13-deploy-github-pages.md`（第三轮补充预览确认未映射）
- `openspec/specs/in-process-build-transactions/spec.md`
- 第 7.6 节 `openspec/changes/fix-core-five-findings/` 下的 proposal、design、tasks 和五份规范 delta；其中 `specs/i18n-worktree-build/spec.md` 为第三轮新增且已确认未映射。准备阶段逐路径预览，并为实际后续同步的主规范路径登记 owner。
- `guide/user/05-markdown-content.md` 和现行 `openspec/specs/i18n-worktree-build/spec.md` 按新增语义复核已有规则及 owner，不把路径存在视为闭包完整。

把 `scripts/checks/codex-workflow-policy.v1.json` 修正作为独立的首个写入任务。其自身先用现有映射生成闭包、分类，初始化队列并取得 writer slot 后，才补充以下精确规则：

| 路径或命令 | 拟定 owner / 登记要求 |
|---|---|
| Notion 指南 | Content、Content.Notion |
| 部署指南 | CLI、Engine（部署身份、隐私扫描及 strict/warn 行为） |
| 事务主规范 | Engine、CLI；本次正文指纹条目同时覆盖 Content、Content.Notion |
| i18n-worktree 主规范 | Engine、CLI，覆盖原项目来源与正式写目标 |
| change 的 proposal/design/tasks | 指定 change 的 strict validate |
| 事务 delta 草案 | 指定 change 的 strict validate；来源/增量语义 owner 为 Engine、CLI、Content、Content.Notion |
| i18n-worktree delta 草案 | 指定 change 的 strict validate；语义 owner 为 Engine、CLI |
| 部署身份 delta 草案 | 指定 change 的 strict validate；语义 owner 为 CLI |
| 隐私报告 delta 草案 | 指定 change 的 strict validate；语义 owner 为 Engine/CLI 隐私检查与报告契约 |
| Notion 生命周期 delta 草案 | 指定 change 的 strict validate；语义 owner 为 Content、Content.Notion |
| 本任务涉及公共表面的具体源码路径 | 追加第 7.5 节两条 API 前置/检查命令及真实输入闭包，不修改其他任务的广域规则 |
| 第 7.5 节两条精确命令 | API-02 改用 `"$PWD"` 绝对根；同步命令映射、classify 和 cache 命令键，登记 `dotnet-serial`，不沿用旧相对根版本 |

策略修改只运行其直接 owner 的 `codex-workflow-self-test.sh` 并完成一次专项审查。该任务经历 `writing → testing → review_wait → done/blocked` 后，才移交下一任务。

准备阶段的规范 delta 是待实施草案，其命令映射只验证指定 change 的文档契约；不为建立草案提前重跑五个运行时专项。各业务包按其实际代码/测试闭包运行完整 owner 专项，正式规范同步则先登记相应主规范路径和直接 owner 命令。

映射补齐后为 OpenSpec 写入及各业务包分别重新生成闭包，确认 `unmappedFiles=[]`、命令集合完整、资源分类正确，再取得相应任务的 writer slot。不能通过手工删去未映射文件或改写 closure 输出制造通过状态。

### 7.4 证据缓存与记录

- 使用 `codex-workflow.py closure` 生成实际变更文件、直接消费者和公共/序列化契约消费者闭包。
- 使用 `codex-workflow.py classify` 分类闭包路径和精确专项命令。
- 使用 `codex-workflow.py cache record` 记录实际通过的 GREEN 证据。
- 只有 HEAD、闭包内容、精确命令、相关环境变量状态、SDK/工具链版本全部匹配时，`cache check` 命中才可复用。
- 记录环境状态，不保存环境值或秘密。
- 测试证据至少包含精确命令、退出码、结果摘要、实际平台能力、失败原因和证据位置。
- 使用 `metrics add` 记录阶段耗时、缓存结果、重跑原因、写者冲突和完成状态；父任务结束时运行一次 `metrics report`。
- 新变更、失败或证据失效构成重跑理由；通过后不为刷新记录而重复运行相同 fixture。

### 7.5 实时公共 API 对比

Architecture 测试用于类型、依赖和既有基线事实的约束；它不替代当前程序集的完整 API 对比。本专项使用 `tools/Bukit.PublicApiDrift/Program.cs` 的 `check` 入口，按以下顺序执行：

| ID | 精确命令 | 必须证明 |
|---|---|---|
| API-01 | `dotnet build src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj --configuration Debug --nologo` | 构建成功；当前源码对应的全部受管 `Debug/net10.0` 程序集可用 |
| API-02 | `dotnet run --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj --configuration Debug -- check docs/governance/bukit-core-public-api-baseline.v1.json "$PWD" Debug` | 绝对根下完整依赖加载成功，工具退出 0，当前编译表面对既有基线无未批准漂移 |

- 在本次基线的项目引用图中，Core CLI 及其传递引用恰好覆盖基线中的 18 个 Core 程序集，无遗漏及非 Core 项目。实施时重新核对该覆盖；图变化、缺少程序集或配置不一致时，先修正已登记的窄范围构建前提。
- 两条命令均从仓库根运行，`"$PWD"` 必须为该仓库的绝对路径。现有 `ApiAssemblyLoadContext` 对相邻依赖保留输入目录形式，传 `.` 会触发 `LoadFromAssemblyPath` 的绝对路径要求；本方案修正调用参数，未安排加载器重构。隔离依赖加载成功只证明该参数修正方向，完整 API-02 仍须取得实际退出 0 的记录。
- API-02 的 `dotnet run` 只构建对比工具，不能使 Core 输入自动变新；实际重跑对比前必须完成 API-01，且两步之间不得有源码或构建输入写入。禁止以旧 DLL 的成功对比证明新源码兼容。
- 输入闭包须包含基线文件、API 工具源码/项目、18 个受管项目及其编译源码、相关共享构建/包配置和实际 SDK 状态。沿用第 7.4 节缓存约束；证据仍有效时直接复用，失效时才重跑相应序列。
- 两条命令登记到涉及公共表面的任务闭包，串行执行；记录各自退出码、配置、覆盖程序集清单与证据位置。最终审查复用有效结果，只对后续变更导致的失效部分补证。
- `scripts/checks/public-api-drift.sh` 会构建 `bukit-core.slnx`；本计划采用上述 Core CLI 前置构建和直接工具调用，不把 wrapper 当作同范围替代。
- 保留原公共基线作为比较对象。未经独立契约决策，不得通过更新基线消除本次修复造成的意外漂移。

### 7.6 OpenSpec 承载与唯一任务清单

拟定 change ID：`fix-core-five-findings`。这是准备阶段要建立的承载，不表示当前仓库已创建或批准该 change；若同名 change 已存在，先核对其内容和状态再使用。

| 文件 | 内容 |
|---|---|
| `openspec/changes/fix-core-five-findings/proposal.md` | 五项 Core 修复范围、用户批准边界、兼容性收紧及来源可见性限制 |
| `openspec/changes/fix-core-five-findings/design.md` | 本文修订后的设计、资源所有权、验证闭包和 API 构建前提 |
| `openspec/changes/fix-core-five-findings/tasks.md` | 唯一实施清单；承接第 8 节编排，按实际证据勾选 |
| `openspec/changes/fix-core-five-findings/specs/in-process-build-transactions/spec.md` | 已知真实来源/内置缓存写路径、公开入口覆盖，Notion 有效正文模式、Markdown 指纹/正文一致性及冷/热增量场景 |
| `openspec/changes/fix-core-five-findings/specs/i18n-worktree-build/spec.md` | 原项目来源与正式写目标在锁/暂存/worker 前预检；失败不交换目录或删除原稿备份 |
| `openspec/changes/fix-core-five-findings/specs/core-deployment-identity/spec.md` | 新增 Core 部署身份规范：local/worktree 定址，先建临时仓库/私有 remote 后在相同有效配置中前检，条件 include、fetch/checkout、环境隔离及 dry-run 契约 |
| `openspec/changes/fix-core-five-findings/specs/core-report-contracts/spec.md` | JSON 解析/字符串解码完成性、strict/warn 消费路径、报告 schema 保持 |
| `openspec/changes/fix-core-five-findings/specs/notion-load-resource-lifecycle/spec.md` | 新增加载级限速、正文并发、全退出路径任务收束、无人等待时失败任务清除、Composite 最终构造清理及空正文预取复用规范 |

每个 requirement 使用 SHALL/MUST，至少包含一个 WHEN/THEN Scenario，并关联本文回归编号。文档写入前完成路径映射、闭包和 writer acquire；写入后执行唯一指定的文档验证：

```bash
openspec validate fix-core-five-findings --type change --strict --no-interactive
```

通过标准是该 change 验证退出 0，且人工核对五项需求、首轮审计 1–6、复审 1–2 和第三轮 1–11 均有落点；第三轮 8/11 分别关联验收命令与用户文档任务，不虚构运行时 requirement。该结果不等于功能测试通过。准备阶段的队列/策略任务证据在 `tasks.md` 建立后回填引用，只有已取得证据的事项可以勾选。现行规范保持权威，delta 的同步和归档须在实现证据齐备后按已批准任务执行。

## 8. 实施编排模板

本节是迁入 `openspec/changes/fix-core-five-findings/tasks.md` 的编排模板，不另行维护勾选状态。实施进度只在该 `tasks.md` 记录；本方案保存设计和验收要求。

### 8.1 准备阶段

1. 只读核对 HEAD、工作树和治理约束，确定实际候选文件、消费者、公共/序列化契约，以及第 7.6 节 change 路径。
2. 在仓库外唯一状态目录执行 `python3 scripts/checks/codex-workflow.py queue init --state <queue.json>`，登记策略准备、契约文档、三个修复包和最终审查的任务标识、单写者及唯一报告位置。
3. 仅以 `scripts/checks/codex-workflow-policy.v1.json` 为首个策略任务的变更输入，用现有策略执行 `closure` / `classify`，确认该任务自身已映射，owner 命令为 `bash scripts/checks/codex-workflow-self-test.sh`。
4. 执行 `queue acquire --state <queue.json> --task prepare-policy` 后，才写入第 7.3 节的路径、API 命令及资源分类规则。这里以及下文的 queue 子命令均通过 `python3 scripts/checks/codex-workflow.py` 调用。
5. 策略任务转入 `testing`，执行 owner self-test；转入 `review_wait` 完成一次专项审查并记录证据，最后转为 `done` 或 `blocked`。阻塞时不进入依赖该策略的后续写入。
6. 为第 7.6 节实际 OpenSpec 文件生成闭包并分类，确认 `unmappedFiles=[]`；契约任务 acquire 后创建 proposal/design/tasks/delta，将已完成准备事项的证据链接回填至唯一 `tasks.md`。
7. 执行指定 change 的 strict validate，核对五项修复、首轮六项、复审两项及第三轮十一项修订、兼容性决策均有对应 requirement/Scenario/任务承载；按队列状态完成该文档任务。
8. 为每个业务包重新生成实际闭包并分类。确认部署指南、i18n delta 及其他未映射路径均已解决，问题 1/5 新增 Content.Notion owner 已纳入；API-02 使用绝对根，两条 API 命令均为 `dotnet-serial`，然后由当前实现任务 acquire 后开始 RED/实现。

### 8.2 安全修复包：问题 1、2、4

1. 添加配置来源冲突、公开 provider 正反例、真实多语言 CLI 原项目保护及内置 Notion 可写缓存发现，覆盖 SRC-01–SRC-18。
2. 添加跨项目部署、真实 Git 继承环境、作用域 origin、私有 remote、条件 include 与 URL 重写回归，覆盖 DEP-01–DEP-18。
3. 添加 JSON 转义、深度边界、输入解码异常及扫描未完成的 strict/warn/deploy 路径，覆盖 PRI-01–PRI-16。
4. 运行已登记完整专项，取得原漏洞的目标断言失败；合法调用等正向保护场景分别记录，不能伪造 RED。
5. 实施真实根/内置可写缓存发现与传递、共享冲突检查、worktree 原项目正式目标预检及 clean 全目标预检。
6. 实施项目根目录传递、作用域 origin、临时仓库初始化/私有 remote 配置后的同上下文前检、fetch/checkout 及共享 Git 环境隔离；同步 fake git 和旧 clone 命令断言。
7. 实施共享 JSON 解码、窄边界输入解码异常归类及完成性结果，接入两侧失败映射。
8. 更新 Markdown/Notion 来源说明、部署指南的 Git/JSON 迁移说明和事务/i18n/部署身份/报告 delta，运行实际闭包的完整专项（含新增 Content.Notion）、API-01/API-02 和已登记文档验证，记录 GREEN。
9. 完成一次专项审查，关闭 Critical / Important，按队列状态和唯一 `tasks.md` 移交任务。

### 8.3 Notion 稳定性包：问题 3

1. 添加来源级限速、正文并发、客户端释放、分类关系混合缓存取消、唯一等待者取消后延迟失败、Composite 最终构造失败及空正文预取回归，覆盖 NOT-01–NOT-16 并取得对应 RED。
2. 实施加载级共享客户端、正文并发门、body store 所有权、关系任务全退出路径收束及按任务身份清除失败缓存。
3. 接入加载结果绑定的关系 resolver，将 Composite 最终构造纳入清理，保存成功的空正文预取；更新 Notion 限速/并发说明及对应 delta。
4. 运行 Content、Content.Notion 完整专项；按实际闭包执行 API 专项和已登记文档验证。
5. 完成一次专项审查，关闭 Critical / Important，记录证据并移交。

### 8.4 增量一致性包：问题 5

1. 添加真实冷构建→首次热构建 RED、模板变化恢复复用、旧 manifest 兼容、Notion 模式切换及 Markdown A→B 时序/事务回归，覆盖 INC-01–INC-11。
2. 先补 Notion 有效 RenderContent 指纹和 Markdown 同次安全读取的 body-only 指纹核对，确认模式指纹变化及 A→B 拒绝的断言；完整连续序列待下一步统一保存规则后验收。
3. 再统一 manifest 保存/比较口径，验证 inline HTML、无指纹回退、媒体限制和旧条目自然收敛；保持正文读取次数约束。
4. 更新 Markdown 来源变化重试说明、Notion 模式/增量说明与事务 delta，运行 Engine、Content、Content.Notion 完整专项、API-01/API-02 及已登记文档验证。
5. 完成一次专项审查，关闭 Critical / Important，记录跨包共享文件引起的证据失效和重新取得的结果，再移交。

### 8.5 最终统一审查

1. 生成一次 `review-scope`，检查原项目/worktree/注入缓存的来源保护、事务/指纹/manifest、Git 条件配置/实际传输/隐私阻断及 Notion 生命周期/空正文/增量模式的跨包交点。
2. 检查公共 API、旧 manifest、未覆盖变更、唯一 `tasks.md` 的证据链接及失效情况；尤其核对后续 Engine/Content 修改是否使先前 API 证据失效。
3. 复用有效证据，只对失效部分运行已登记的专项和必要构建前提。
4. 对照第 1.2 节确认第三轮 1–11 均有实际交付证据，关闭全部 Critical / Important，记录新 Minor；本版已纳入的 NOT-15/16 和部署指南仍是交付范围，任何必需证明缺失时保留未完成状态。
5. 汇总一次 metrics report，交付第 10 节证据索引与明确范围内的验收结论。

## 9. 执行与审查约束

- 同一时刻只有一个实现写者修改仓库文件。
- `openspec/changes/fix-core-five-findings/tasks.md` 是唯一实施清单；本方案的编排模板不作为第二套完成记录。
- 多任务执行先创建队列；写者持有任务贯穿 `writing`、`testing`、`review_wait`，进入 `done` 或 `blocked` 后再移交。
- 策略映射、OpenSpec 文档和测试文件都是仓库写入，均须满足各自闭包已映射、资源已分类及 writer acquire 前置条件。
- 每个普通实施任务在专项完成后进行一次专项审查。
- 仅因 Critical / Important 发现返回实现及对应范围复审；Minor 记录后继续。
- 父任务完成时进行一次 delta-only 统一审查，关注交点、失效证据、未覆盖变更和未关闭的重要问题。
- 任何新增或调整的测试命令先登记到实际任务闭包。
- 环境、权限或工具失败按具体阻塞项记录，不作为修改无关业务代码的依据。
- 若所需平台或证明不可用，明确标记未验证的场景，避免将跳过或阻塞计为通过。

## 10. 完成标准与交付证据

每项修复需同时具备：

1. 能证明原问题的失败回归。
2. 修复后的完整专项通过记录。
3. 用户可观察行为的断言。
4. 异常、取消或兼容性边界证据。
5. 无未关闭的 Critical / Important 发现。
6. 最新公共 API 对比及必需契约验证有效，唯一 `tasks.md` 可以追溯到实际命令与证据。

| 问题 | 最终必须交付的直接证据 |
|---|---|
| 1 | 配置/公开 provider/worktree 原项目冲突均前置拒绝；worker 未启动；内置 Notion 可写缓存不侵入已知来源根；内存 provider 与 readonly/off/独立缓存无误拒绝；原稿、旧输出和缓存保持；隐藏来源边界已声明 |
| 2 | local/worktree origin 与私有 remote 实际传输目标一致；初始化和 remote 配置后的 hasconfig/gitdir 重写在首次传输前拒绝，错误目标访问为零；分支/浅获取/keep-history/force/认证兼容；源仓库 refs/工作树/索引保持，指南说明迁移 |
| 3 | 连续许可、正文并发峰值及请求计数；关系调度退出等待已启动任务后释放；无人等待的延迟失败不污染下一次读取；Composite 最终构造失败恰好一次清理，空正文预取无重复请求 |
| 4 | 实际 .NET 解码扫描命中；超深/畸形 JSON 和未配对代理项均判未完成；strict 事务阻断、warn 完成且报告失败、部署 add/commit/push 前返回隐私验证错误；合法配对、误报及 schema 回归通过 |
| 5 | 真实首次热构建复用；Notion 模式切换正确失效并恢复复用；Markdown 指纹对应实际正文，A→B 拒绝提交且恢复 A 后无旧 B；输出等价、旧 manifest 自然收敛；Engine/Content/Content.Notion 专项有效 |
| 跨项契约 | API-01/API-02 的新鲜性、绝对根加载及无漂移记录；五份 OpenSpec delta 验证、部署/来源用户说明、唯一任务证据索引、队列状态及最终 delta-only 审查结果 |

主要兼容性与范围边界：

- 来源根目录保护会拒绝 `dir: "."` 与项目内写入目录共存的配置；未知自定义 provider 的隐藏磁盘根不在可识别来源保证内，需按第 2.3 节说明。
- 部署身份来自项目 local/worktree 原始 origin；全局 origin 兜底、依赖 URL 重写的别名和匹配 canonical URL 的重写规则不再接受。正常 CWD、配置路径、linked worktree 和不改变目标的认证配置保留。
- 无法完成解析或输入字符串解码的公开 JSON 将导致检查失败；strict 构建和部署拒绝，warn 完成并报告失败。合法 Unicode 代理项配对继续允许。
- Markdown 元数据读取与延迟正文读取不一致时，构建在提交前失败并保留旧目标，用户需稳定输入后重试；Notion 模式切换及旧指纹迁移可触发一次必要重渲染。

当前状态：首轮审计 1–6、复审 1–2 及第三轮 1–11 的修订均已纳入本文；仓库策略、OpenSpec change、代码修复、完整专项和运行时验收仍待实施。本次文档一致性检查、静态推导及隔离运行时探针是不同层级的证据，均不足以单独支持“五项修复均已闭合”的结论。
