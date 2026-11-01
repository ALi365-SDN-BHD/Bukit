# i18n-worktree-build Specification

## Purpose
为显式 Bukit CLI 多语言构建提供同一 Git 快照的进程级 Worktree 隔离、完整输出等价和失败不污染正式产物的合同。

## Requirements

### Requirement: 显式多语言 CLI 构建必须自动使用 Git Worktree

系统 SHALL 在显式 `bukit build` 的规范化 `site.languages` 非空时，为每个语言使用同一 HEAD 的 detached Git Worktree 和独立 Worker 进程。`dev`、`deploy` 内部构建和直接 `SiteEngine` 调用 MUST 保持现有进程内行为。

#### Scenario: 显式多语言构建
- **WHEN** 用户在干净 Git 站点执行 `bukit build` 且配置至少一个 `site.languages` 值
- **THEN** 每个语言由同一 HEAD 的独立 Worktree Worker 构建，并在父进程聚合

#### Scenario: 非目标入口
- **WHEN** 多语言配置由 `dev`、`deploy` 或嵌入式 `SiteEngine` 使用
- **THEN** 系统不创建 Worktree，保持既有进程内语义

### Requirement: 源码和本地构建输入必须来自干净 HEAD

系统 MUST 在创建 Worker 前确认 Git 可用、站点位于仓库内、HEAD 可解析、staged/unstaged/untracked 状态为空，并确认配置和实际必需本地输入位于仓库内且已跟踪。任一条件不满足 MUST 失败且不得回退。

#### Scenario: 工作树不干净
- **WHEN** staged、unstaged 或 untracked 内容存在
- **THEN** 构建以稳定诊断失败，正式输出和既有 Worktree 不变

#### Scenario: 必需输入未跟踪
- **WHEN** 配置、内容、主题、静态资源或本地插件所需文件被忽略、未跟踪或位于仓库外
- **THEN** 构建在 Worker 启动前失败并指出输入身份

### Requirement: 所有语言必须消费同一内容快照

父进程 SHALL 只执行一次内容获取和规范化，并 SHALL 通过有版本、带 hash、路径受限的临时快照向 Worker 提供文档和正文。Worker MUST NOT 各自重新读取远程内容源。

#### Scenario: 远程内容源
- **WHEN** 多语言站点使用 Notion 或其他远程内容源
- **THEN** 远程获取只发生一次，所有语言结果记录相同内容快照 hash

### Requirement: Worker 结果必须完整且可验证

每个 Worker SHALL 返回有版本、AOT 可序列化的结果，足以重建现有 `BuildVariantResult`。父进程 MUST 验证 schema、HEAD、配置/内容 hash、语言全集、重复语言、输出路径和完成状态，拒绝任何不一致或越界结果。

#### Scenario: 结果损坏或错配
- **WHEN** Worker 结果损坏、来自不同 HEAD/hash、遗漏或重复语言，或引用 staging 外路径
- **THEN** 父进程拒绝聚合、取消其余 Worker，并保持正式输出不变

### Requirement: 根级输出必须保持现有语义

父进程 SHALL 从验证后的 Worker 结果重建现有变体模型，并 MUST 复用现有 sitemap、feed、search、robots、agent manifest、llms、报告和安全门禁实现。split、index 和 merged 模式的公开输出除非本来包含非确定时间指标，否则 MUST 与同一输入的进程内构建等价。

#### Scenario: 完整多语言投影
- **WHEN** 所有 Worker 成功且站点启用任意受支持的根级输出模式
- **THEN** 聚合结果与现有进程内构建具有相同公开文件、路由、内容和索引语义

### Requirement: 失败不得提交部分输出

系统 MUST 在正式输出同文件系统的 staging 中完成全部 Worker、聚合、报告、安全门禁、完成状态和 manifest。只有完整成功后才可提交；Worker、协议、聚合、门禁或目录交换失败 MUST 返回非零并恢复原输出和 manifest。

#### Scenario: 一个语言失败
- **WHEN** 任一语言 Worker 失败或被取消
- **THEN** 其他 Worker 被取消，部分语言不得发布，正式输出和 manifest 保持原值

#### Scenario: 输出交换失败
- **WHEN** staging 提交在正式输出移动后失败
- **THEN** 系统尝试从受控 backup 恢复，并以清晰诊断保留无法自动恢复的受控路径

### Requirement: Worktree 清理必须有精确所有权

系统 SHALL 只删除当前运行登记且规范化路径位于该运行临时根下的 Worktree，不得创建分支、修改远程/config/hook 或执行全局 prune。成功、失败和取消 SHALL 清理本次 Worktree；崩溃残留只能由后续运行在再次验证所有权后定向恢复。

#### Scenario: 未知 Worktree 同时存在
- **WHEN** 仓库已有用户或其他任务 Worktree
- **THEN** 多语言构建不得修改、删除或重新登记这些 Worktree

### Requirement: 两级并发语义必须保持明确

`build.languageJobs` SHALL 限制同时运行的语言 Worker 数量，最小为一且不超过处理器数；`--jobs` SHALL 继续只控制每个 Worker 内部页面渲染并发。

#### Scenario: 限制语言 Worker
- **WHEN** 语言数大于 `languageJobs`
- **THEN** 同时活动的语言 Worker 不超过该上限，且最终语言顺序和输出保持确定

### Requirement: Preflight original-project sources before worktree side effects

显式多语言 CLI Worktree 构建 MUST 在取得运行锁、创建运行目录、准备暂存、登记或创建 Worktree、启动 Worker 之前，以应用覆盖后的配置将原项目真实本地来源根与正式输出、缓存、媒体下载、外部 metrics JSON 及伴随 HTML、配置的 Notion `readwrite` 缓存执行来源/写目标冲突预检。预检 MUST 使用原项目路径语义，不得以映射到 detached Worktree 的输入副本代替原项目来源。后续既有 Worktree 输入校验 MUST 保留，但 MUST NOT 被当作本次来源保护的替代。

#### Scenario: Original source equals the formal output (SRC-14)
- **WHEN** 原项目 Markdown 来源目录与正式输出目录相同，首次 no-clean 构建或正式输出含旧 marker
- **THEN** 构建在运行锁、暂存、Worktree 和 worker 创建前以稳定诊断失败

#### Scenario: No exchange or backup deletion on conflict (SRC-14)
- **WHEN** 来源预检拒绝显式多语言构建
- **THEN** 系统不交换输出/缓存目录、不删除备份、不修改原稿，原稿和旧正式目标保持原值

### Requirement: Preflight covers every formal managed target

预检 MUST 同时覆盖正式输出、正式缓存、媒体下载目录、外部 metrics 文件及其伴随 HTML，以及配置中 `readwrite` 且 `CacheDir` 非空的 Notion 缓存；`readonly`/`off` 或空目录 MUST NOT 虚构写目标。路径比较 MUST 使用与进程内事务相同的规范化、符号链接和大小写语义。

#### Scenario: Cache, media or metrics conflict (SRC-15)
- **WHEN** 原项目来源与正式缓存、媒体下载或 metrics/伴随 HTML 双向包含
- **THEN** 冲突以原项目路径判定并在任何目标写入或 worker 启动前拒绝

#### Scenario: Independent safe targets (SRC-15)
- **WHEN** 原项目来源与全部正式目标互不重叠
- **THEN** 多语言构建正常完成，原稿与正式目标语义保持

### Requirement: Verified scope covers final exchange

通过预检后 MUST 保持最终目录交换和备份清除处于已验证的正式目标范围内；交换 MUST 只使用本次运行为这些正式目标创建的暂存和备份路径。预检失败 MUST NOT 进入 `WorktreeBuildTransaction` 的交换或恢复路径。

#### Scenario: Exchange still targets the verified paths
- **WHEN** 预检通过且构建成功
- **THEN** 最终交换只使用预检覆盖的正式输出和缓存路径及本次运行备份

#### Scenario: Preflight failure skips exchange
- **WHEN** 原项目来源预检失败
- **THEN** 不创建暂存交换，不进入提交或恢复流程，正式目标保持原值
