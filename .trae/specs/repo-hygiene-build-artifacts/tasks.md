# Tasks

## Task 1: git rm 移除已入库的构建产物
- [x] 1.1 `git rm -r examples/blog-site/.smoke-all-run-debug/`
- [x] 1.2 `git rm -r examples/component-theme/.smoke-all-run-debug/`
- [x] 1.3 `git rm examples/starter/.sitegen-smoke-ai-43269.yaml`
- [x] 1.4 验证 `git ls-files` 不再包含上述路径（外加发现 tests/fixtures/ 下有额外构建产物，一并移除）

## Task 2: 补 .gitignore 规则
- [x] 2.1 追加 `**/.smoke-all-run-debug/`
- [x] 2.2 追加 `examples/starter/.sitegen-smoke-ai-*.yaml`
- [x] 2.3 追加 `**/.bukit-build-state.json`
- [x] 2.4 追加 `**/.bukit-output-marker`
- [x] 2.5 追加 `tests/fixtures/**/.smoke-all-run/`（发现 tests/fixtures 下也有遗留产物）
- [x] 2.6 验证 `.gitignore` 语法正确，模式覆盖所有已知变体

## Task 3: CI 加入 repo hygiene 检查
- [x] 3.1 创建 `scripts/check-repo-hygiene.sh`
- [x] 3.2 在 `scripts/quality-gate.sh`（`dotnet build` 之前）加入调用
- [x] 3.3 验证脚本语法 `bash -n` 通过
- [x] 3.4 验证运行 `bash scripts/check-repo-hygiene.sh` 通过（Repo hygiene: clean）

# Task Dependencies

- Task 2 不依赖 Task 1（可并行）；但 Task 3 依赖 Task 1（清理后才能通过检查）
- 推荐顺序：Task 1 → Task 2 + Task 3（Task 2 和 3 可并行）
