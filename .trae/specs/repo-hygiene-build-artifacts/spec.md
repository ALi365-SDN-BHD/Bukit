# Repo Hygiene: 禁止 Smoke/Debug 构建产物入库

## Why

主线 `.gitignore` 已忽略 `examples/*/.smoke-all-run/`，但未覆盖 `.smoke-all-run-debug/`、`.bukit-build-state.json`、`.bukit-output-marker`、`examples/starter/.sitegen-smoke-ai-*.yaml` 等变体。当前 `main` 分支已包含 `examples/blog-site/.smoke-all-run-debug/` 和 `examples/component-theme/.smoke-all-run-debug/` 的完整构建输出（共约 120+ 文件），违反 release repo 清洁原则。

## What Changes

- `git rm -r` 移除已入库的构建产物
- 补 `.gitignore` 规则，覆盖所有已知 smoke/debug 构建输出模式
- 在 CI（`quality-gate.sh`）加检查步骤，禁止此类产物再次入库

## Impact

- Affected specs: `smoke-all`（构建输出目录命名相关）
- Affected code:
  - `.gitignore` — 追加忽略规则
  - `scripts/quality-gate.sh` — 追加 repo hygiene 检查
  - `scripts/smoke-all.sh` — 复核输出目录均在 `.gitignore` 覆盖范围内（如有必要）

## ADDED Requirements

### Requirement: .gitignore 禁止构建产物入库

`.gitignore` SHALL 包含以下模式，禁止 smoke/debug 构建产物被 git 跟踪：

- `examples/**/.smoke-all-run-debug/`
- `examples/starter/.sitegen-smoke-ai-*.yaml`
- `**/.bukit-build-state.json`
- `**/.bukit-output-marker`

#### Scenario: 所有构建产物被忽略

- **WHEN** 在 `examples/` 下任意站点运行 `dotnet run -- build` 生成 `.smoke-all-run-debug/`、`.bukit-build-state.json`、`.bukit-output-marker`
- **THEN** `git status` 不显示这些文件为未跟踪文件

### Requirement: CI 检查禁止构建产物入库

`scripts/quality-gate.sh` SHALL 包含一个检查步骤，验证仓库中不存在不应入库的构建产物。

#### Scenario: 清洁仓库通过

- **WHEN** CI 运行 `quality-gate.sh` 且仓库不含任何被禁止的构建产物
- **THEN** 检查通过，退出码 0

#### Scenario: 存在构建产物则失败

- **WHEN** `git ls-files` 匹配到 `examples/**/.smoke-all-run-debug/` 或 `**/.bukit-build-state.json` 或 `**/.bukit-output-marker` 或 `examples/starter/.sitegen-smoke-ai-*.yaml`
- **THEN** 输出 ERROR 并列出违规文件，退出码 1

### Requirement: 清理当前已入库的构建产物

仓库当前 `main` 分支中的以下构建产物 SHALL 被 `git rm -r` 移除：

- `examples/blog-site/.smoke-all-run-debug/`
- `examples/component-theme/.smoke-all-run-debug/`
- `examples/starter/.sitegen-smoke-ai-43269.yaml`

#### Scenario: 构建产物已从仓库移除

- **WHEN** 执行 `git rm -r` 后提交
- **THEN** `git ls-files` 不再包含上述路径
