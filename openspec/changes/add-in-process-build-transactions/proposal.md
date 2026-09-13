## Why

Bukit 当前定位为企业内部稳定的可信内容发布引擎。用户已明确语言隔离不是必需能力；现有进程内构建已经支持一次内容获取和语言并发。真正需要补齐的是构建失败时正式输出与受管缓存的保护，以及竞争构建的资源互斥。

本提案基于 main `4e6f80c05de8105e638780241a2af0b51a568bb7`，不以隔离分支作为实现基线。

## What Changes

- 在公共 Engine 构建路径引入 staging、资源锁、协调提交与异常回滚，覆盖单语言及多语言。
- 保留一次内容获取、进程内语言并发、页面并发、增量渲染和现有输出语义。
- 保持正式路径身份、输出安全检查和所有权清理；报告、安全门禁、manifest 与完成状态在提交前完成。
- 对 dev 的监听、刷新及 deploy 内部构建明确同一保护语义。
- 不增加 Git 前置条件、Worker 协议、语言隔离开关或新配置。

## Capabilities

### New Capabilities

- `in-process-build-transactions`: 进程内构建的资源互斥、输出事务、失败保护和入口一致性。

### Modified Capabilities

无。main 的正式 specs 中不存在 `i18n-worktree-build`，因此不伪造对该正式能力的 REMOVED delta。

## Supersession and Approval

用户已确认“语言隔离并非必须”，并要求基于 main 整理替代提案。本提案是待审阅的具体合同；实现任务尚未开始，提案生成或校验通过不表示实现完成。

本提案获准实施后，取代 `codex/i18n-worktree-isolation`（审阅 HEAD `93b1d77f`）中的 `add-i18n-worktree-isolation` 作为本次交付目标：

| 原隔离要求 | 替代决定 |
| --- | --- |
| 显式多语言 build 自动创建 detached worktree | 撤除；所有语言继续在当前进程构建 |
| 干净 Git、tracked inputs、固定 HEAD 为构建前提 | 撤除；普通构建继续允许非 Git 目录及本地修改 |
| 内容快照及 Worker 请求/结果版本、hash 校验 | 撤除跨进程协议；保留原有一次获取内容 |
| worktree 所有权登记、Worker 取消、残留恢复 | 撤除；只管理本次输出事务资源 |
| 根级输出等价、失败不提交部分输出 | 保留并推广至公共 Engine 构建路径 |
| languageJobs 与 --jobs 两级并发 | 保留，languageJobs 继续限制进程内语言任务 |

原分支、其 3 个未提交文件及历史任务记录保持原状，不合并、不删除、不重写。后续实施不得同时满足互相冲突的自动隔离要求；若切换回原分支，必须先重新确认合同。只有新实现及验收完成后才同步正式 spec、归档新变更。

## Impact

- 内部使用方：现有内部站点通过 Core Engine/CLI 构建；SRBiz 是既有消费方，不在本次修改其站点或交付仓库。
- 交付责任：本 Core 任务执行者负责实现和证据，用户审阅合同与验收结果。
- 候选源文件：SiteEngine.cs、BuildPlanner.cs、OutputDirectoryCleaner.cs、IncrementalManifestReportWriter.cs，以及一个最小内部事务实现；CLI dev 的监听排除规则和专项测试按实际消费关系调整。
- 公开方法签名、配置、路由、渲染及插件业务行为不变；失败行为收敛为保护旧产物，资源竞争快速失败。
- 非目标：源码隔离、沙箱、断电恢复、无间隙目录读取、分布式锁、性能提升承诺、外部插件任意写入回滚；不修改 CI/门禁策略，不提交、推送、部署或发布。

## Validation

本次提案使用 `openspec validate add-in-process-build-transactions --type change --strict` 和文档差异检查。实施阶段按 tasks.md 生成最终闭包、核查写入边界并运行 Engine/CLI 完整专项；不隐含授权全量或 release gate。
