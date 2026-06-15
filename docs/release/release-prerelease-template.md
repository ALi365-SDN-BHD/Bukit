# Release Precheck Template（维护者）

## 发布前主干 CI 预检（强制）

1. 先在 `main` 或 `master` 上提交待发布改动，并等待 `ci.yml` 全量通过。
2. 在打 tag 前确认同一 commit 的主干 CI 绿灯：
   - 目标 commit 的 `workflow_runs` 至少存在 1 条 `ci.yml` 记录，且 `status=completed`、`conclusion=success`，`head_branch` 为 `main` 或 `master`。
3. 确认无误后再创建并推送 tag：
   - `git tag vX.Y.Z`
   - `git push origin vX.Y.Z`
4. 触发/等待 release，确认 `release-gate` 产物中包含：
   - `TestResults/release-gate/ci-workflow-evidence.json`
   - `TestResults/release-gate/rc-gate-evidence.md`

## Release order（v1.0.2 起）

1. Merge to main
2. Wait for ci.yml completed success
3. Confirm workflow evidence
4. Create tag v1.0.2
5. Release workflow runs
