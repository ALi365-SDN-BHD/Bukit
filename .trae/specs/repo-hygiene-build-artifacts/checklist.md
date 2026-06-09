# Repo Hygiene Checklist

## 构建产物已移除
- [x] `examples/blog-site/.smoke-all-run-debug/` 已从 git 移除
- [x] `examples/component-theme/.smoke-all-run-debug/` 已从 git 移除
- [x] `examples/starter/.sitegen-smoke-ai-43269.yaml` 已从 git 移除
- [x] `tests/fixtures/taxonomy-site/.smoke-all-run-debug/` 已从 git 移除（额外发现）
- [x] 所有 `tests/fixtures/**/.smoke-all-run/` 已从 git 移除（额外发现）
- [x] 所有被跟踪的 `.bukit-build-state.json` 和 `.bukit-output-marker` 已从 git 移除（额外发现，包括 `examples/starter/smoke/out2/` 和 `tests/fixtures/*/.test-tmp-*/`）

## .gitignore 覆盖完整
- [x] `**/.smoke-all-run-debug/` 已加入 `.gitignore`
- [x] `examples/starter/.sitegen-smoke-ai-*.yaml` 已加入 `.gitignore`
- [x] `**/.bukit-build-state.json` 已加入 `.gitignore`
- [x] `**/.bukit-output-marker` 已加入 `.gitignore`
- [x] `tests/fixtures/**/.smoke-all-run/` 已加入 `.gitignore`（额外发现）

## CI 检查生效
- [x] `scripts/check-repo-hygiene.sh` 已创建，逻辑：`git ls-files` 匹配 `.smoke-all-run-debug/`、`.smoke-all-run/`、`.sitegen-smoke-ai-`、`.bukit-build-state.json`、`.bukit-output-marker`
- [x] `scripts/quality-gate.sh` 在 `dotnet build` 之前调用 `bash scripts/check-repo-hygiene.sh`
- [x] 脚本语法 `bash -n` 通过
- [x] 本地运行 `bash scripts/check-repo-hygiene.sh` 输出 "Repo hygiene: clean" 退出码 0
