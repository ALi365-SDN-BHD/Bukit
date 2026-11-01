## ADDED Requirements

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
