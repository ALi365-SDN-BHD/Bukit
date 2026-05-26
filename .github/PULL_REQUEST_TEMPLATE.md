<!-- 感谢贡献！请按以下模板补全信息，便于评审。 -->
## 概述

一句话说明本 PR 做了什么。

## 变更动机

背景/问题/收益。

## 关联

- Closes #
- Spec: .trae/specs/<change-id>/spec.md

## TDD 流程（Red → Green → Refactor）

- [ ] 🔴 Red：先编写一个会失败的测试用例，覆盖本次变更的预期行为
- [ ] 🟢 Green：以最小实现让上述失败测试通过
- [ ] 🔵 Refactor：在测试保护下重构与命名优化，无新增失败
- [ ] ⚪ N/A — 本 PR 不涉及代码逻辑（如纯文档、配置、CI 调整）

## 质量门禁

- [ ] 本地已通过 `bash scripts/quality-gate.sh`
- [ ] 覆盖率 ≥ 80%（详见 TestResults/coverage-report/Summary.txt）
- [ ] `dotnet format bukit.slnx --verify-no-changes` 无变更

## 风险与回滚

<!-- 描述潜在风险与回滚方案 -->

## 截图/日志

<!-- 可选：附上截图或关键日志 -->
