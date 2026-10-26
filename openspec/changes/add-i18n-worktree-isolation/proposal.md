## Why

Bukit 已能在一个进程中按 `build.languageJobs` 构建语言变体，但语言任务共享进程、工作目录和失败边界。显式 CLI 构建需要固定到同一 Git HEAD，并保证任一语言失败时不改动正式输出。

## What Changes

- 显式 `bukit build` 在 `site.languages` 非空时自动要求干净、完整跟踪的 Git 工作树，并为每种语言创建同一 HEAD 的 detached Worktree 子进程。
- 父进程只加载一次内容，Worker 复用版本化临时快照，并将可重建 `BuildVariantResult` 的内部结果交回父进程。
- 父进程继续使用现有根级多语言 writer；所有语言、报告和安全检查成功后，才以可恢复目录交换提交输出和 manifest。
- `build.languageJobs` 成为语言 Worker 并发上限；`--jobs` 仍只控制 Worker 内部页面渲染。
- 更新正式 i18n/故障排查文档并删除过时的 Worktree 提案。

## Capabilities

### New Capabilities

- `i18n-worktree-build`: 定义 Git 快照、语言 Worker、结果交换、完整聚合、输出提交和清理恢复合同。

### Modified Capabilities

无。

## Impact

- 这是显式 `bukit build` 的 breaking change：多语言站点不在 Git 中、工作树不干净或必需本地输入未跟踪时将失败。
- `bukit dev`、`bukit deploy` 内部构建及直接 `SiteEngine` 调用保持现有同进程行为。
- 不新增公开 CLI 选项、配置字段、NuGet 包、分支、远程操作或安全沙箱能力。
