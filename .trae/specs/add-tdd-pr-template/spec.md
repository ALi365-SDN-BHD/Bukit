# 引入 TDD 流程化的 PR 模板 Spec

## Why
项目已有完整的测试套件与 TDD Skill 规范，但缺乏在 Pull Request 评审入口处强制提醒贡献者按"Red → Green → Refactor"节奏工作的机制，导致 TDD 文化主要靠维护者个人自觉。引入一个轻量的 PR 模板，将 TDD 步骤变成每次提交都必须勾选/填写的清单，使 TDD 走向流程化与可审计化。

## What Changes
- 新增 `.github/PULL_REQUEST_TEMPLATE.md`，作为 GitHub 在创建 PR 时自动加载的默认模板。
- 模板包含一个"TDD 流程"小节，提供 3 个勾选项分别对应 **Red / Green / Refactor** 三个阶段，并附简短说明文字。
- 模板兼顾仓库已有质量门禁：增加"quality-gate 通过 / 覆盖率 ≥ 80%"等通用 checklist，避免维护者额外重复维护多份模板。
- 不引入任何代码或 CI 强校验逻辑（GitHub 不强制 PR 模板勾选，是约定式的流程化工具）。

## Impact
- Affected specs: 无（流程类变更，无已有需求被修改/删除）
- Affected code: 仅新增 `.github/PULL_REQUEST_TEMPLATE.md` 一份 Markdown 文件
- 不影响构建、CI、运行时行为

## ADDED Requirements

### Requirement: TDD PR 模板
仓库 SHALL 提供一份默认 Pull Request 模板，在贡献者创建 PR 时自动加载，并在模板正文中显式包含 "Red → Green → Refactor" 三个阶段的可勾选项，引导贡献者按 TDD 节奏完成变更。

#### Scenario: 创建新 PR 时自动加载模板
- **WHEN** 贡献者在 GitHub 上对本仓库发起一个新的 Pull Request
- **THEN** PR 描述输入框 SHALL 被预填充 `.github/PULL_REQUEST_TEMPLATE.md` 的内容，且其中可见 "🔴 Red"、"🟢 Green"、"🔵 Refactor" 三个独立的复选框

#### Scenario: TDD 步骤可逐项勾选
- **WHEN** 贡献者审视 PR 模板中的 TDD 小节
- **THEN** 贡献者 SHALL 能够独立勾选三个步骤之一或全部，每个步骤旁边带有一句简短解释（例如 Red：先写失败用例；Green：以最小实现让测试通过；Refactor：在测试保护下重构）

#### Scenario: 模板配合质量门禁
- **WHEN** 贡献者填写模板
- **THEN** 模板 SHALL 同时包含一个"质量门禁"小节，至少覆盖：本地已运行 `scripts/quality-gate.sh`、覆盖率达到 ≥ 80% 阈值、`dotnet format` 无变更

#### Scenario: 不存在的 TDD 步骤的兜底说明
- **WHEN** 变更类型不涉及代码逻辑（例如纯文档更新）
- **THEN** 模板 SHALL 允许贡献者勾选一个"N/A — 本 PR 不涉及代码逻辑"的选项以跳过 TDD 勾选，避免模板成为非代码变更的阻碍

## MODIFIED Requirements
（无）

## REMOVED Requirements
（无）
