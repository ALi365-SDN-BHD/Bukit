## Context

`SiteEngine` 当前先加载内容和准备主题资源，再通过 `Parallel.ForEachAsync` 执行 `VariantBuildPipeline`，最后让 `I18nOutputMerger` 消费进程内 `BuildVariantResult`。根级 feed、search、llms、agent manifest 和 sitemap 需要路由、SEO、内容图和正文，不能可靠地只合并生成文件。

## Goals / Non-Goals

**Goals:**

- 每个语言在同一已提交 HEAD 的 detached Worktree 和独立进程中构建。
- 远程内容只获取一次，所有 Worker 消费同一内容快照。
- 完整复用现有变体管线和根级 writer，保持 split/index/merged 语义。
- 任一前置步骤失败时正式输出和 manifest 保持不变。

**Non-Goals:**

- 不改变 `SiteEngine` 公共 API、`dev`、`deploy`、单语言构建或插件安全边界。
- 不实现多主题矩阵、dry-run、分支管理或性能保证。

## Decisions

### 1. CLI 编排，Engine 保持可嵌入

只有顶层显式 build handler 启动 Worktree 编排。Deploy 调用专用的现有进程内构建入口，直接 `SiteEngine` 调用不感知 Git。

### 2. 一次内容快照和版本化 Worker 结果

父进程按现有内容管线加载并规范化一次内容，将文档清单和逐正文文件写入权限受限的运行目录。Worker 结果使用 AOT source-generated JSON，包含重建 `BuildVariantResult` 所需的可序列化字段、输出计划、派生内容和正文；协议记录 schema、HEAD、语言及配置/内容 hash 并由父进程严格验证。

### 3. Detached Worktree 和最小 Git 副作用

Worker Worktree 位于系统临时运行目录，以 `git worktree add --detach` 创建。实现不创建或修改分支、远程、hook 或 Git config，不执行全局 prune，只删除本次登记且路径位于运行目录内的 Worktree。

### 4. Staging 后统一聚合与提交

Worker 写入正式输出同文件系统的 sibling staging。父进程重建变体结果后调用现有根级 writer、报告和安全门禁。完成状态与 manifest 写入 staging 后，通过带 backup 的目录交换提交；任一交换失败时恢复原输出，保留可诊断状态并返回失败。

### 5. 不把 Worktree 宣称为沙箱或加速器

Worker 为保持现有内容和插件语义继承必要环境。Worktree 只隔离 Git 工作目录和进程故障；不限制网络、凭据或插件能力。并行度继续由 `languageJobs` 限制。

## Risks / Trade-offs

- 内容和 Worker 结果快照增加临时磁盘使用量；通过逐正文文件、运行目录边界和成功/失败清理控制。
- 输出与 cache 无法由文件系统提供跨目录事务；通过同卷 staging、backup 和 manifest 回滚把失败窗口收敛为可恢复状态。
- 非 Git 或本地未跟踪输入的多语言站点不再兼容；这是已批准的失败关闭合同。
- 子进程日志和取消可能死锁；复用有界 stdout/stderr 收集和进程树终止能力。

## Migration Plan

1. 先以现有同进程输出作为等价 oracle，实现内部快照和单 Worker 回环。
2. 接入 detached Worktree、多 Worker 限流、聚合和 staging 提交。
3. 完成 Engine、CLI、文档和当前平台 Native AOT 专项证据。
4. 提交、推送、发布和远端跨平台 CI 均保持独立授权。
