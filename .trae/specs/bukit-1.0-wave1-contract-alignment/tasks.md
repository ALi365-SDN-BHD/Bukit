# Tasks: Bukit 1.0 Wave 1 仓库自一致性

## 任务包 1：修复 `smoke.sh` 当前失败链条 ✅

- [x] Task 1.1: 复现 smoke 失败 → **已通过，无需修复**
- [x] Task 1.2: 验证所有核心命令通过
  - [x] `bash scripts/smoke.sh Release` → PASSED
  - [x] `bash scripts/smoke-all.sh Release` → 17/17 PASSED
  - [x] `bash scripts/security-regression.sh Release` → PASSED
  - [x] `dotnet test bukit.slnx -c Release --no-restore` → PASSED

## 任务包 2：统一 starter / example / doctor / docs 契约

- [ ] Task 2.1: 修复技能文档中的文件引用错误
  - [ ] 修复 `src/skills/bukit-config/SKILL.md` 中的无效文件引用
  - [ ] 修复 `src/skills/bukit-dev/SKILL.md` 中的无效文件引用
  - [ ] 修复 `src/skills/bukit-import/SKILL.md` 中的无效文件引用
  - [ ] 修复 `src/skills/bukit-notion/SKILL.md` 中的无效文件引用
  - [ ] 修复 `src/skills/bukit-seo/SKILL.md` 中的无效文件引用
  - [ ] 修复 `src/skills/AGENTS.md` 中的无效文件引用
  - [ ] 修复 `src/skills/CLAUDE.md` 中的无效文件引用
  - [ ] 修复 `src/skills/GEMINI.md` 中的无效文件引用
  - [ ] 修复 `src/skills/copilot-instructions.md` 中的无效文件引用
  - [ ] 修复 `src/skills/README.md` 中的无效文件引用

- [ ] Task 2.2: 修复技能文档中 CLI 命令引用不一致
  - [ ] 在 `bukit-cli-reference/SKILL.md` 中补充缺失的 CLI 命令文档
  - [ ] 或从各技能文档中移除对不存在 CLI 命令的引用

- [ ] Task 2.3: 修复代理入口文件技能数量
  - [ ] 修复 `CLAUDE.md` 中技能数量（19 → 20）
  - [ ] 修复 `GEMINI.md` 中技能数量（19 → 20）

- [ ] Task 2.4: 为缺失 `bukit.templates.yaml` 的示例站点补齐
  - [ ] `examples/blog-site/layouts/bukit.templates.yaml`
  - [ ] `examples/corporate-site/layouts/bukit.templates.yaml`
  - [ ] `examples/docs-site/layouts/bukit.templates.yaml`
  - [ ] `examples/plugin-site/layouts/bukit.templates.yaml`
  - [ ] `examples/multilingual-site/layouts/bukit.templates.yaml`
  - [ ] `examples/theme-inheritance-site/layouts/bukit.templates.yaml`

- [ ] Task 2.5: 为缺失 `theme.yaml` 的示例站点补齐
  - [ ] `examples/corporate-site/layouts/theme.yaml`
  - [ ] `examples/multilingual-site/layouts/theme.yaml`
  - [ ] `examples/theme-inheritance-site/layouts/theme.yaml`

- [ ] Task 2.6: 补齐 `starter` 的 `theme.yaml` 缺失字段
  - [ ] 添加 `engine: bukit`
  - [ ] 添加 `min_engine_version` 字段

- [ ] Task 2.7: 修复 `bukit-config` 技能中 `deploy` 节点文档
  - [ ] 标注 `deploy` 为 "planned" 或 "experimental" 状态

- [ ] Task 2.8: 验证所有示例站点 doctor 通过
  - [ ] 对每个示例站点运行 `bukit doctor` 并确认无 ERROR
  - [ ] 修复发现的任何配置/模板结构问题

## 任务包 3：把 Wave 1 修复固化为回归

- [ ] Task 3.1: 确保 `docs check` 通过
  - [ ] 运行 `dotnet run --project src/Bukit.Cli -c Release -- docs check`
  - [ ] 确认文件引用错误清零
  - [ ] 确认 CLI 命令引用警告清零

- [ ] Task 3.2: 运行全部验证命令确认回归
  - [ ] `bash scripts/smoke.sh Release`
  - [ ] `bash scripts/smoke-all.sh Release`
  - [ ] `bash scripts/security-regression.sh Release`
  - [ ] `dotnet test bukit.slnx -c Release --no-restore`

# Task Dependencies

- Task 2.1 ~ 2.7 相互独立，可并行执行
- Task 2.8 依赖 Task 2.4、2.5、2.6 完成
- Task 3.1 依赖 Task 2.1、2.2 完成
- Task 3.2 依赖所有 Task 2.x 完成
