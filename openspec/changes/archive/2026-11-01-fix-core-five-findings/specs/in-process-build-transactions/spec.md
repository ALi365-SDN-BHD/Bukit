## ADDED Requirements

### Requirement: Protect recognizable local content sources from managed writes

系统 MUST 在创建暂存目录、写入输出标记、创建缓存或加载内容之前，将可识别的本地内容源根与全部受管写目标比较：输出、默认和自定义缓存、媒体下载、Notion 可写缓存、外部 metrics JSON 及其伴随 HTML。比较 MUST 使用现有路径规范化、符号链接解析和实际文件系统大小写语义，并 MUST 拒绝相等及父子目录双向包含。来源根 MUST 按整个来源目录保护，`includePaths`/`includeGlobs` 不缩小保护范围。发现冲突 MUST 使用 `BuildOutputUnsafe` 诊断在首次写入或删除前失败，原稿清单与哈希、旧输出、缓存和 marker 保持原值。

#### Scenario: Configured source equals output on a first no-clean build (SRC-01, SRC-05)
- **WHEN** 配置的 Markdown 来源与输出相同，首次以 `clean:false` 构建，且目录为空或含旧 marker
- **THEN** 构建在写 marker、暂存和缓存之前失败，来源文件不变

#### Scenario: Containment, aliases and similar names (SRC-02, SRC-06, SRC-07)
- **WHEN** 输出是来源父目录或子目录，或来源经符号链接、大小写别名、尚未创建的末级目录指向写目标；或目录名为 `content` 与 `content-old`
- **THEN** 双向包含与真实路径冲突被拒绝，仅字符串前缀相同不误判

#### Scenario: Cache, metrics and multi-source targets (SRC-03, SRC-04, SRC-08)
- **WHEN** 来源位于 `.cache`、自定义缓存或 metrics 目标内，或存在多个独立安全来源目录
- **THEN** clean 在删除输出前拒绝且不部分清理；冲突配置在同一共享检查下失败；独立安全来源正常构建和清理

#### Scenario: Whole-root contract for the project root (SRC-09)
- **WHEN** `markdown.dir` 为 `"."` 且配合精确的 `includePaths`/`includeGlobs`
- **THEN** 与项目内输出或缓存共存时按根目录保护契约拒绝，并给出收窄来源目录的迁移提示

### Requirement: Cover injected built-in providers without guessing hidden roots

公开 `SiteEngine.BuildAsync(IContentProvider, BuildOptions)` MUST 从注入的内置 `MarkdownFolderProvider` 读取真实 `ContentDir`，递归检查内置 `CompositeContentProvider` 子来源，并收集内置 `NotionContentSource` 的真实 `readwrite` 可写缓存；这些信息 MUST 经内部参数传到事务预检。合成默认 `content` 来源 MUST NOT 被当作注入 provider 的磁盘事实；显式空来源集合 MUST NOT 回退到合成来源。自定义或不透明 provider 的隐藏磁盘根 MUST NOT 被猜测或一律拒绝，其保护范围 MUST 在用户说明和验收结论中明确声明。注入的内置 Notion `readonly`/`off` 或空缓存目录 MUST NOT 被当作写目标。

#### Scenario: In-memory provider with a content-named output (SRC-10)
- **WHEN** 公开入口接收内存 provider，输出目录名为 `content`
- **THEN** 构建正常进行，不把合成默认 Markdown 来源当作真实输入

#### Scenario: Injected Markdown provider conflicts (SRC-11)
- **WHEN** 注入 Markdown provider 的真实根与输出均为 `articles`，`clean:false`
- **THEN** 在 marker、暂存及缓存写入前拒绝，原稿哈希不变

#### Scenario: Nested composite and writable Notion cache (SRC-12, SRC-16, SRC-18)
- **WHEN** 多层 Composite 中的 Markdown 子来源与输出或缓存重叠；或注入内置 Notion `readwrite` 缓存与 Markdown 根相等、包含或被包含（含相对路径和路径别名）
- **THEN** 按 provider 实际进程语义解析并识别真实路径冲突，在加载和创建 `pages`/`relations`、缓存 JSON、临时或锁文件之前拒绝，既存哨兵不变

#### Scenario: Safe injected combinations (SRC-13, SRC-17)
- **WHEN** 使用不透明自定义 provider 且无已知本地输入，或使用 Notion `readonly`/`off`、空 `CacheDir`、独立 `readwrite` 目录
- **THEN** 不误拒绝，安全组合成功并保持实际缓存语义

### Requirement: Protect the original project in the explicit multilingual worktree entry

显式多语言 CLI MUST 在取得运行锁、创建运行目录、暂存或启动 Worker 之前，以应用覆盖后的配置、原项目真实来源根和正式输出/缓存/媒体/metrics/Notion 可写缓存目标执行同一预检。映射后的 Worktree 输入副本 MUST NOT 代替原项目来源。最终目录交换及备份清除 MUST 处于已验证范围内。失败 MUST NOT 交换目录、删除备份或修改原稿。

#### Scenario: Original source equals formal output (SRC-14)
- **WHEN** 显式多语言 CLI 的原项目来源等于正式输出，首次 no-clean 或存在旧 marker
- **THEN** 在锁、暂存和 worker 创建前拒绝，不交换目录或删除备份，原稿和旧目标不变

#### Scenario: Original source conflicts with cache, media or metrics (SRC-15)
- **WHEN** 原项目来源与正式缓存、媒体或 metrics/伴随 HTML 冲突，或与独立安全目录对照
- **THEN** 以原项目路径判定冲突，不被 detached worktree 副本掩盖；冲突时 worker 未启动，安全构建成功

### Requirement: Notion body fingerprints reflect the effective content mode

内置 Notion 的稳定 `bodyFingerprint` MUST 包含有明确分隔的有效 `RenderContent` 布尔标记，并使用已合并来源覆盖与默认值的 options。切换正文模式 MUST 使增量输入失效并产生与全量渲染一致的公开 HTML；稳定模式下一次后续热构建 MUST 立即复用。manifest 结构 MUST 保持现状。

#### Scenario: Bidirectional content-mode switch (INC-09)
- **WHEN** Notion 冷构建从 `RenderContent=true` 切到 `false`，稳定后切回 `true`
- **THEN** 每次模式变化均失效并重渲染，稳定模式立即复用，公开 HTML 分别有/无正文且与全量渲染一致

### Requirement: Markdown body verification matches the rendered body

Markdown producer 与正文读取 MUST 共用一个 body-only（剥离 front matter）SHA-256 指纹实现。正文读取 MUST 在安全 opener 已读取的同一字符串上核对捕获指纹，并使用该次已核对正文渲染 HTML。检测到变化 MUST 返回明确的来源变化失败，在返回正文、更新 manifest 或事务提交之前停止，保留旧输出和缓存；诊断 MUST 不输出正文内容。仅对携带捕获指纹的文档核对；无指纹调用 MUST 保持既有读取/HTML 哈希回退与编码、换行语义。

#### Scenario: Metadata read A, delayed body read B (INC-10)
- **WHEN** 元数据读取 A 后、延迟正文读取前文件被替换为 B，随后恢复 A
- **THEN** A→B 被指纹核对拒绝，正式输出、缓存和 manifest 不提交；恢复 A 后生成或复用正确 A，不残留 B 页面

#### Scenario: Stable Markdown inputs and legacy callers (INC-11)
- **WHEN** 输入为 A→A，含或不含 front matter、既有换行与 Unicode；或调用方不提供捕获指纹
- **THEN** producer 与正文核对使用同一 body-only 语义，无误拒绝或额外磁盘重读；稳定输入首次热构建即复用，无指纹回退兼容

### Requirement: Manifest save and comparison share one hash definition

`PageRenderDispatcher` MUST 复用现有稳定指纹选择器：可使用语义完整且对应实际正文的指纹时保存 `H(metadata, bodyFingerprint)`，否则保存 `H(metadata, 已加载正文 HTML)`。保存 MUST 使用已经加载的 HTML，避免为哈希额外读取正文。`LocalizedContentBodyStore`、`ContentMediaOutput` 的稳定快跳限制、模板/路由/元数据/渲染依赖失效 MUST 保持。旧 HTML 哈希或旧 Notion 模式条目 MAY 经一次必要重渲染自然收敛。

#### Scenario: Cold build then identical hot build (INC-01)
- **WHEN** 真实冷构建生成 manifest 后，以完全相同输入执行第一次热构建
- **THEN** 符合条件的页面在第一次热构建即被复用

#### Scenario: Invalidation and recovery (INC-02, INC-03, INC-07)
- **WHEN** 模板、元数据、正文、路由或渲染依赖变化后再次无变化构建
- **THEN** 变化时正确重渲染，下一次稳定构建立即复用；旧条目至多一次转换后稳定

#### Scenario: Fallbacks and transaction failure (INC-04, INC-05, INC-06, INC-08)
- **WHEN** inline HTML 页面、无稳定指纹正文、媒体本地化开启，或构建失败
- **THEN** inline HTML 遵守同一口径，无指纹走 HTML 哈希回退，媒体改写参与结果；失败时旧 manifest 与正式输出保持原值
