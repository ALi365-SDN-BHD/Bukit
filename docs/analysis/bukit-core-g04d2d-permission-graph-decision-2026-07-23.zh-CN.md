# Bukit Core G-04D2D permission graph 决策

日期：2026-07-23

状态：`group-verification-pending`

## 决策

本任务仅处理 `Bukit.PluginHost` permission implementation graph：

- `PluginPermissionEvaluator` 保持 public，并以 public 无参构造继续提供原有默认入口；
- 注入 `PluginFileSystemPermissionEvaluator` 的构造改为 internal、非 optional；
- `PluginFileSystemPermissionEvaluator` 与
  `PluginPermissionPathNormalizer` 两个 CLR 类型及其构造/方法原子
  internalize；
- current baseline 由 14 assemblies / 507 public types / 103 candidates
  更新为 14 / 505 / 101；
- 关闭的 136-entry candidate manifest 保持历史记录与 Git blob
  `7b07d6890562387010b52301e9f8716e9bf10ed1` 不变。

这是一项 2.0 source/binary breaking 的 CLR access narrowing：直接构造两个候选，
或调用旧 `PluginPermissionEvaluator(PluginFileSystemPermissionEvaluator?)`
构造的外部消费者需要迁移；普通 `new PluginPermissionEvaluator()` 调用保持有效。
没有新增替代 API，因为受支持的产品入口仍是 retained evaluator 与
`bukit-plugin-v1` 进程协议。

## 不变边界

本任务没有修改 permission 算法、异常类型、诊断码、拒绝原因、路径比较、
配置 schema、插件协议或 wire error。normalizer 仍只执行词法声明规范化；本任务
没有实现，也不声称实现 symlink/reparse point 的物理文件系统防护。

没有新增 `InternalsVisibleTo`。现有入口测试继续从 public
`PluginPermissionEvaluator` 覆盖读写权限子集、相对路径规范化、绝对路径、`..`
与 `.bukit` 拒绝行为。

## 待验证证据

已添加架构断言，固定以下预期：

- 两个候选类型仍存在于 owning assembly，但不再导出；
- retained evaluator 只有 public 无参构造，candidate-typed 注入构造不是 public；
- current baseline 为 14 / 505 / 101，且删除两项候选记录；
- 历史 candidate manifest 的两项记录和精确 blob 均不变。

按 G1 总计划，本任务未运行 test、build、gate 或 Native AOT。上述断言与 PluginHost、
Architecture、安全路径检查统一留待 Task 10 执行；验证前不得把本状态改为关闭。
