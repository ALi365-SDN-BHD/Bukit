# Bukit Core G-04D2B2 `PluginHostErrorCodes` 单类型 internalization 决策账本

日期：2026-07-23

基线：`2.0@757fb14976ad7337edc2a6fbf925b986222dea6f`

状态：实施中；最终资格由本任务最新 handoff 决定

## 决策

只将 `Bukit.PluginHost.PluginHostErrorCodes` 的 containing type 从 public
收窄为 internal。六个 const 成员和值、五个 Host 实际诊断行为及
`plugin.permissionDenied` 保留词汇保持不变。

## 兼容边界

这是 2.0-only source/public-metadata/reflection breaking change。普通已编译
const consumer 可能继续使用内联字符串，但这不构成全面 binary compatibility
承诺。私有消费者继续为 `unknown-until-voluntary-declaration`。

## Governed delta

目标是 14 assemblies / 507 types / 103 candidates。closed 136-entry manifest
必须保持 blob `7b07d6890562387010b52301e9f8716e9bf10ed1`。

## 搜索证据限制

2026-07-22 认证公开搜索未发现目标匹配；2026-07-23 环境没有可校准的治理级
GitHub Code Search，因此没有把本轮连接器结果写成新的认证快照。

## 验证状态

RED、GREEN、focused、Native AOT、release smoke、published CLI process-plugin
proof、唯一 aggregate 和独立复审的最终状态，必须以本任务最新 handoff 的实测结果
为准；本文不提前宣称通过。

## 排除项

不修改 schema、插件协议、配置语义、CLI 行为、错误字符串、权限语义、
`PluginProtocolClient`、其他 PluginHost 类型、CI/release/gate 或 protected
reference areas。
