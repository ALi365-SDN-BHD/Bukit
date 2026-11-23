# Tasks

唯一勾选来源。只有取得实际证据（测试通过、提交完成）的事项才勾选。任务编号和顺序与
[任务计划书](../../../docs/plans/2026-11-23-cloudbase-content-source-task-plan.zh-CN.md)第 3 节一致，
按单写者串行链 T00 → T02 → T01 → T03 → T05 → T04 → T06 → T07 → T08 → T09 → T10 执行。

## 0. 准备

- [x] T00 批准记录、队列、policy 规则与本 openspec 变更：G0 记录和调研结果写进 proposal.md；
      `codex-workflow-policy.v1.json` 新增 CloudBase pathRules，工具门禁归入 `dotnet-serial`；
      完整候选清单的 closure 没有 unmapped 文件；`openspec validate --strict` 通过。

## 1. 基础

- [x] T02 诊断码 `CloudBase*`（`0x0C01`～`0x0C0B`）、formatter 说明、public API 基线、`guide/dev/cli.md` 消费方契约表。
- [x] T01 配置模型与校验：internal `CloudBaseConfig`、读取、严格字段校验、语义校验（含 D8 ①②④ 与 ③ 的 `onFailure` 部分、D13、D18）、JSON Schema。
- [x] T03 `Bukit.CloudBase` transport（PostgREST 查询、存储批量签名、JWT `role` 诊断解码）与新项目登记（解决方案、受治理程序集、架构计数、覆盖率清单），行覆盖率不低于 70%。

## 2. 主体

- [x] T05 Markdown 渲染的不可信链接模式与 `CloudBaseUrlPolicy`（D9）。
- [x] T04 Provider、字段投影、错误映射、工厂接入，以及构建阶段的占位图检查（D8 ③）。
- [x] T06 文件对象批量签名（对象名与 `cloud://` 兼容形式）、D17 降级、`SignedUrlIdentity` 的 CloudBase 规则、D8 ⑤。

## 3. 收口

- [x] T07 构建阶段和部署阶段的公开产物隐私检查覆盖 CloudBase 标识（D11）。
- [x] T08 `doctor` 的 CloudBase 检查：`role` 声明（`service_role` 警告）、表可达、RLS 空结果提示、存储签名。
- [x] T09 用户、开发、技能、排错文档与三语 CHANGELOG。
- [ ] T10 归档（需授权）、最终验证、统一审查。
