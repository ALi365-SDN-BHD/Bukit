<!-- 感谢贡献！请按以下模板补全信息，便于评审。 -->
## 概述

一句话说明本 PR 做了什么。

## 变更动机

背景/问题/收益。

## 关联

- Closes #
- Spec: .trae/specs/<change-id>/spec.md  <!-- ≥ 200 行变更必填；若 < 200 行可写 N/A -->
- 本 PR diff 行数：____ 行（若 > 400 行，请在"风险与回滚"中说明原因）
- Bug 复现测试（bugfix 必填）：`tests/.../XxxTests.cs#LNN`  <!-- 非 bugfix 写 N/A -->

## TDD 流程（Red → Green → Refactor）

- [ ] 🔴 Red：先编写一个会失败的测试用例，覆盖本次变更的预期行为
- [ ] 🟢 Green：以最小实现让上述失败测试通过
- [ ] 🔵 Refactor：在测试保护下重构与命名优化，无新增失败
- [ ] ⚪ N/A — 本 PR 不涉及代码逻辑（如纯文档、配置、CI 调整）

## OOP 自查（参见 `.trae/rules/project_rules.md` §1）

- [ ] 新增的有副作用服务类已定义 `I*` 接口（或本 PR 不新增此类服务）
- [ ] 依赖通过构造函数注入，未在业务类内 `new` 出有副作用依赖
- [ ] 单类承担单一职责，未出现"采集+处理+输出"复合职责

## 产品定位

- [ ] 变更服务于明确的内部消费者或维护现有受治理契约
- [ ] 未把 Labs、Import、WeChat 或外部插件表述为 Core 发布就绪
- [ ] 如涉及公开发布，已单独记录明确管理批准；否则按内部制品处理

## 质量门禁

- [ ] 本地已通过 `bash scripts/quality-gate.sh Release`
- [ ] 代码变更已运行目标测试，或已通过 `BUKIT_CI_FULL_SKIP_FAST=1 bash scripts/gates/ci-full.sh Release`
- [ ] 已运行 `bash scripts/checks/dotnet-format.sh`
- [ ] 如涉及发布产物、Native AOT、冒烟或安全表面，已运行对应 release-owned 检查

## 风险与回滚

<!-- 描述潜在风险与回滚方案 -->

## 截图/日志

<!-- 可选：附上截图或关键日志 -->
