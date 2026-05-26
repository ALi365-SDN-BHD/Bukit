# Tasks

- [x] Task 1: 起草并新增 `.github/PULL_REQUEST_TEMPLATE.md`
  - [x] SubTask 1.1: 编写 PR 标题/概述/变更动机三段式正文骨架
  - [x] SubTask 1.2: 编写 "TDD 流程（Red → Green → Refactor）" 小节，含 3 个独立 `- [ ]` 复选框 + 每项一句中文说明 + 一个 "N/A 非代码变更" 的兜底复选框
  - [x] SubTask 1.3: 编写 "质量门禁" 小节，含 `bash scripts/quality-gate.sh` 通过、覆盖率 ≥ 80%、`dotnet format` 无变更等复选框
  - [x] SubTask 1.4: 编写 "关联 Issue / Spec / 风险评估" 等常规小节
- [x] Task 2: 本地与规范一致性验证
  - [x] SubTask 2.1: 用 `grep` 确认模板文件包含 "Red"、"Green"、"Refactor"、"quality-gate" 等关键字
  - [x] SubTask 2.2: 校验 Markdown 渲染：所有复选框使用合法的 `- [ ]` 语法且能在 GitHub 上正常显示

# Task Dependencies
- Task 2 依赖于 Task 1
