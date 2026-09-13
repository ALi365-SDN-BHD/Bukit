## ADDED Requirements

### Requirement: Preserve in-process build semantics
系统 SHALL 保留一次内容获取及进程内语言并发，不因 site.languages 非空要求 Git、干净 checkout、worktree 或 Worker；languageJobs 与 --jobs MUST 保持各自语言并发和页面并发含义。

#### Scenario: Local multilingual input
- **WHEN** 用户在非 Git 站点或带未提交修改的站点执行有效构建
- **THEN** 系统按本地输入构建，不以 Git 状态拒绝请求，且不创建 worktree 或语言子进程

### Requirement: Serialize conflicting build resources
系统 MUST 在任何受管目标写入之前，对规范化后的输出、缓存及事务内外部报告资源获取互斥锁；任一共享资源的竞争构建 MUST 在修改产物之前失败并给出可识别诊断。

#### Scenario: Different outputs share one cache
- **WHEN** 第二个构建使用不同输出但与活动构建共享缓存
- **THEN** 第二个构建被拒绝且双方正式产物保持不受其修改

#### Scenario: Disjoint builds
- **WHEN** 两个构建的资源集合完全不相交
- **THEN** 不因同一机器或仓库而被全局锁串行化

### Requirement: Preserve protected output and incremental behavior
系统 MUST 在暂存前保留危险路径与 marker 检查，并 SHALL 在暂存副本上执行现有 clean、no-clean、迁移和所有权清理语义。临时路径 MUST NOT 改变持久化增量身份或正式报告路径。

#### Scenario: Unsafe existing output
- **WHEN** 清理目标为危险目录或缺少所需 marker 的非空目录
- **THEN** 构建拒绝且原有文件保持不变

#### Scenario: Unchanged incremental build
- **WHEN** 同一输入在成功构建后再次以增量方式构建
- **THEN** 随机 staging 路径不引发全量重渲染，no-clean 下非自有文件仍保留

### Requirement: Stage the complete managed write set
系统 MUST 将正式输出、受管缓存、manifest、报告、外部 metrics 及完成状态的本次修改写入受控 staging，在全部语言、聚合和安全门禁成功前不得替换正式目标。

#### Scenario: Build or gate fails
- **WHEN** 内容获取、语言渲染、聚合、报告或安全门禁失败，或提交前取消
- **THEN** 正式输出、受管缓存、manifest 和已有外部 metrics 保持原值，仅清理本次临时资源

### Requirement: Coordinate commit and recover exchange failure
系统 SHALL 通过唯一 backup 协调提交全部目标；交换失败 MUST 尝试反向恢复。恢复失败 MUST 保留可恢复旧数据及明确诊断。成功提交后 MUST 才发布完成信号。

#### Scenario: A later target cannot be exchanged
- **WHEN** 输出已交换而后续缓存或报告交换失败
- **THEN** 系统恢复已交换目标并返回失败；若恢复失败，保留 backup 并报告恢复路径，不删除唯一旧副本

#### Scenario: Cleanup fails after commit
- **WHEN** 所有目标已成功提交但旧 backup 清理失败
- **THEN** 系统保留新产物，明确提交已完成及清理残留，不误报为未提交或执行破坏性回滚

### Requirement: Cover public engine consumers
公共 Engine 构建入口 MUST 对单语言、多语言、CLI build、deploy 内部构建及 dev 构建应用同一保护流程，并 SHALL 保留成功输出的路由、SEO/GEO、插件产物和内容语义。

#### Scenario: Development rebuild fails
- **WHEN** dev 重建失败
- **THEN** 旧输出仍保留且不发送成功刷新通知，事务目录事件不触发重建循环

#### Scenario: Deployment build fails
- **WHEN** deploy 内部构建失败
- **THEN** 不调用后续部署动作，旧构建产物仍保留

#### Scenario: Existing engine caller succeeds
- **WHEN** 直接 Engine 调用成功构建同一输入
- **THEN** 返回结果及报告引用正式路径，公开产物除原有非确定时间指标外与原流程等价
