# Checklist: Bukit 1.0 Wave 1 仓库自一致性

## 任务包 1：smoke.sh 验证

- [x] `bash scripts/smoke.sh Release` 退出码为 0
- [x] `bash scripts/smoke-all.sh Release` 17/17 通过
- [x] `bash scripts/security-regression.sh Release` 通过
- [x] `dotnet test bukit.slnx -c Release --no-restore` 0 失败

## 任务包 2：契约统一

### 技能文档文件引用
- [ ] `docs check` 无 "File reference not found" 错误
- [ ] `src/skills/bukit-config/SKILL.md` 文件引用有效
- [ ] `src/skills/bukit-dev/SKILL.md` 文件引用有效
- [ ] `src/skills/bukit-import/SKILL.md` 文件引用有效
- [ ] `src/skills/bukit-notion/SKILL.md` 文件引用有效
- [ ] `src/skills/bukit-seo/SKILL.md` 文件引用有效
- [ ] `src/skills/AGENTS.md` 文件引用有效
- [ ] `src/skills/CLAUDE.md` 文件引用有效
- [ ] `src/skills/GEMINI.md` 文件引用有效
- [ ] `src/skills/copilot-instructions.md` 文件引用有效
- [ ] `src/skills/README.md` 文件引用有效

### CLI 命令引用一致性
- [ ] `docs check` 无 "CLI command ... is not documented" 警告

### 代理入口文件
- [ ] `CLAUDE.md` 技能数量为 20
- [ ] `GEMINI.md` 技能数量为 20

### 示例站点 bukit.templates.yaml
- [ ] `examples/blog-site/layouts/bukit.templates.yaml` 存在
- [ ] `examples/corporate-site/layouts/bukit.templates.yaml` 存在
- [ ] `examples/docs-site/layouts/bukit.templates.yaml` 存在
- [ ] `examples/plugin-site/layouts/bukit.templates.yaml` 存在
- [ ] `examples/multilingual-site/layouts/bukit.templates.yaml` 存在
- [ ] `examples/theme-inheritance-site/layouts/bukit.templates.yaml` 存在

### 示例站点 theme.yaml
- [ ] `examples/corporate-site/layouts/theme.yaml` 存在
- [ ] `examples/multilingual-site/layouts/theme.yaml` 存在
- [ ] `examples/theme-inheritance-site/layouts/theme.yaml` 存在

### starter theme.yaml 完整性
- [ ] `examples/starter/layouts/theme.yaml` 包含 `engine: bukit`
- [ ] `examples/starter/layouts/theme.yaml` 包含 `min_engine_version`

### bukit-config 技能 deploy 节点
- [ ] `deploy` 节点标注了支持层级

### 示例站点 doctor 通过
- [ ] `examples/starter/` doctor 无 ERROR
- [ ] `examples/blog-site/` doctor 无 ERROR
- [ ] `examples/corporate-site/` doctor 无 ERROR
- [ ] `examples/docs-site/` doctor 无 ERROR
- [ ] `examples/plugin-site/` doctor 无 ERROR
- [ ] `examples/multilingual-site/` doctor 无 ERROR
- [ ] `examples/theme-inheritance-site/` doctor 无 ERROR
- [ ] `examples/component-theme/` doctor 无 ERROR

## 任务包 3：回归验证

- [ ] `dotnet run --project src/Bukit.Cli -c Release -- docs check` 错误和警告显著减少
- [ ] `bash scripts/smoke.sh Release` 通过
- [ ] `bash scripts/smoke-all.sh Release` 通过
- [ ] `bash scripts/security-regression.sh Release` 通过
- [ ] `dotnet test bukit.slnx -c Release --no-restore` 通过
