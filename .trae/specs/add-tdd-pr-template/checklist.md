# Checklist

- [x] `.github/PULL_REQUEST_TEMPLATE.md` 文件已创建且位置正确（GitHub 默认能识别的路径）
- [x] 模板正文中可见三个独立的 TDD 复选框，分别对应 "🔴 Red"、"🟢 Green"、"🔵 Refactor"
- [x] 每个 TDD 复选框旁边带有一句中文说明（含义、入口动作或验证标准）
- [x] 模板提供 "N/A — 本 PR 不涉及代码逻辑" 的兜底复选框，避免阻碍纯文档/配置类 PR
- [x] 模板包含 "质量门禁" 小节，至少含：`bash scripts/quality-gate.sh` 已通过、覆盖率 ≥ 80%、`dotnet format` 无变更 三个复选框
- [x] 模板包含 "关联 Issue / Spec" 链接占位（例如 `Closes #` / `Spec:` 字段）
- [x] 所有复选框使用合法的 `- [ ]` Markdown 语法，未引入会破坏 GitHub 渲染的 HTML
- [x] 文件以 UTF-8 + LF 行尾保存，无 BOM
- [x] 仅新增 `.github/PULL_REQUEST_TEMPLATE.md` 一个文件，不修改其他源码/CI/脚本
