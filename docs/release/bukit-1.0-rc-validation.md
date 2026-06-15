quality-gate ✅

cross-platform-tests ubuntu/windows/macos ✅
smoke-examples ✅

native-aot ubuntu/windows/macos ✅

stress-cli 手动通过 / 未触发说明

debug artifact cleanup ✅

import marked preview ✅

workflow evidence (ci.yml, completed success on main/master) ❌

workflow evidence file (json/md) `TestResults/release-gate/ci-workflow-evidence.json`, `TestResults/release-gate/rc-gate-evidence.md` ❌

## 发布操作模板（维护者）

- 先执行统一模板： [Release Precheck Template](release-prerelease-template.md)

release decision: BLOCKED

说明：当前 commit 仍未在 `main/master` 分支上有可追溯的 `workflow_runs` 真实绿灯记录；静态审计通过不能替代 CI 通过。
