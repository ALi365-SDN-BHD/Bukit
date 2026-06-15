# Bukit 1.0.2 Release Checklist（P0-2，硬门槛）

## 目标

1.0.2 发布前必须基于真实 CI 绿灯证据（GitHub Actions workflow run completed success）。
以下条目全部满足后，release 才允许继续。

## 1.0.2 Release Hard Gate

| # | 条目 | 证据文件 | 位置 | 通过标准 |
|---|---|---|---|---|
| P0 | ci.yml completed success evidence | `ci-workflow-evidence.json` | `TestResults/release-gate/ci-workflow-evidence.json` | `workflow_runs` 必须包含至少 1 条 status=`completed` 且 conclusion=`success` 的 `ci.yml` 记录（针对同一 commit SHA）。 |
| P0 | Evidence Markdown | `rc-gate-evidence.md` | `TestResults/release-gate/rc-gate-evidence.md` | 文件存在且非空。 |
| P0 | release artifact smoke report | `release-artifact-smoke.md` | `TestResults/release-gate/native-aot/linux-x64/release-artifact-smoke.md`（release-gate 产物） | 文件存在且包含步骤记录（至少 1 个 `PASS` 条目）。 |
| P1 | config schema artifact | `site.schema.json` | `TestResults/release-gate/site.schema.json` | 文件存在且为有效 JSON（可解析）。 |
| P1 | Coverage summary | `coverage-summary.txt` | `TestResults/release-gate/coverage-summary.txt` | 文件存在且包含覆盖率摘要字段。 |
| P1 | Native AOT 发布构建 | `linux-x64` native-aot 产物 | `TestResults/release-gate/native-aot/linux-x64/` | 目录存在并包含 release smoke 可追溯产物。 |

## 阻断规则

- 缺任何 P0 条目：**Release BLOCKED**（阻断发布）。
- 缺 P1 条目：**Release BLOCKED**（待补齐）。

## 发版前最终验收（人工核对）

- GitHub Actions `release` 的 `release-gate` job 通过。
- 在 release artifact 下载包中能看到以下文件/目录：
  - `TestResults/release-gate/ci-workflow-evidence.json`
  - `TestResults/release-gate/rc-gate-evidence.md`
  - `TestResults/release-gate/site.schema.json`
  - `TestResults/release-gate/coverage-summary.txt`
  - `TestResults/release-gate/native-aot/linux-x64/`
  - `TestResults/release-gate/native-aot/linux-x64/release-artifact-smoke.md`

## 备注

- `GITHUB_ACTIONS` 判定必须以 true/false 兼容语义解析，避免 `true` 被误判为非 CI 环境导致证据检查跳过。

