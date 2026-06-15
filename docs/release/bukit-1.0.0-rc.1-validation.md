# Bukit 1.0.0 RC1 Validation Report

## Scope

Bukit 1.0.0 RC1 validates the stable core static-site engine.

Preview features such as `import html-demo`, `import seed`, Notion push, and demo-to-theme migration are not part of the 1.0 stable core compatibility contract.

## CI Results

| Check                               | Status |
| :---------------------------------- | :----- |
| quality-gate                        | PASS   |
| cross-platform-tests ubuntu-latest  | PASS   |
| cross-platform-tests windows-latest | PASS   |
| cross-platform-tests macos-latest   | PASS   |
| smoke-examples                      | PASS   |
| native-aot ubuntu-latest            | PASS   |
| native-aot windows-latest           | PASS   |
| native-aot macos-latest             | PASS   |
| stress-cli                          | PASS   |

## Workflow Evidence (GitHub Actions Ground Truth)

| Check | Status |
| :---- | :----- |
| `actions/workflows/ci.yml` runs for target commit (`workflow_runs`) on main/master (`head_branch`) | ❌ BLOCKED (`workflow_runs` currently empty for last snapshot) |
| Release gate precondition (completed success run on main/master required) | ❌ BLOCKED |
| Evidence output file required | `TestResults/release-gate/ci-workflow-evidence.json` (required), `TestResults/release-gate/rc-gate-evidence.md` (for reviewer-visible proof) |

## 发布操作模板（维护者）

- 先执行统一模板： [Release Precheck Template](release-prerelease-template.md)

## Repository Hygiene

| Check                                | Status |
| :----------------------------------- | :----- |
| No smoke/debug artifacts tracked     | PASS   |
| No `.smoke-all-run-debug` tracked    | PASS   |
| No `.sitegen-smoke-ai-*` tracked     | PASS   |
| No `.bukit-build-state.json` tracked | PASS   |
| No `.bukit-output-marker` tracked    | PASS   |

## Release Decision

Bukit 1.0.0 Core is **not ready** for RC1.

Reason: 无法建立 `main/master` 分支上真实 CI 绿灯证据前，不能把静态检查通过等同于 RC 通过。
